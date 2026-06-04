# DrakesWorkshopLibs

## 0.3.0
- **DrakeConfigSync** — wraps ServerSync `ConfigSync` for suite consumers (`BindSynced`, `BindClientOnly`, `AddLockingConfigEntry`, `FinalizeBinding`).
- **CustomizeLibsAPI.CreateConfigSync** — public factory for other Drake mods.
- **ILRepack** — ServerSync is embedded/internalized into `DrakesWorkshopLibs.dll` at build time (only this mod ships merged ServerSync).

## 0.2.1
- Display Harmony patches, name resolution pipeline, tag gatekeeper, menu binding registry, shared customization API.

## 0.1.0
- Phase 1 scaffold: project template, CI, stub BepInEx plugin.
