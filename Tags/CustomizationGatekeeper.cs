using System;
using System.Collections.Generic;
using DrakeModsLibs.API;
using DrakeModsLibs.Data;

namespace DrakeModsLibs.Tags;

public delegate bool CustomizeEditValidator(ItemDrop.ItemData? item, Player? player);

public static class CustomizationGatekeeper
{
    sealed class DeferredAuthorityRegistration
    {
        public CustomizeOperation Operations;
        public DeferredEditHandler Handler = null!;
    }

    static readonly Dictionary<CustomizeOperation, List<CustomizeEditValidator>> Validators = new();
    static readonly List<TagBlockRule> TagBlockRules = new();
    static readonly List<ItemExclusionRule> ItemExclusions = new();
    static readonly Dictionary<string, DeferredAuthorityRegistration> DeferredAuthorities =
        new(StringComparer.OrdinalIgnoreCase);
    static bool _defaultTagRulesRegistered;

    /// <summary>
    /// When this returns true for a player, soft tag/exclusion blocks are skipped —
    /// including soft <see cref="IsRenameInventorySuppressed"/> (inventory tab/tooltip handoff).
    /// RenameIt registers admin/VIP override here. Does <b>not</b> affect hard locks or deferred authority.
    /// </summary>
    public static Func<Player?, bool>? TagBypass { get; set; }

    static void EnsureDefaultTagRules()
    {
        if (_defaultTagRulesRegistered)
            return;
        _defaultTagRulesRegistered = true;
        RegisterTagBlockRule(DrakeCustomDataKeys.NoRename, CustomizeOperation.RenameName, suppressRenameInventoryUi: true);
        RegisterTagBlockRule(DrakeCustomDataKeys.NoDescription, CustomizeOperation.RenameDescription);
        RegisterTagBlockRule(DrakeCustomDataKeys.NoCraftedByEdit, CustomizeOperation.EditCraftedBy);
        RegisterTagBlockRule(DrakeCustomDataKeys.QuestItem, CustomizeOperation.AllEdits, suppressRenameInventoryUi: true);
        RegisterTagBlockRule(
            DrakeCustomDataKeys.HardNoRename,
            CustomizeOperation.RenameName,
            suppressRenameInventoryUi: true,
            hardLock: true);
        // Description / crafted-by hard locks block those ops only — do not hide the whole Rename inventory tab.
        RegisterTagBlockRule(
            DrakeCustomDataKeys.HardNoDescription,
            CustomizeOperation.RenameDescription,
            suppressRenameInventoryUi: false,
            hardLock: true);
        RegisterTagBlockRule(
            DrakeCustomDataKeys.HardNoCraftedByEdit,
            CustomizeOperation.EditCraftedBy,
            suppressRenameInventoryUi: false,
            hardLock: true);
        RegisterTagBlockRule(
            DrakeCustomDataKeys.Immutable,
            CustomizeOperation.AllEdits,
            suppressRenameInventoryUi: true,
            hardLock: true);
        RegisterTagBlockRule(
            DrakeCustomDataKeys.DeferSuppressUi,
            CustomizeOperation.None,
            suppressRenameInventoryUi: true);
    }

    public static void RegisterTagBlockRule(string tagKey, CustomizeOperation blockedOperations) =>
        RegisterTagBlockRule(tagKey, blockedOperations, suppressRenameInventoryUi: false, hardLock: false);

    public static void RegisterTagBlockRule(
        string tagKey,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi) =>
        RegisterTagBlockRule(tagKey, blockedOperations, suppressRenameInventoryUi, hardLock: false);

    public static void RegisterTagBlockRule(
        string tagKey,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi,
        bool hardLock)
    {
        if (string.IsNullOrEmpty(tagKey))
            return;
        TagBlockRules.Add(new TagBlockRule(tagKey, blockedOperations, suppressRenameInventoryUi, hardLock));
    }

