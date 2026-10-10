---
name: drakemods-compat-host
description: >-
  DrakeModsLibs per-plugin CompatHost pattern for optional soft-compat (WardIsLove,
  DevCommands, etc.). Use when adding or migrating compatibility modules in LockSmith,
  RenameIt, or other DrakeMods consumers; never invent a second global singleton.
---

# DrakeMods CompatHost

## Rule

**Scaffolding lives in DrakeModsLibs. Domain logic stays in the consumer mod.**

- Libs: `DrakeModsLibs.Compat` — `ICompatModule`, `CompatPriority`, `CompatHost`, `CompatHosts`
- Each Drake plugin owns **its own** host via `CompatHosts.GetOrCreate(ownerGuid, log)`
- Do **not** put ward access aggregation, paper take rules, or WIL Interact skips in Libs

## When working on RenameIt / LockSmith / new Drake mod

1. SoftDependency foreign mods only (`DependencyFlags.SoftDependency`) — never hard-require WardIsLove.
2. Get the consumer host: `CompatHosts.GetOrCreate(ThisPlugin.GUID, Logger)`.
3. Implement modules under `Compat/<ForeignModName>/` implementing `ICompatModule` (or a consumer domain interface that extends it).
4. `Register` present modules, then `host.Initialize(harmony)` once after `PatchAll`.
5. Third-party register API (if any) must target **this consumer’s** host / facade — SoftDepend the consumer GUID, not a vague Libs dump.

## LockSmith (reference)

- Host owner GUID: `com.drakesworkshop.locksmith`
- Domain: `IWardCompatModule` + `WardCoverageKind` + `CompatibilityManager` ward queries
- Built-ins: `Compat/Vanilla`, `Compat/WardIsLove`, `Compat/DevCommands`
- Public API: `LockSmith.API.LockSmithCompatApi`

## RenameIt

- Host owner GUID: `com.drakesworkshop.renameit` via `CompatibilityManager` → `CompatHosts.GetOrCreate`
- Domain: `IAreaCompatModule` + `AreaCoverageKind` (paper take / edit). Item-stand hover and DevCommands are `ICompatModule` only.
- Built-ins: `Compat/Vanilla`, `Compat/WardIsLove`, `Compat/ItemLibs`, `Compat/DevCommands`
- **Item stands:** `ItemStandHoverModule` postfixes WardIsLove's item-stand hover method. WIL 4.x type is `ShowWardLockOnItemStandHover`; WIL 3.5.x type is `ItemStandGetHoverTextPatch`. Both expose `Postfix(string __result, ItemStand __instance)` and return the piece name plus a red No access line. The method is static — read the stand from `__args`, not Harmony `__instance`. When `ShowItemStandItemNameWhenNoAccess` is on, replace that denied line with the Libs stand label plus No access. Do not patch stand Interact (`BlockUnpermittedItemStandUse` / old `ItemStandInteractPatch`).

See `docs/handover-wardislove-renameit.md` for the WardIsLove notes and the in-game checks still worth doing.

## Priority

| Value | Constant | Patches |
| --- | --- | --- |
| 0 | `CompatPriority.Vanilla` | First |
| 100 | `CompatPriority.BuiltIn` | After vanilla |
| 200 | `CompatPriority.ThirdParty` | After built-ins |

Access override semantics (highest covering wins) are **consumer-defined** (LockSmith does this for wards).
