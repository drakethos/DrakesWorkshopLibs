# DrakeModsLibs

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
