# DrakeModsLibs

## 0.9.7
- **`CompatHost`** / **`CompatHosts`** / **`ICompatModule`** / **`CompatPriority`** — per-plugin soft-compat scaffolding (no hard deps). Consumers keep domain modules; see `.cursor/skills/drakemods-compat-host` and `docs/handover-wardislove-renameit.md`.
- **`CustomizeLibsAPI.GetItemStandHoverLabel`** — the same stand label the warded hover postfix uses, so a consumer can keep that name when another mod replaces item-stand hover text.

## 0.9.6
- **`DrakeStackForce`** on `IStackMergePolicy.GetStackForce` — per-item `None` / `ByIdentity` / `Never` merge override (ignores SeparateStacks; no TagBypass). Consumers that implement the policy must add this method.
- **`DrakeNumericStepper.SetRange`** / **`SetInteractable`** so spin boxes can change min/max and disable in place.

## 0.9.5
- Shared **`DrakeNumericStepper`** (− / field / +) for integer spin boxes (Jotunn has no NumericUpDown).
- Shared **`DrakeToggleLayout`** for label-left / box-right wood toggle rows.
- Local PackageMod can stage Pfhoenix CI refs and deploy via Thunderstore zip extract (same surface as store builds).

## 0.9.4
- Soft `IsRenameInventorySuppressed` respects admin/VIP `TagBypass` (hard suppress still always hides Rename inventory UI).
- `HardNoDescription` / `HardNoCraftedByEdit` no longer set `suppressRenameInventoryUi` — they block those ops only, so Locksmith keys can show Lock|Rename tabs for bypass admins.
- Shared Drake wood UI shell (RenameIt-standard): `DrakeWoodActionMenu`, `DrakeConfirmPanel` (300×178), `DrakeTextPromptPanel`, `DrakeGuiInput`, `DrakeButtonSfx`.
- **`DrakeTabHost`**: Craft|Upgrade-style top-right tabs; `Register` / `ClaimDefault` / priority (Rename baseline low); `Hide` / `NotifyFeatureClosed`; `CloseSilent` on action menus. Missing mod = no tab.
- Inventory context chord: Libs `Integration.InventoryOpenModifier` → open when any usable tab exists; dynamic interact hint (`getHintPhrase` / multi-mode customize token).
- **`DrakeIntegrationConfig`**: umbrella Integration section — shared open modifier, tab priority overrides, force default tab, disable ClaimDefault.
- Tag / edit gatekeeper: rule-driven `IsRenameInventorySuppressed`; hard locks; deferred edit authority; soft stamp helpers; prefab/family exclusions and deferrals.

## 0.9.3
- Fix Valheim 1.0 pickup/drop HUD: `Character.Message` / `Player.Message` take a fifth `bool log` argument. Old 4-arg calls threw `MissingMethodException` on every pickup.

## 0.9.2
- **ArtItemLoader** — register Asset Forge items from `Assets/Items` beside a consumer plugin (optional customize hook).
- Folder packs: shared `keys.bundle` + per-item JSON/PNG (e.g. LockSmith `masterkey`), with process-lifetime AssetBundle path cache.
- Legacy layout still supported: `Assets/Items/<id>/item.json` + `art.bundle`.
- `ArtItemContext.UseDonorVisual` keeps the donor mesh and only retints materials when requested.

## 0.9.1
- Item-stand hover labels load the attached item from the stand ZDO (durability and custom data) instead of the prefab, so names stay accurate without mutating ObjectDB.
- `CustomizeLibsAPI.RefreshItemStandDisplayNames` recomputes stand labels immediately after name-modifier config changes.
- `Drake_PublicRewrite` custom-data key for allowing non-owners to rewrite name/description.
- `DrakeConfigSync.BindSynced` accepts Configuration Manager acceptable values.

## 0.9.0
- First standalone Thunderstore-oriented package identity: **DrakeModsLibs** (`com.drakemods.libs`).
- Shared library split out for **DrakesRenameit** and future DrakeMods consumers (no full suite release planned at this time).
- **Valheim 1.0 compatibility** — display / item-stand Harmony paths updated for the 1.0 game API (fixes broken labels / stand visuals after the update).
- **DrakeConfigSync** — wraps ServerSync `ConfigSync` for consumers (`BindSynced`, `BindClientOnly`, `AddLockingConfigEntry`, `FinalizeBinding`).
- **CustomizeLibsAPI.CreateConfigSync** — public factory for other Drake mods.
- **ILRepack** — ServerSync embedded/internalized into `DrakeModsLibs.dll` (only this mod ships merged ServerSync).

## 0.3.0
- DrakeConfigSync + CreateConfigSync + ILRepack ServerSync (pre-identity rename).

## 0.2.1
- Display Harmony patches, name resolution pipeline, tag gatekeeper, menu binding registry, shared customization API.

## 0.1.0
- Phase 1 scaffold: project template, CI, stub BepInEx plugin.
