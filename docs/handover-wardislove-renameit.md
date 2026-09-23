# Handover: WardIsLove → RenameIt

**Status:** LockSmith and RenameIt both use Libs `CompatHost`. RenameIt item-stand names are `Compat/ItemLibs/ItemStandHoverModule`. WIL 4.0.4 uses `ShowWardLockOnItemStandHover`; WIL 3.5.x used `ItemStandGetHoverTextPatch`. The module postfixes whichever is loaded.

## Context

WardIsLove replaces vanilla `PrivateArea` with `WardMonoscript`. Under WIL-only bases, `PrivateArea.m_allAreas` is empty, so `PrivateArea.CheckAccess` returns true (falsely “open”). WIL separately:

- Blocks `ItemStand.Interact` via `ItemStandInteractPatch` (RenameIt does not skip this)
- Postfixes `ItemStand.GetHoverText` via `ShowWardLockOnItemStandHover` (4.0.4) or `ItemStandGetHoverTextPatch` (3.5.x). The return is the piece `m_name` plus a red No access line, which drops the attached item's custom name.

## RenameIt modules

- `Compat/CompatibilityManager` — per-plugin host, `CheckAccess` / `IsInsideEnabledWard`
- `Compat/Vanilla` — vanilla `PrivateArea` (priority 0)
- `Compat/WardIsLove` — `WardMonoscript` access (does not patch hover)
- `Compat/ItemLibs` — postfixes the WIL hover method when `ShowItemStandItemNameWhenNoAccess` is on; label from `CustomizeLibsAPI.GetItemStandHoverLabel`; Interact stays blocked
- `Compat/DevCommands` — detection only; nocost remains `Player.NoCostCheat`
- Paper wall take/edit and `PaperItemStandPatches` call `CompatibilityManager.CheckAccess`

## Still verify in game

- Warded item stand with the show-name option on: custom name + No access, Interact still blocked
- Same option off: WardIsLove No access only
- Non-public wall paper cannot be taken inside a WardIsLove ward

## LockSmith ownership (not RenameIt)

- Door/chest public/guest bypass of WIL `BlockUnpermitted*`  
- `RequireActiveWard` / permission via `IWardCompatModule`  
- Third-party ward register API: `LockSmithCompatApi`

## SoftDependency reminder

SoftDependency = optional load order. Missing WIL must not break RenameIt or LockSmith.
