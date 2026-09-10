using System;
using System.Collections.Generic;
using DrakeModsLibs.API;
using DrakeModsLibs.Data;

namespace DrakeModsLibs.Tags;

public delegate bool CustomizeEditValidator(ItemDrop.ItemData? item, Player? player);

public static class CustomizationGatekeeper
{
    static readonly Dictionary<CustomizeOperation, List<CustomizeEditValidator>> Validators = new();
    static readonly List<TagBlockRule> TagBlockRules = new();
    static bool _defaultTagRulesRegistered;

    /// <summary>
    /// When this returns true for a player, tag blocks (e.g. <see cref="DrakeCustomDataKeys.NoRename"/>) are skipped.
    /// RenameIt registers admin/VIP override here.
    /// </summary>
    public static Func<Player?, bool>? TagBypass { get; set; }

    static void EnsureDefaultTagRules()
    {
        if (_defaultTagRulesRegistered)
            return;
        _defaultTagRulesRegistered = true;
        RegisterTagBlockRule(DrakeCustomDataKeys.NoRename, CustomizeOperation.RenameName);
        RegisterTagBlockRule(DrakeCustomDataKeys.NoDescription, CustomizeOperation.RenameDescription);
        RegisterTagBlockRule(DrakeCustomDataKeys.NoCraftedByEdit, CustomizeOperation.EditCraftedBy);
        RegisterTagBlockRule(DrakeCustomDataKeys.QuestItem, CustomizeOperation.AllEdits);
    }

    public static void RegisterTagBlockRule(string tagKey, CustomizeOperation blockedOperations)
    {
        if (string.IsNullOrEmpty(tagKey))
            return;
        TagBlockRules.Add(new TagBlockRule(tagKey, blockedOperations));
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
        return false;
    }

    public static bool CanPerform(CustomizeOperation operation, ItemDrop.ItemData? item, Player? player)
    {
        if (item == null)
            return false;
        if (IsBlockedByTag(operation, item) && TagBypass?.Invoke(player) != true)
            return false;
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
