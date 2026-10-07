using System;
using System.Collections.Generic;

namespace DrakeModsLibs.Permissions;

/// <summary>
/// Category tokens for exclusion lists (same vocabulary as RenameIt's ExcludedCategory):
/// aliases (Weapons, Armor, ...), any <see cref="Skills.SkillType"/>, or any <see cref="ItemDrop.ItemData.ItemType"/>.
/// Feature mods can add aliases (e.g. RenameIt's "Paper") via <see cref="RegisterAlias"/>.
/// </summary>
public static class DrakeItemCategory
{
    static readonly Dictionary<string, Func<ItemDrop.ItemData, bool>> Aliases =
        new Dictionary<string, Func<ItemDrop.ItemData, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["armor"] = i => i.m_shared.m_itemType is ItemDrop.ItemData.ItemType.Helmet
                or ItemDrop.ItemData.ItemType.Chest or ItemDrop.ItemData.ItemType.Legs or ItemDrop.ItemData.ItemType.Shoulder,
            ["weapons"] = i => i.m_shared.m_itemType is ItemDrop.ItemData.ItemType.OneHandedWeapon
                or ItemDrop.ItemData.ItemType.TwoHandedWeapon or ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft
                or ItemDrop.ItemData.ItemType.Bow,
            ["tools"] = i => i.m_shared.m_skillType is Skills.SkillType.Pickaxes or Skills.SkillType.WoodCutting,
            ["ranged"] = i => i.m_shared.m_itemType is ItemDrop.ItemData.ItemType.Bow or ItemDrop.ItemData.ItemType.Ammo
                              || i.m_shared.m_skillType is Skills.SkillType.Bows or Skills.SkillType.Crossbows,
            ["melee"] = i => i.m_shared.m_skillType is Skills.SkillType.Swords or Skills.SkillType.Axes
                or Skills.SkillType.Clubs or Skills.SkillType.Polearms or Skills.SkillType.Spears
                or Skills.SkillType.Knives or Skills.SkillType.Unarmed or Skills.SkillType.Pickaxes,
            ["shields"] = i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Shield,
            ["ammo"] = i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Ammo,
            ["fish"] = i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Fish,
        };

    public static void RegisterAlias(string token, Func<ItemDrop.ItemData, bool> matches)
    {
        if (!string.IsNullOrWhiteSpace(token) && matches != null)
            Aliases[token.Trim()] = matches;
    }

    public static bool Matches(ItemDrop.ItemData? item, string? token)
    {
        if (item?.m_shared == null || string.IsNullOrWhiteSpace(token))
            return false;
        token = token!.Trim();

        if (Aliases.TryGetValue(token, out var alias))
        {
            try
            {
                return alias(item);
            }
            catch
            {
                return false;
            }
        }

        if (Enum.TryParse(token, true, out Skills.SkillType skill) && Enum.IsDefined(typeof(Skills.SkillType), skill)
            && item.m_shared.m_skillType == skill)
            return true;
        return Enum.TryParse(token, true, out ItemDrop.ItemData.ItemType type)
               && Enum.IsDefined(typeof(ItemDrop.ItemData.ItemType), type)
               && item.m_shared.m_itemType == type;
    }
}
