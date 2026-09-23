using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;

namespace DrakeModsLibs.Compat;

/// <summary>
/// Per-consumer compatibility host. Keyed by owner plugin GUID so LockSmith, RenameIt,
/// and other Drake mods never share one global module list.
/// </summary>
public sealed class CompatHost
{
    private readonly object _gate = new();
    private readonly List<ICompatModule> _modules = new();
    private readonly ManualLogSource? _log;
    private Harmony? _harmony;
    private bool _initialized;

    public CompatHost(string ownerGuid, ManualLogSource? log = null)
    {
        OwnerGuid = ownerGuid ?? throw new ArgumentNullException(nameof(ownerGuid));
        _log = log;
    }

    /// <summary>Owning BepInEx plugin GUID (e.g. LockSmith / RenameIt).</summary>
    public string OwnerGuid { get; }

    /// <summary>True after <see cref="Initialize"/> finishes.</summary>
    public bool IsInitialized
    {
        get
        {
            lock (_gate)
                return _initialized;
        }
    }

    /// <summary>Raised once after <see cref="Initialize"/> (for late SoftDependency Awake).</summary>
    public event Action? Initialized;

    /// <summary>
    /// Register built-ins (and optionally early third-party modules), then apply patches
    /// in ascending priority. Call once from the consumer plugin Awake after PatchAll.
    /// </summary>
    public void Initialize(Harmony harmony)
    {
        if (harmony == null)
            return;

        Action? raise = null;
        lock (_gate)
        {
            if (_initialized)
                return;

            _harmony = harmony;
            _initialized = true;
            SortModulesUnlocked();
            ApplyAllPatchesUnlocked(harmony);
            LogActiveModulesUnlocked();
            raise = Initialized;
        }

        try
        {
            raise?.Invoke();
        }
        catch (Exception ex)
        {
            _log?.LogWarning($"CompatHost[{OwnerGuid}] Initialized subscribers failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Activate and keep a module when <see cref="ICompatModule.TryActivate"/> succeeds.
    /// Safe before or after <see cref="Initialize"/>; late registers get patches immediately.
    /// </summary>
    public bool Register(ICompatModule module)
    {
        if (module == null)
            return false;

        lock (_gate)
            return TryRegisterUnlocked(module);
    }

    /// <summary>Re-apply soft patches for all active modules (ascending priority).</summary>
    public void ApplyAllPatches(Harmony harmony)
    {
        if (harmony == null)
            return;

        lock (_gate)
            ApplyAllPatchesUnlocked(harmony);
    }

    public bool HasActiveModule(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        lock (_gate)
        {
            foreach (var module in _modules)
            {
                if (module.IsActive && string.Equals(module.Id, id, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>Snapshot of active modules (ascending priority).</summary>
    public List<ICompatModule> GetActiveModules()
    {
        lock (_gate)
        {
            var list = new List<ICompatModule>(_modules.Count);
            foreach (var module in _modules)
            {
                if (module.IsActive)
                    list.Add(module);
            }

            return list;
        }
    }

    /// <summary>Active modules assignable to <typeparamref name="T"/> (ascending priority).</summary>
    public List<T> GetActiveModulesOfType<T>() where T : class, ICompatModule
    {
        lock (_gate)
        {
            var list = new List<T>();
            foreach (var module in _modules)
            {
                if (module.IsActive && module is T typed)
                    list.Add(typed);
            }

            return list;
        }
    }

    private bool TryRegisterUnlocked(ICompatModule module)
    {
        try
        {
            if (!module.TryActivate())
            {
                _log?.LogDebug($"CompatHost[{OwnerGuid}] {module.Id} not present — skipped.");
                return false;
            }
        }
        catch (Exception ex)
        {
            _log?.LogDebug($"CompatHost[{OwnerGuid}] {module.Id} TryActivate failed: {ex.Message}");
            return false;
        }

        foreach (var existing in _modules)
        {
            if (string.Equals(existing.Id, module.Id, StringComparison.OrdinalIgnoreCase))
            {
                _log?.LogDebug($"CompatHost[{OwnerGuid}] {module.Id} already registered — ignored.");
                return existing.IsActive;
            }
        }

        _modules.Add(module);
        SortModulesUnlocked();

        if (_initialized && _harmony != null)
        {
            try
            {
                module.ApplyHarmonyPatches(_harmony);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(
                    $"CompatHost[{OwnerGuid}] {module.Id} late ApplyHarmonyPatches failed: {ex.Message}");
            }
        }

        _log?.LogInfo($"CompatHost[{OwnerGuid}] registered: {module.Id} (priority {module.Priority})");
        return true;
    }

    private void SortModulesUnlocked()
    {
        _modules.Sort((a, b) =>
        {
            var c = a.Priority.CompareTo(b.Priority);
            return c != 0 ? c : string.Compare(a.Id, b.Id, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void ApplyAllPatchesUnlocked(Harmony harmony)
    {
        foreach (var module in _modules)
        {
            if (!module.IsActive)
                continue;

            try
            {
                module.ApplyHarmonyPatches(harmony);
            }
            catch (Exception ex)
            {
                _log?.LogWarning(
                    $"CompatHost[{OwnerGuid}] {module.Id} ApplyHarmonyPatches failed: {ex.Message}");
            }
        }
    }

    private void LogActiveModulesUnlocked()
    {
        var sb = new StringBuilder();
        sb.Append("CompatHost[");
        sb.Append(OwnerGuid);
        sb.Append("] active:");
        var any = false;
        foreach (var module in _modules)
        {
            if (!module.IsActive)
                continue;
            sb.Append(' ');
            sb.Append(module.Id);
            sb.Append('[');
            sb.Append(module.Priority);
            sb.Append(']');
            any = true;
        }

        if (!any)
            sb.Append(" (none)");

        _log?.LogInfo(sb.ToString());
    }
}