    public static void RegisterItemExclusion(ItemExclusionRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.Match) || rule.BlockedOperations == CustomizeOperation.None)
            return;
        ItemExclusions.Add(rule);
    }

    public static void RegisterValidator(CustomizeOperation operation, CustomizeEditValidator validator)
    {
        if (!Validators.TryGetValue(operation, out var list))
        {
            list = new List<CustomizeEditValidator>();
            Validators[operation] = list;
        }
        list.Add(validator);
    }

    public static void RegisterDeferredEditAuthority(
        string authorityId,
        CustomizeOperation operations,
        DeferredEditHandler handler)
    {
        if (string.IsNullOrEmpty(authorityId) || handler == null || operations == CustomizeOperation.None)
            return;
        DeferredAuthorities[authorityId] = new DeferredAuthorityRegistration
        {
            Operations = operations,
            Handler = handler,
        };
    }

    public static bool IsBlockedByTag(CustomizeOperation operation, ItemDrop.ItemData? item)
    {
        if (item == null)
            return true;
        EnsureDefaultTagRules();
        foreach (var rule in TagBlockRules)
        {
            if ((rule.BlockedOperations & operation) == 0)
                continue;
            if (DrakeTagManager.HasTag(item, rule.TagKey))
                return true;
        }
        return IsSoftOrHardExcluded(operation, item);
    }

    public static bool IsHardBlockedByTag(CustomizeOperation operation, ItemDrop.ItemData? item)
    {
        if (item == null)
            return true;
        EnsureDefaultTagRules();
        foreach (var rule in TagBlockRules)
        {
            if (!rule.HardLock)
                continue;
            if ((rule.BlockedOperations & operation) == 0)
                continue;
            if (DrakeTagManager.HasTag(item, rule.TagKey))
                return true;
        }
        return IsHardExcluded(operation, item);
    }

    public static bool IsDeferredFor(CustomizeOperation operation, ItemDrop.ItemData? item)
    {
        if (item == null || operation == CustomizeOperation.None)
            return false;
        if (DrakeTagManager.HasTag(item, DrakeCustomDataKeys.DeferEdits) &&
            (operation & CustomizeOperation.AllEdits) != 0)
            return true;
        if ((operation & CustomizeOperation.RenameName) != 0 &&
            DrakeTagManager.HasTag(item, DrakeCustomDataKeys.DeferRename))
            return true;
        if ((operation & CustomizeOperation.RenameDescription) != 0 &&
            DrakeTagManager.HasTag(item, DrakeCustomDataKeys.DeferDescription))
            return true;
        if ((operation & CustomizeOperation.EditCraftedBy) != 0 &&
            DrakeTagManager.HasTag(item, DrakeCustomDataKeys.DeferCraftedBy))
            return true;
        return TryGetDeferredExclusionAuthority(operation, item, out _);
    }

    public static string? GetEditAuthorityId(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null)
            return null;
        if (!item.m_customData.TryGetValue(DrakeCustomDataKeys.EditAuthority, out var id))
            return null;
        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }

    public static bool TryEvaluateDeferred(
        CustomizeOperation operation,
        ItemDrop.ItemData? item,
        Player? player,
        out bool allowed)
    {
        allowed = false;
        if (!IsDeferredFor(operation, item))
            return false;

        string? authorityId = GetEditAuthorityId(item);
        if (authorityId == null)
            TryGetDeferredExclusionAuthority(operation, item, out authorityId);

        if (authorityId == null ||
            !DeferredAuthorities.TryGetValue(authorityId, out var reg) ||
            (reg.Operations & operation) == 0)
        {
            allowed = false;
            return true;
        }

        try
        {
            allowed = reg.Handler(item, player, operation);
        }
        catch (Exception)
        {
            allowed = false;
        }

        return true;
    }

    public static bool IsRenameInventorySuppressed(ItemDrop.ItemData? item) =>
        IsRenameInventorySuppressed(item, Player.m_localPlayer);

    /// <summary>
    /// Soft inventory-UI suppress respects <see cref="TagBypass"/>; hard suppress does not.
    /// </summary>
    public static bool IsRenameInventorySuppressed(ItemDrop.ItemData? item, Player? player)
    {
        if (item == null)
            return false;
        EnsureDefaultTagRules();

        var bypass = false;
        try
        {
            bypass = TagBypass?.Invoke(player) == true;
        }
        catch
        {
            bypass = false;
        }

        foreach (var rule in TagBlockRules)
        {
            if (!rule.SuppressRenameInventoryUi)
                continue;
            if (!DrakeTagManager.HasTag(item, rule.TagKey))
                continue;
            if (rule.HardLock)
                return true;
            if (bypass)
                continue;
            return true;
        }

        foreach (var ex in ItemExclusions)
        {
            if (!ex.SuppressRenameInventoryUi)
                continue;
            if (!ItemNameMatch.Matches(item, ex.Match))
                continue;
            if (ex.HardLock)
                return true;
            if (bypass)
                continue;
            return true;
        }

        return false;
    }

    public static bool CanPerform(CustomizeOperation operation, ItemDrop.ItemData? item, Player? player)
    {
        if (item == null)
            return false;

        EnsureDefaultTagRules();

        if (IsHardBlockedByTag(operation, item))
            return false;

        if (TryEvaluateDeferred(operation, item, player, out var deferredAllowed))
        {
            if (!deferredAllowed)
                return false;
            return RunValidators(operation, item, player);
        }

        if (IsSoftBlocked(operation, item) && TagBypass?.Invoke(player) != true)
            return false;

        return RunValidators(operation, item, player);
    }

    static bool IsSoftBlocked(CustomizeOperation operation, ItemDrop.ItemData? item)
    {
        foreach (var rule in TagBlockRules)
        {
            if (rule.HardLock)
                continue;
            if ((rule.BlockedOperations & operation) == 0)
                continue;
            if (DrakeTagManager.HasTag(item, rule.TagKey))
                return true;
        }

        foreach (var ex in ItemExclusions)
        {
            if (ex.IsDeferred || ex.HardLock)
                continue;
            if ((ex.BlockedOperations & operation) == 0)
                continue;
            if (ItemNameMatch.Matches(item, ex.Match))
                return true;
        }

        return false;
    }

    static bool IsHardExcluded(CustomizeOperation operation, ItemDrop.ItemData? item)
    {
        foreach (var ex in ItemExclusions)
        {
            if (!ex.HardLock || ex.IsDeferred)
                continue;
            if ((ex.BlockedOperations & operation) == 0)
                continue;
            if (ItemNameMatch.Matches(item, ex.Match))
                return true;
        }
        return false;
    }

    static bool IsSoftOrHardExcluded(CustomizeOperation operation, ItemDrop.ItemData? item)
    {
        foreach (var ex in ItemExclusions)
        {
            if (ex.IsDeferred)
                continue;
            if ((ex.BlockedOperations & operation) == 0)
                continue;
            if (ItemNameMatch.Matches(item, ex.Match))
                return true;
        }
        return false;
    }

    static bool TryGetDeferredExclusionAuthority(
        CustomizeOperation operation,
        ItemDrop.ItemData? item,
        out string? authorityId)
    {
        authorityId = null;
        foreach (var ex in ItemExclusions)
        {
            if (!ex.IsDeferred)
                continue;
            if ((ex.BlockedOperations & operation) == 0)
                continue;
            if (!ItemNameMatch.Matches(item, ex.Match))
                continue;
            authorityId = ex.DeferredAuthorityId;
            return true;
        }
        return false;
    }

    static bool RunValidators(CustomizeOperation operation, ItemDrop.ItemData? item, Player? player)
    {
        if (!Validators.TryGetValue(operation, out var list) || list.Count == 0)
            return true;
        foreach (var v in list)
        {
            if (!v(item, player))
                return false;
        }
        return true;
    }
}
