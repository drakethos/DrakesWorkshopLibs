using System;
using BepInEx.Configuration;

namespace DrakeModsLibs.Integration;

/// <summary>
/// Umbrella integration knobs owned by DrakeModsLibs (cross-mod orchestration).
/// Feature mods keep gameplay config; libs coordinates tabs / open chord / overrides.
/// </summary>
public static class DrakeIntegrationConfig
{
    const string Section = "Integration";

    static ConfigEntry<string>? _inventoryOpenModifier;
    static ConfigEntry<string>? _tabPriorityOverrides;
    static ConfigEntry<string>? _forceDefaultTabId;
    static ConfigEntry<bool>? _disableClaimDefault;

    /// <summary>Shared inventory menu chord when tab host is wired (e.g. Shift).</summary>
    public static string InventoryOpenModifier =>
        _inventoryOpenModifier?.Value?.Trim() ?? "Shift";

    /// <summary>When set and available, forces that tab id as default open.</summary>
    public static string? ForceDefaultTabId
    {
        get
        {
            var v = _forceDefaultTabId?.Value?.Trim();
            return string.IsNullOrEmpty(v) ? null : v;
        }
    }

    public static bool DisableClaimDefault => _disableClaimDefault?.Value ?? false;

    public static void Bind(ConfigFile config)
    {
        if (config == null || _inventoryOpenModifier != null)
            return;

        _inventoryOpenModifier = config.Bind(
            Section,
            "InventoryOpenModifier",
            "Shift",
            "Shared inventory menu modifier chord for the libs tab host (when wired). Examples: Shift, Ctrl, Alt, None.");

        _tabPriorityOverrides = config.Bind(
            Section,
            "TabPriorityOverrides",
            "",
            "Optional tab priority overrides: id=priority;id2=priority (e.g. renameit=50;locksmith.keypass=250). Empty = use Register baselines.");

        _forceDefaultTabId = config.Bind(
            Section,
            "ForceDefaultTabId",
            "",
            "If set to a registered tab id that is available for the item, that tab opens first. Empty = ClaimDefault / priority.");

        _disableClaimDefault = config.Bind(
            Section,
            "DisableClaimDefault",
            false,
            "When true, ignore ClaimDefault predicates and pick the highest-priority available tab.");
    }

    /// <summary>Parse TabPriorityOverrides for a tab id.</summary>
    public static bool TryGetPriorityOverride(string tabId, out int priority)
    {
        priority = 0;
        var raw = _tabPriorityOverrides?.Value;
        if (string.IsNullOrWhiteSpace(raw) || string.IsNullOrEmpty(tabId))
            return false;

        foreach (var part in raw!.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split(new[] { '=' }, 2);
            if (kv.Length != 2)
                continue;
            if (!string.Equals(kv[0].Trim(), tabId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!int.TryParse(kv[1].Trim(), out priority))
                return false;
            return true;
        }

        return false;
    }
}
