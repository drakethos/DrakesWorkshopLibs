#!/usr/bin/env python3
"""Publish a Thunderstore-compatible zip to Hexium without tcli's SemVer check.

Hexium accepts -alpha.X / -beta.X / -rc.X in version_number (see hexium.gg/packaging).
tcli rejects those client-side; this script uses the same upload API Hexium already
exposes for tcli, and lets the zip's manifest.json supply the version.
"""
from __future__ import annotations

import argparse
import base64
import hashlib
import json
import os
import sys
import urllib.error
import urllib.request
from pathlib import Path


def api_url(repo: str, path: str) -> str:
    repo = repo.rstrip("/")
    if not repo.startswith("http"):
        repo = "https://" + repo
    return f"{repo}/{path.lstrip('/')}"


def request_json(
    method: str,
    url: str,
    token: str,
    body: dict | None = None,
    expect: int | None = None,
) -> dict:
    data = None
    headers = {
        "Authorization": f"Bearer {token}",
        "Accept": "application/json",
    }
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=600) as resp:
            raw = resp.read().decode("utf-8")
            status = resp.status
    except urllib.error.HTTPError as e:
        err = e.read().decode("utf-8", errors="replace")
        raise SystemExit(f"HTTP {e.code} {method} {url}\n{err}") from e
    if expect is not None and status != expect:
        raise SystemExit(f"Expected HTTP {expect}, got {status} for {method} {url}\n{raw}")
    return json.loads(raw) if raw else {}


def put_chunk(url: str, chunk: bytes, content_md5_b64: str) -> str:
    req = urllib.request.Request(
        url,
        data=chunk,
        method="PUT",
        headers={
            "Content-MD5": content_md5_b64,
            "Content-Length": str(len(chunk)),
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=600) as resp:
            etag = resp.headers.get("ETag")
            if not etag:
                raise SystemExit(f"Chunk upload missing ETag: HTTP {resp.status}")
            return etag
    except urllib.error.HTTPError as e:
        err = e.read().decode("utf-8", errors="replace")
        raise SystemExit(f"Chunk upload failed HTTP {e.code}\n{err}") from e


def main() -> int:
    p = argparse.ArgumentParser()
    p.add_argument("--file", required=True, help="Path to package zip")
    p.add_argument("--repository", default="https://valheim.hexium.gg")
    p.add_argument("--namespace", required=True, help="Team / author_name")
    p.add_argument("--communities", default="valheim", help="Comma-separated")
    p.add_argument(
        "--categories",
        default="Quality of Life,Valheim 1.0",
        help="Comma-separated categories for the first community",
    )
    p.add_argument("--token", default=os.environ.get("HEXIUM_TOKEN", ""))
    args = p.parse_args()

    if not args.token:
        raise SystemExit("HEXIUM_TOKEN is not set")

    zip_path = Path(args.file)
    if not zip_path.is_file():
        raise SystemExit(f"Zip not found: {zip_path}")

    communities = [c.strip() for c in args.communities.split(",") if c.strip()]
    categories = [c.strip() for c in args.categories.split(",") if c.strip()]
    if not communities:
        raise SystemExit("At least one community is required")

    size = zip_path.stat().st_size
    print(f"Publishing {zip_path.name} ({size} bytes) to {args.repository}")

    init = request_json(
        "POST",
        api_url(args.repository, "api/experimental/usermedia/initiate-upload/"),
        args.token,
        {"filename": zip_path.name, "file_size_bytes": size},
        expect=201,
    )
    meta = init.get("user_media") or {}
    uuid = meta.get("uuid")
    upload_urls = init.get("upload_urls") or []
    if not uuid or not upload_urls:
        raise SystemExit(f"Bad initiate-upload response:\n{json.dumps(init, indent=2)}")

    print(f"Upload uuid={uuid} parts={len(upload_urls)}")
    completed = []
    with zip_path.open("rb") as f:
        for part in sorted(upload_urls, key=lambda x: x["part_number"]):
            offset = int(part["offset"])
            length = int(part["length"])
            f.seek(offset)
            chunk = f.read(length)
            if len(chunk) != length:
                raise SystemExit(
                    f"Short read for part {part['part_number']}: want {length}, got {len(chunk)}"
                )
            digest = hashlib.md5(chunk).digest()
            etag = put_chunk(part["url"], chunk, base64.b64encode(digest).decode("ascii"))
            completed.append({"ETag": etag, "PartNumber": int(part["part_number"])})
            print(f"  uploaded part {part['part_number']}")

    request_json(
        "POST",
        api_url(args.repository, f"api/experimental/usermedia/{uuid}/finish-upload/"),
        args.token,
        {"parts": completed},
        expect=200,
    )
    print("Finalized upload")

    primary = communities[0]
    submit_body = {
        "author_name": args.namespace,
        "categories": [],
        "communities": communities,
        "community_categories": {primary: categories},
        "has_nsfw_content": False,
        "upload_uuid": uuid,
    }
    result = request_json(
        "POST",
        api_url(args.repository, "api/experimental/submission/submit/"),
        args.token,
        submit_body,
        expect=200,
    )
    pkg = (result.get("package_version") or {})
    download = pkg.get("download_url")
    version = pkg.get("version_number")
    print(f"Published namespace={args.namespace} version={version}")
    if download:
        print(f"Download: {download}")
    else:
        print(json.dumps(result, indent=2))
    return 0


if __name__ == "__main__":
    sys.exit(main())
