<img width="256" height="256" alt="DrakeModsLibs icon" src="icon.png" />

# DrakeModsLibs

Shared library for **DrakeMods** Valheim mods. It owns the display Harmony patches, item custom-data helpers, tag gatekeeping, menu binding registry, and **server config sync** so feature mods stay smaller and stay compatible when the game updates.

**DrakeModsLibs** is published on its own so other DrakeMods (starting with **DrakesRenameit**) can depend on one common package. More customization-oriented mods may use it later; there is **no full suite planned for release right now** — priorities and a redesign pass come first. **Reskin** is something we still want to look into when time allows.

## Valheim 1.0

This release targets **Valheim 1.0** API changes (notably item-stand visual updates and related display paths). Install the matching **DrakeModsLibs** build with your feature mods so labels and hover text keep working after the 1.0 update.

## Required for

- **LockSmith** (ArtItemLoader for the Locksmith Key pack)
- **DrakesRenameit** (and any future DrakeMods that share display / config sync)

## Art items (ArtItemLoader)

Consumer mods call `ArtItemLoader.Register(log, pluginDir, config, customize)` from `Awake`. Layouts:

- **Folder pack:** `Assets/Items/keys/keys.bundle` + `masterkey.json` / `masterkey.png` (shared bundle, many items)
- **Legacy:** `Assets/Items/<id>/item.json` + `art.bundle`

Optional `customize` can set name, recipe, scale, materials, or `UseDonorVisual` (keep CryptKey mesh, retint only).

## Server config sync (DrakeConfigSync)

**Only DrakeModsLibs** references and ILRepack-embeds [ServerSync](https://github.com/MSchmoecker/ServerSync). Consumer mods must not add a ServerSync Thunderstore dependency or ILRepack step.

At startup, create sync via the API:

```csharp
var sync = CustomizeLibsAPI.CreateConfigSync(modId, displayName, version);
var entry = sync.BindSynced(config, section, configurationManagerCategory, key, defaultValue, description);
sync.AddLockingConfigEntry(lockEntry);
sync.FinalizeBinding(log, expectedSyncedEntryCount, () => lockEntry.Value);
```

Use `BindClientOnly` for per-client settings (never registered with ServerSync). `IsSourceOfTruth` / `SourceOfTruthChanged` behave like the underlying ServerSync instance.

## Custom data API (other mods)

Read and dump `ItemDrop.ItemData.m_customData` through `CustomizeLibsAPI`:

- `GetCustomDataValue(item, key)` — display value (tags → `true`/null, text → stored string)
- `GetCustomDataRaw(item, key)` — raw dictionary value
- `DumpItemCustomData(item, options)` — all keys, one mod, one key, JSON or neat text
- `DumpAllDrakeCustomData(item)` / `DumpItemCustomDataForMod(item, modId)`
- `RegisterModCustomDataFields(modId, fields)` — register your mod’s keys at startup

Built-in mod ids: `DrakesRenameIt`, `DrakeModsLibs`, `DrakesQuestItems`, `DrakesItemShop`.

Subscribe to `CustomizationEvents.OnItemNameChanged` for rename logging; use `DrakeCustomDataKeys.Rename` with `GetCustomDataValue`.

## Install

1. Install **BepInEx** and **Jotunn** (see Thunderstore dependencies).
2. Install **DrakeModsLibs**.
3. Install feature mods that depend on it (e.g. **DrakesRenameit**).

Team: **DrakeMods** · Contact: Drakethos (Discord / email in the RenameIt page).

## Publishing (GitHub Actions)

Pushing a tag `v{Version}` that matches `<Version>` in `mod.package.props` runs [.github/workflows/release.yml](.github/workflows/release.yml):

1. Build the Thunderstore zip and create a GitHub Release with the zip attached  
2. Publish that zip to **Thunderstore** (`DrakeMods-DrakeModsLibs`)  
3. Publish the same zip to **Hexium** (`valheim.hexium.gg`) after Thunderstore succeeds  

Repo secrets required:

| Secret | Source |
|--------|--------|
| `THUNDERSTORE_TOKEN` | Thunderstore team service account for **DrakeMods** |
| `HEXIUM_TOKEN` | Hexium team API token for **DrakeMods** ([team settings](https://hexium.gg/faq)) |
