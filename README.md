<img width="256" height="256" alt="DrakesWorkshopLibs icon" src="icon.png" />

# DrakesWorkshopLibs

**DrakesWorkshop** — shared customization library for Drake mods (display patches, tags, APIs, server config sync).

## Server config sync (DrakeConfigSync)

**Only DrakesWorkshopLibs** references and ILRepack-embeds [ServerSync](https://github.com/MSchmoecker/ServerSync). Consumer mods must not add a ServerSync Thunderstore dependency or ILRepack step.

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

Built-in mod ids: `DrakesRenameIt`, `DrakesWorkshopLibs`, `DrakesQuestItems`, `DrakesItemShop`.

Subscribe to `CustomizationEvents.OnItemNameChanged` for rename logging; use `DrakeCustomDataKeys.Rename` with `GetCustomDataValue`.
