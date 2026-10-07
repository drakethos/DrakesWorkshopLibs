using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using DrakeModsLibs.Sync;
using DrakeModsLibs.Tags;

namespace DrakeModsLibs.Permissions;

/// <summary>
/// Who counts as admin/VIP and which items are excluded — for one feature mod.
/// <para>
/// Each mod owns a profile. A profile can point at another mod's profile as its source of truth
/// (e.g. ReskinIt's <c>AdminSource = RenameIt</c>), so a server keeps one VIP list for every Drake mod,
/// or keeps them separate. Admin settings and exclusions link independently; exclusions can also <c>Merge</c>
/// (blocked if either mod blocks it).
/// </para>
/// Two kinds: config-bound (<see cref="Bind"/>, full settings in the owning mod's synced cfg) and
/// delegate-backed (<see cref="DrakePermissionProfiles.RegisterSource"/>, a mod that keeps its own logic and only
/// exposes it as a link target — RenameIt today).
/// </summary>
public sealed class DrakePermissionProfile
{
    public const string SourceOwn = "Own";
    public const string ExclusionMerge = "Merge";

    readonly Func<Player?, bool>? _elevatedOverride;
    readonly Func<ItemDrop.ItemData, bool>? _excludedOverride;

    ConfigEntry<string>? _adminSource;
    ConfigEntry<string>? _exclusionSource;
    ConfigEntry<bool>? _allowAdminOverride;
    ConfigEntry<bool>? _vipOnlyOverride;
    ConfigEntry<string>? _vipList;
    ConfigEntry<string>? _excludedNames;
    ConfigEntry<string>? _excludedCategory;
    ConfigEntry<string>? _allowlist;

    string? _vipRaw;
    HashSet<string> _vips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> _warned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    ManualLogSource? _log;

    internal DrakePermissionProfile(string id, string displayName,
        Func<Player?, bool>? elevated = null, Func<ItemDrop.ItemData, bool>? excluded = null)
    {
        Id = id;
        DisplayName = displayName;
        _elevatedOverride = elevated;
        _excludedOverride = excluded;
    }

    /// <summary>Lowercase registry key (e.g. <c>renameit</c>).</summary>
    public string Id { get; }

    /// <summary>Name used in config values (e.g. <c>RenameIt</c>).</summary>
    public string DisplayName { get; }

