# DrakeModsLibs

## 0.10.0
- **Forge helpers** (`DrakeModsLibs.Forge`): what Drakes Asset Forge's plain C# export calls to dress items and pieces, as plain statics (nothing runs until a mod calls them).
  - `ForgeTextures.Load/Sprite(assembly, file)`: images from inside the calling mod's DLL (embedded resources), falling back to a file with that name anywhere under the mod's folder, so Hexium/Gale flattening folders can't break them. Loaded once, cached.
  - `ForgeSprites.Add`: flat images (cut-out quads, one or two sided, shared mesh and material per image).
  - `ForgeLook`: materials (tint, textures, glow), body armour material, borrowed meshes, hide mesh, icons, snap points, add/remove components, fire and light colours, glow light, station and piece-table names.
  - Kitbashing: `ForgeLook.AddPart` (another prefab's meshes on this one, restyle with `Material`) and `ForgeLook.Scale`.
- **Removed `ArtItemLoader` / `ArtItemContext`** (the Unity asset-bundle art loader, `DrakeModsLibs.Art`). Items are now made with Drakes Asset Forge and the Forge helpers above. Mods built against it must stay pinned to Libs 0.9.x; LockSmith's key is temporarily removed and no longer uses it.

## 0.9.12
- **Fix: `DrakeWoodActionMenu` showed no buttons after logout / character swap.** The GUI root is rebuilt then, and the menu kept references to the destroyed buttons, so the new panel never made its own. It now starts a fresh button list whenever it rebuilds the panel. This affected LockSmith's Lock menu and any other mod's action menu.
- **Fix: `DrakeWoodActionMenu` with many buttons ran past the panel and into Close.** Buttons started from a fixed spot near the middle no matter how tall the panel was. The panel now sizes to its content (up to 640px) and stacks buttons down from just under the title/subtitle, with Close kept in its own footer.

## 0.9.11
- **Fix: reskinned equipped models flickered, vanished or shook the camera** (e.g. a hoe made to look like a hammer). The game's equipment state now always keeps the real item; the owner publishes real -> look prefab hashes for every worn item on the player ZDO (any slot, hidden back items, and slots added by other mods) and every client swaps the hash only at the final attach (`VisEquipment.Set*Equipped`). Players without the target model keep the original.

## 0.9.10
- **Item looks** (`ItemLookService`, `CustomizeLibsAPI.Get/SetModelOverride`, `Get/SetIconTint`, `Get/SetModelTint`, `CanReskinModel`, `CanRecolor`): equipped-model swap (same item type) via VisEquipment hash prefixes, model tint synced through the player ZDO, icon tint in inventory grids and hotbar. New ops `ReskinModel` / `ReskinColor`; keys `Drake_ModelOverride` / `Drake_IconTint` / `Drake_ModelTint` (part of stack identity). Game members bound by name at runtime (1.0 signatures differ from CI stubs).
- **Progression helpers** (`DrakeModsLibs.Progression`): `DrakeProgression.RequiredKey` (boss key per item: anchored materials + recipe ingredients, with aliases/overrides), `IsKeyUnlocked` (global key or player key). `DrakeDiscovery`: `HasSeen` (vanilla known materials), `HasCrafted` / craft tracking (saved per character as `Drake_Crafted`), `IsCraftable`.
- **Permission profiles** (`DrakeModsLibs.Permissions`): `DrakePermissionProfiles.Create` + `DrakePermissionProfile.Bind` give a mod synced admin/VIP + exclusion settings with `AdminSource` / `ExclusionSource` links to another mod's profile (`Own` / `<Mod>` / `Merge`). `RegisterSource` exposes an existing mod's logic as a link target without moving its config. Helpers: `DrakePlayerIdentity` (Valheim admin + VIP keys), `DrakeItemCategory` (exclusion category tokens, `RegisterAlias`).
- **Icon overrides (ReskinIt):** `Drake_IconOverride` custom-data key stores a source `PrefabName[#variant]`; a shared `ItemData.GetIcon` postfix shows that icon in inventory, hotbar, tooltips, and HUD messages. API: `CustomizeLibsAPI.GetIconOverride` / `SetIconOverride` / `ClearIconOverride` / `HasIconOverride` / `CanReskinIcon` / `GetIconCatalog` (`IconCatalog`, `IconCategory`).
- **`DrakeTabRegistration.DefaultReskinPriority`** (50) / **`ReskinItTabId`** — Reskin ranks below Rename and Paper, so it never takes the default tab.
- **`CustomizeLibsAPI.ShowHudMessage`** — public Valheim 1.0-safe `Character.Message` (feature mods must not call `Message` directly when built against CI stubs).
- **`CustomizeOperation.ReskinIcon`** (outside `AllEdits`); quest-item and immutable tags block it. Icon override is part of stack identity, so reskinned stacks don't merge with plain ones.

## 0.9.9
- **Fix: multiplayer join with pre-release mod versions.** `DrakeConfigSync` now strips SemVer suffixes (`-beta.3`, `+build`) before handing versions to ServerSync, whose `System.Version` parse threw in `VersionCheck.RPC_PeerInfo` and left clients unable to join (e.g. DrakesRenameit 1.2.0-beta.3).
- Dependencies: BepInExPack_Valheim 5.4.2351, Jotunn 2.30.2.

## 0.9.8
- **`DrakeConfirmPanel`:** nested Show over an already-blocked wood panel no longer calls `EnsureUnblocked` on Close (Yes/No/Esc). Fixes cursor disappearing / camera look while a parent editor is still open.

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
