#!/usr/bin/env python3
"""Fail if a Thunderstore zip would be mangled by Gale's / r2modman's BepInEx install rules.

Mirrors Gale's SubdirInstaller (src-tauri/src/profile/install/installers/subdir.rs): a file whose path has no
plugins/patchers/monomod/core/config folder falls into the default `plugins` target and is FLATTENED to its bare file
name (Assets/foo/bar.png -> BepInEx/plugins/<Author-Name>/bar.png). Under a plugins/ folder the sub-tree is kept.

Usage:
  check-gale-layout.py <zip> <Author-Name> [--expect REL ...] [--expect-tree SRC_DIR:ZIP_DIR ...] [--ignore NAME ...]

  --expect REL                 path (relative to BepInEx/plugins/<Author-Name>/) that must exist after install,
                               e.g. --expect MyMod.dll --expect Assets/data.json
  --expect-tree SRC:ZIPDIR     every file under repo folder SRC must be in the zip under ZIPDIR (e.g. Assets:plugins/Assets)
  --ignore NAME                file name that --expect-tree may skip (repeatable)
  --allow-root-dir NAME        top-level folder that is store-listing/docs only (e.g. screenshots); it may stay outside
                               plugins/ (Gale flattens it to loose files, which is harmless because nothing reads them)

Rules enforced (exit 1 on any):
  1. every file except manifest.json/icon.png/README.md/CHANGELOG.md sits under a Gale-recognized folder
     (plugins/ patchers/ monomod/ core/ config/), so nothing is flattened and nothing depends on flattening;
  2. no two files collapse onto the same installed path;
  3. manifest.json has the Thunderstore fields (a description over 250 chars only warns), icon.png is a 256x256 PNG at the root, README.md is present;
  4. any --expect / --expect-tree paths land where the mod looks for them.
"""
import json, os, struct, sys, zipfile

SUBDIRS = {"plugins": "BepInEx/plugins", "patchers": "BepInEx/patchers", "monomod": "BepInEx/monomod",
           "core": "BepInEx/core", "config": "BepInEx/config"}
ROOT_ONLY = {"manifest.json", "icon.png", "README.md", "CHANGELOG.md"}


def map_file(path, pkg):
    """Where Gale installs `path`, and whether it was flattened (no recognized folder)."""
    parts = [p for p in path.split("/") if p]
    for i, name in enumerate(parts):
        key = name.lower()
        if key in SUBDIRS or name.endswith(".mm.dll"):
            sub = key if key in SUBDIRS else "monomod"
            rest = parts[i + 1:]
            if sub == "config":
                return "/".join([SUBDIRS[sub], *rest]), False
            return "/".join([SUBDIRS[sub], pkg, *rest]), False
    return "/".join([SUBDIRS["plugins"], pkg, parts[-1]]), True


def main(argv):
    if len(argv) < 2:
        print(__doc__); return 2
    zpath, pkg = argv[0], argv[1]
    expect, trees, ignore, allow = [], [], set(), set()
    i = 2
    while i < len(argv):
        if argv[i] == "--expect": expect.append(argv[i + 1]); i += 2
        elif argv[i] == "--expect-tree": trees.append(argv[i + 1]); i += 2
        elif argv[i] == "--ignore": ignore.add(argv[i + 1]); i += 2
        elif argv[i] == "--allow-root-dir": allow.add(argv[i + 1]); i += 2
        else: print("unknown arg", argv[i]); return 2
    z = zipfile.ZipFile(zpath)
    names = [n for n in z.namelist() if not n.endswith("/")]
    errors = []
    mapped, flattened = {}, []
    for n in names:
        if n in ROOT_ONLY:
            continue
        m, flat = map_file(n, pkg)
        mapped[n] = m
        if flat:
            flattened.append(n)
    base = f"BepInEx/plugins/{pkg}/"
    for n in flattened:
        if n.split("/")[0] in allow and "/" in n:
            continue
        errors.append(f"not under plugins/ (Gale would flatten it): {n} -> {mapped[n]}")
    seen = {}
    for n, m in mapped.items():
        if m in seen:
            errors.append(f"collision: {n} and {seen[m]} both install to {m}")
        seen[m] = n
    for rel in expect:
        if base + rel not in seen:
            errors.append(f"would not exist after install: {base + rel}")
    for t in trees:
        src, zdir = t.split(":", 1)
        zdir = zdir.strip("/")
        for root, _, files in os.walk(src):
            for f in files:
                if f in ignore:
                    continue
                rel = os.path.relpath(os.path.join(root, f), src).replace(os.sep, "/")
                if f"{zdir}/{rel}" not in names:
                    errors.append(f"missing from zip: {zdir}/{rel} (from {src}/{rel})")
    # store-listing checks
    if "manifest.json" not in names:
        errors.append("manifest.json missing at zip root")
    else:
        try:
            mf = json.loads(z.read("manifest.json").decode("utf-8-sig"))
            for k in ("name", "version_number", "website_url", "description", "dependencies"):
                if k not in mf:
                    errors.append(f"manifest.json missing field: {k}")
            if len(mf.get("description", "")) > 250:
                print(f"::warning::manifest.json description is {len(mf['description'])} characters (Thunderstore's limit is 250)", file=sys.stderr)
        except Exception as e:
            errors.append(f"manifest.json unreadable: {e}")
    if "icon.png" not in names:
        errors.append("icon.png missing at zip root")
    else:
        data = z.read("icon.png")
        if data[:8] != b"\x89PNG\r\n\x1a\n":
            errors.append("icon.png is not a PNG")
        elif struct.unpack(">II", data[16:24]) != (256, 256):
            errors.append("icon.png must be 256x256, got %dx%d" % struct.unpack(">II", data[16:24]))
    if "README.md" not in names:
        errors.append("README.md missing at zip root")
    if errors:
        print("::error::Gale would mangle this package.", file=sys.stderr)
        for e in errors[:12]:
            print("  " + e, file=sys.stderr)
        if len(errors) > 12:
            print(f"  ... and {len(errors) - 12} more", file=sys.stderr)
        return 1
    print(f"OK Gale layout: {len(mapped)} files keep their folders under {base}")
    return 0


sys.exit(main(sys.argv[1:]))