    // ── Binding ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Bind the admin + exclusion settings into the owning mod's synced config. Adds
    /// <paramref name="syncedEntryCount"/> to account for in <see cref="DrakeConfigSync.FinalizeBinding"/>.
    /// </summary>
    /// <param name="linkTargets">Profiles this one may link to, by <see cref="DisplayName"/> (e.g. <c>"RenameIt"</c>).</param>
    /// <param name="defaultAdminSource"><see cref="SourceOwn"/> or one of <paramref name="linkTargets"/>.</param>
    public void Bind(ConfigFile config, DrakeConfigSync sync, ManualLogSource log,
        string adminSection, string exclusionSection, string[] linkTargets, string defaultAdminSource,
        out int syncedEntryCount)
    {
        _log = log;
        var sources = new[] { SourceOwn }.Concat(linkTargets).ToArray();
        var exclusionSources = sources.Concat(linkTargets.Length > 0 ? new[] { ExclusionMerge } : Array.Empty<string>()).ToArray();
        var linkHelp = linkTargets.Length > 0 ? string.Join(" / ", linkTargets) : "another Drake mod";

        _adminSource = sync.BindSynced(config, adminSection, adminSection, "AdminSource", defaultAdminSource,
            $"Where {DisplayName} gets its admin settings (AllowAdminOverride, VipOnlyOverride, VipList).\n" +
            $"Own = use the three settings below.\n" +
            $"{linkHelp} = use that mod's admin settings instead (one VIP list for both mods); the settings below are ignored.\n" +
            "If the linked mod is not installed, Own is used and a warning is logged.",
            new AcceptableValueList<string>(sources));
        _allowAdminOverride = sync.BindSynced(config, adminSection, adminSection, "AllowAdminOverride", true,
            "If on, server admins and VIPs are 'elevated': they skip the exclusions below and pass any admins-and-VIPs-only access setting. " +
            "Does NOT grant Valheim admin commands. Ignored when AdminSource links to another mod.");
        _vipOnlyOverride = sync.BindSynced(config, adminSection, adminSection, "VipOnlyOverride", false,
            "If on, only people on VipList are elevated — Valheim server admins are not. Useful for testing VIP behavior. " +
            "Ignored when AdminSource links to another mod.");
        _vipList = sync.BindSynced(config, adminSection, adminSection, "VipList", "",
            "Comma- or semicolon-separated character names and/or platform IDs (Steam ID, same IDs as adminlist.txt). " +
            "Grants mod elevation only, not Valheim admin. Ignored when AdminSource links to another mod.");

        _exclusionSource = sync.BindSynced(config, exclusionSection, exclusionSection, "ExclusionSource", SourceOwn,
            $"Which item exclusions {DisplayName} uses.\n" +
            "Own = the lists below.\n" +
            (linkTargets.Length > 0
                ? $"{linkHelp} = that mod's exclusions exactly as it applies them (the lists below are ignored).\n" +
                  $"Merge = an item is blocked if EITHER the lists below or {linkHelp} block it (use this to be stricter than {linkHelp}).\n"
                : "") +
            "Elevated players (see AllowAdminOverride) are never blocked by exclusions.",
            new AcceptableValueList<string>(exclusionSources));
        _excludedNames = sync.BindSynced(config, exclusionSection, exclusionSection, "ExcludedNames", "",
            "Comma-separated items non-elevated players cannot change. Each entry may be a token ($item_axe_stone), " +
            "a prefab/spawn name (AxeStone), or the English display name (Stone axe).");
        _excludedCategory = sync.BindSynced(config, exclusionSection, exclusionSection, "ExcludedCategory", "",
            "Comma-separated categories non-elevated players cannot change. Aliases: Weapons, Armor, Tools, Ranged, Melee, " +
            "Shields, Ammo, Fish. Also any skill type (Swords, Bows, ...) or item type (Trophy, Material, Consumable, ...).");
        _allowlist = sync.BindSynced(config, exclusionSection, exclusionSection, "Allowlist", "",
            "Comma-separated items (same format as ExcludedNames) that stay allowed even when ExcludedNames or ExcludedCategory would block them.");

        syncedEntryCount = 8;
    }

    // ── Queries ─────────────────────────────────────────────────────────────

    /// <summary>True when <paramref name="player"/> is admin/VIP for this mod (follows <c>AdminSource</c>).</summary>
    public bool IsElevated(Player? player)
    {
        var source = ResolveLink(_adminSource?.Value);
        return source != null ? source.OwnElevated(player) : OwnElevated(player);
    }

    /// <summary>
    /// True when exclusions block <paramref name="item"/> for <paramref name="player"/> (follows <c>ExclusionSource</c>).
    /// Elevated players are never blocked.
    /// </summary>
    public bool IsExcluded(ItemDrop.ItemData? item, Player? player)
    {
        if (item?.m_shared == null || IsElevated(player))
            return false;

        var mode = _exclusionSource?.Value ?? SourceOwn;
        if (string.Equals(mode, ExclusionMerge, StringComparison.OrdinalIgnoreCase))
        {
            if (OwnExcluded(item))
                return true;
            foreach (var target in LinkTargetsOf(_exclusionSource))
            {
                if (target.OwnExcluded(item))
                    return true;
            }
            return false;
        }

        var source = ResolveLink(mode);
        return source != null ? source.OwnExcluded(item) : OwnExcluded(item);
    }

