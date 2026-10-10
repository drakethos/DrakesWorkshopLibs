# DrakeModsLibs

## 1.0.0-beta.1
- First 1.0 beta. Nothing removed or changed: consumers on 0.11.x keep working. Adds the message card below.
- **Fix: menus with no text.** The UI Toolkit fonts were looked up once; a window built before the game had loaded its fonts (a mod creating its window early) pinned them to nothing, so every Drake window lost its text for the session. The lookup now waits for the font, and a window built early picks its fonts up when it opens.
- **New message card: `DrakeMessage.Show(text, kind, title, seconds)`** (`DrakeModsLibs.UI`). A dark wood card near the top of the screen with a coloured edge (info, success, warning, error) that fades out on its own, in place of Valheim's yellow centre text. Drawn above every Drake window and dialog and never takes clicks. Falls back to the centre text when `UseToolkitUi` is off.
- `UkScreen.TopSorting` caps window sorting so the message layer always stays above.
- `CustomizeLibsAPI.ShowHudMessage` now shows on the same card (its `type` argument is ignored). Existing callers keep compiling.
- New API only, nothing removed.

## 0.11.2
- **Fix: the 0.10.1 VIP check was inverted.** `IsSourceOfTruth` is true while the local config is in charge and turns false once the host's synced config arrives. 0.10.1 trusted a remote client's `VipList` only while it was still true, so an unsynced client could still grant itself VIP, and real VIPs on a remote or dedicated server lost VIP once synced. A remote client now trusts `VipList` only after the host's config has taken over. Host and offline play are unchanged.
- No API changes.

## 0.11.1
- **Fix: synced settings changed while the game runs never reached players on Valheim 1.0.** The bundled ServerSync was built when `ZRoutedRpc.Everybody` was a static field; Valheim 1.0 made it a constant, so ServerSync's broadcast threw `MissingFieldException` on every synced setting change (an admin editing config, or a mod migrating a value at startup). The bundled ServerSync is patched to use the constant, as a rebuild against 1.0 would (DrakesWorkshop `scripts/Patch-ServerSyncEverybody.ps1`).
- No API changes. Consumers pinned to 0.10.x or 0.11.0 keep working and get the fix by updating Libs.

## 0.11.0
- **Custom models from Drakes Asset Forge** (`DrakeModsLibs.Forge`):
  - `ForgeLook.Model(assembly, prefab, file)`: shows a .glb model embedded in the calling mod's DLL (or beside it) instead of the prefab's own meshes, like Forge's `look.mesh.file`. Each submesh becomes a renderer named `glb{i}_{mesh name}`, with a copy of the base's material named after the .glb material, so `ForgeLook.Material` can restyle it by that name.
  - `ForgeLook.CollisionBox(prefab, center, size)`: one box collider instead of the prefab's solid colliders (Forge's `look.collision` box). The originals are switched off; triggers stay.
  - Drakes Asset Forge's plain C# export with "Use DrakeModsLibs" now writes these calls. Used by DrakesItemShop's display cases.
- New API only, nothing removed. Consumers pinned to 0.10.x keep working.

## 0.10.2
- **Recolor skin-tight armor.** Chest and leg armor that Valheim paints onto the body (bear, wolf, troll leather, ...) now takes the model tint. Tinted copies of `_ChestTex` / `_LegsTex` are made once per texture and color, cached, and freed when unused. Bare skin is never tinted.
- **Tint brightness boost** (`ItemLookService.MaxTintBoost`, `BoostOf`): tints can exceed white up to 4x to lighten dark textures. Stored as `RRGGBB*2.50` and synced in spare bits of the per-slot tint; older versions read just the base color.
- **`DrakeSlider`** (`DrakeModsLibs.UI`): labeled float slider row, layout-driven.
- New API only, nothing removed. Consumers pinned to 0.10.x keep working.

## 0.10.1
- **Fix: a player could gain VIP by editing their local config on a self-hosted or remote server.** `DrakePermissionProfile` read its `VipList` straight from the local file. A remote client now only trusts `VipList` once the host's synced config is the source of truth, the same rule RenameIt uses. Host and offline play are unchanged.
- No API changes. Consumers pinned to 0.10.0 keep working.

## 0.10.0
- **UI layout core** (`DrakeModsLibs.UI`): wood panels size to their content instead of fixed heights.
  - `DrakePanelOptions` (new, optional constructor argument on `DrakeConfirmPanel` and `DrakeWoodActionMenu`): `Width`, `MinHeight`, `MaxHeight`, `ButtonWidth`, `ButtonHeight`, `RowGap`. Defaults match the old sizes.
  - `DrakeConfirmPanel`: body text is measured. The panel grows with it up to `MaxHeight`, then the text scrolls. Fixes the cut-off "warning is too long to read" popup.
  - `DrakeWoodActionMenu`: rows sit in a scroll area between the header and Cancel. Long lists scroll instead of running into Cancel.
  - Public signatures are unchanged. No consumer change needed.
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
