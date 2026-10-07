using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using Jotunn.Configs;
using ServerSync;

namespace DrakeModsLibs.Sync;

public sealed class DrakeConfigSync
{
    readonly ConfigSync _inner;
    int _syncedEntryCount;
    bool _lockingConfigRegistered;

    DrakeConfigSync(ConfigSync inner) => _inner = inner;

    public static DrakeConfigSync Create(string modId, string displayName, string currentVersion, string minimumRequiredVersion = null)
    {
        var inner = new ConfigSync(modId)
        {
            DisplayName = displayName,
            CurrentVersion = ToSystemVersion(currentVersion),
            MinimumRequiredVersion = ToSystemVersion(minimumRequiredVersion ?? currentVersion),
        };
        return new DrakeConfigSync(inner);
    }

    /// <summary>
    /// ServerSync parses versions with <see cref="System.Version"/>, which throws on SemVer suffixes
    /// like "1.2.0-beta.3" and drops the joining peer. Strip pre-release/build metadata.
    /// </summary>
    static string ToSystemVersion(string version)
    {
        if (string.IsNullOrEmpty(version))
            return version;
        var core = version.Split('-', '+')[0];
        return System.Version.TryParse(core, out var parsed) ? parsed.ToString() : "0.0.0";
    }

    public bool IsSourceOfTruth => _inner.IsSourceOfTruth;

    public event Action<bool> SourceOfTruthChanged
    {
        add => _inner.SourceOfTruthChanged += value;
        remove => _inner.SourceOfTruthChanged -= value;
    }

    public ConfigEntry<T> BindSynced<T>(ConfigFile config, string section, string configurationManagerCategory, string key, T defaultValue, string description)
        => BindSynced(config, section, configurationManagerCategory, key, defaultValue, description, null);

    public ConfigEntry<T> BindSynced<T>(
        ConfigFile config,
        string section,
        string configurationManagerCategory,
        string key,
        T defaultValue,
        string description,
        AcceptableValueBase? acceptableValues)
    {
        var entry = config.Bind(section, key, defaultValue,
            new ConfigDescription(description, acceptableValues,
                string.IsNullOrEmpty(configurationManagerCategory) ? null : new ConfigurationManagerAttributes { Category = configurationManagerCategory }));
        var synced = _inner.AddConfigEntry(entry);
        synced.SynchronizedConfig = true;
        _syncedEntryCount++;
        return entry;
    }

    public ConfigEntry<T> BindClientOnly<T>(ConfigFile config, string section, string configurationManagerCategory, string key, T defaultValue, string description)
    {
        return config.Bind(section, key, defaultValue,
            new ConfigDescription(description, null,
                string.IsNullOrEmpty(configurationManagerCategory) ? null : new ConfigurationManagerAttributes { Category = configurationManagerCategory }));
    }

    public void AddLockingConfigEntry(ConfigEntry<bool> lockEntry)
    {
        _inner.AddLockingConfigEntry(lockEntry);
        _lockingConfigRegistered = true;
    }

    public void FinalizeBinding(ManualLogSource log, int expectedSyncedEntryCount, Func<bool> getLockSyncedConfig)
    {
        if (!_lockingConfigRegistered)
            log?.LogError("[DrakeConfigSync] LockSyncedConfig was not registered via AddLockingConfigEntry.");
        if (_syncedEntryCount != expectedSyncedEntryCount)
            log?.LogError($"[DrakeConfigSync] Expected {expectedSyncedEntryCount} synced entries, registered {_syncedEntryCount}.");
        SourceOfTruthChanged += authoritative => OnSourceOfTruthChanged(log, authoritative, getLockSyncedConfig);
        OnSourceOfTruthChanged(log, IsSourceOfTruth, getLockSyncedConfig);
    }

    static void OnSourceOfTruthChanged(ManualLogSource log, bool localIsAuthoritative, Func<bool> getLockSyncedConfig)
    {
        if (localIsAuthoritative)
        {
            if (!getLockSyncedConfig())
                log?.LogWarning("[DrakeConfigSync] LockSyncedConfig is false on the host. Non-admin clients can change synced gameplay settings.");
            return;
        }
        if (!getLockSyncedConfig())
            log?.LogInfo("[DrakeConfigSync] Connected to a host with config lock disabled; server values still apply until changed.");
    }
}