    bool OwnElevated(Player? player)
    {
        if (_elevatedOverride != null)
            return SafeInvoke(_elevatedOverride, player);
        if (player == null || _allowAdminOverride?.Value != true)
            return false;
        if (IsOnVipList(player))
            return true;
        return _vipOnlyOverride?.Value != true && DrakePlayerIdentity.IsValheimAdmin(player);
    }

    bool OwnExcluded(ItemDrop.ItemData item)
    {
        if (_excludedOverride != null)
            return SafeInvoke(_excludedOverride, item);
        if (SplitList(_allowlist?.Value).Any(t => ItemNameMatch.Matches(item, t)))
            return false;
        return SplitList(_excludedNames?.Value).Any(t => ItemNameMatch.Matches(item, t))
               || SplitList(_excludedCategory?.Value).Any(t => DrakeItemCategory.Matches(item, t));
    }

    bool IsOnVipList(Player player)
    {
        var raw = _vipList?.Value ?? "";
        if (!string.Equals(raw, _vipRaw, StringComparison.Ordinal))
        {
            _vipRaw = raw;
            _vips = new HashSet<string>(raw.Split(',', ';').Select(s => s.Trim()).Where(s => s.Length > 0),
                StringComparer.OrdinalIgnoreCase);
        }
        return _vips.Count > 0 && DrakePlayerIdentity.Keys(player).Any(_vips.Contains);
    }

    DrakePermissionProfile? ResolveLink(string? value)
    {
        if (string.IsNullOrEmpty(value) || string.Equals(value, SourceOwn, StringComparison.OrdinalIgnoreCase))
            return null;
        var target = DrakePermissionProfiles.Get(value!);
        if (target == null || target == this)
        {
            if (_warned.Add(value!))
                _log?.LogWarning($"[{DisplayName}] linked to '{value}', but that mod is not installed — using {DisplayName}'s own settings.");
            return null;
        }
        return target;
    }

    IEnumerable<DrakePermissionProfile> LinkTargetsOf(ConfigEntry<string>? entry)
    {
        if (entry?.Description?.AcceptableValues is not AcceptableValueList<string> list)
            yield break;
        foreach (var name in list.AcceptableValues)
        {
            if (string.Equals(name, SourceOwn, StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, ExclusionMerge, StringComparison.OrdinalIgnoreCase))
                continue;
            var target = ResolveLink(name);
            if (target != null)
                yield return target;
        }
    }

    static IEnumerable<string> SplitList(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Enumerable.Empty<string>()
            : csv!.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

    static bool SafeInvoke<T>(Func<T, bool> f, T arg)
    {
        try
        {
            return f(arg);
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>Registry of permission profiles by id, so mods can link to each other without hard references.</summary>
public static class DrakePermissionProfiles
{
    static readonly Dictionary<string, DrakePermissionProfile> Profiles =
        new Dictionary<string, DrakePermissionProfile>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Create (or replace) a config-bound profile; call <see cref="DrakePermissionProfile.Bind"/> next.</summary>
    public static DrakePermissionProfile Create(string displayName)
    {
        var profile = new DrakePermissionProfile(displayName.ToLowerInvariant(), displayName);
        Profiles[profile.Id] = profile;
        return profile;
    }

    /// <summary>
    /// Expose a mod's existing admin/exclusion logic as a link target without moving its config
    /// (other profiles can then use <c>AdminSource = displayName</c>).
    /// </summary>
    public static DrakePermissionProfile RegisterSource(string displayName,
        Func<Player?, bool> isElevated, Func<ItemDrop.ItemData, bool> isExcluded)
    {
        var profile = new DrakePermissionProfile(displayName.ToLowerInvariant(), displayName, isElevated, isExcluded);
        Profiles[profile.Id] = profile;
        return profile;
    }

    public static DrakePermissionProfile? Get(string idOrDisplayName) =>
        Profiles.TryGetValue(idOrDisplayName, out var p) ? p : null;
}
