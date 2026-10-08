using System.Collections.Generic;
using System.Globalization;
using DrakeModsLibs.Data;
using UnityEngine;

namespace DrakeModsLibs.Display;

/// <summary>
/// Per-stack equipped-model override and tints. Like <see cref="ItemIconService"/>, only names and colors are saved
/// (<see cref="DrakeCustomDataKeys.ModelOverride"/>, <see cref="DrakeCustomDataKeys.IconTint"/>,
/// <see cref="DrakeCustomDataKeys.ModelTint"/>), so looks survive saves, chests and trades.
/// </summary>
public static class ItemLookService
{
    public static string? GetModelOverride(ItemDrop.ItemData? item) => Get(item, DrakeCustomDataKeys.ModelOverride);

    /// <summary>Store a model source (<see cref="IconCatalogEntry.SourceRef"/>); null/empty clears. Caller checks compatibility.</summary>
    public static void SetModelOverride(ItemDrop.ItemData item, string? sourceRef) => Set(item, DrakeCustomDataKeys.ModelOverride, sourceRef);

    public static Color? GetIconTint(ItemDrop.ItemData? item) => ParseColor(Get(item, DrakeCustomDataKeys.IconTint));
    public static void SetIconTint(ItemDrop.ItemData item, Color? tint) => Set(item, DrakeCustomDataKeys.IconTint, FormatColor(tint));

    public static Color? GetModelTint(ItemDrop.ItemData? item) => ParseColor(Get(item, DrakeCustomDataKeys.ModelTint));
    public static void SetModelTint(ItemDrop.ItemData item, Color? tint) => Set(item, DrakeCustomDataKeys.ModelTint, FormatColor(tint));

    /// <summary>Item types whose model is drawn on the character (the only ones a model swap affects).</summary>
    public static bool IsEquippable(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;
        switch (item.m_shared.m_itemType)
        {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.Shield:
            case ItemDrop.ItemData.ItemType.Helmet:
            case ItemDrop.ItemData.ItemType.Chest:
            case ItemDrop.ItemData.ItemType.Legs:
            case ItemDrop.ItemData.ItemType.Shoulder:
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.Torch:
            case ItemDrop.ItemData.ItemType.Utility:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// A model can only come from an item of the exact same type (one-handed for one-handed, helmet for helmet, ...),
    /// so attach points and animations stay correct.
    /// </summary>
    public static bool IsModelCompatible(ItemDrop.ItemData? item, IconCatalogEntry? source) =>
        IsEquippable(item) && source != null && source.ItemType == item!.m_shared.m_itemType;

    /// <summary>Prefab hash + variant for an override on <paramref name="item"/>, when it still resolves to a compatible prefab.</summary>
    internal static bool TryResolveModel(ItemDrop.ItemData item, out int prefabHash, out int variant)
    {
        prefabHash = variant = 0;
        var sourceRef = GetModelOverride(item);
        if (sourceRef == null || ObjectDB.instance == null)
            return false;
        ItemIconService.ParseSourceRef(sourceRef, out var prefabName, out variant);
        var shared = ObjectDB.instance.GetItemPrefab(prefabName)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
        if (shared == null || shared.m_itemType != item.m_shared.m_itemType)
            return false;
        prefabHash = prefabName.GetStableHashCode();
        return true;
    }

    /// <summary>
    /// Highest brightness boost a tint can carry. A boosted tint is the base color times the boost (channels above 1),
    /// which pushes a texture toward white instead of only darkening it.
    /// </summary>
    public const float MaxTintBoost = 4f;

    const int BoostSteps = 127; // 7 bits in the network form

    /// <summary>Boost carried by a tint: its brightest channel, clamped to 1..<see cref="MaxTintBoost"/>.</summary>
    public static float BoostOf(Color color) => Mathf.Clamp(Mathf.Max(color.r, Mathf.Max(color.g, color.b)), 1f, MaxTintBoost);

    /// <summary><c>RRGGBB</c> or boosted <c>RRGGBB*2.50</c> -> color (channels may exceed 1); null for empty/invalid.</summary>
    public static Color? ParseColor(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;
        var hex = value!;
        var boost = 1f;
        var star = hex.IndexOf('*');
        if (star >= 0)
        {
            if (!float.TryParse(hex.Substring(star + 1), NumberStyles.Float, CultureInfo.InvariantCulture, out boost))
                return null;
            boost = Mathf.Clamp(boost, 1f, MaxTintBoost);
            hex = hex.Substring(0, star);
        }
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return null;
        return new Color(((rgb >> 16) & 0xFF) / 255f * boost, ((rgb >> 8) & 0xFF) / 255f * boost, (rgb & 0xFF) / 255f * boost, 1f);
    }

    /// <summary>Color -> <c>RRGGBB</c>, or <c>RRGGBB*boost</c> when a channel exceeds 1. Older readers see only the base color.</summary>
    public static string? FormatColor(Color? color)
    {
        if (color == null)
            return null;
        var boost = BoostOf(color.Value);
        var c = BaseColor(color.Value, boost);
        var hex = $"{c.r:X2}{c.g:X2}{c.b:X2}";
        return boost > 1.005f ? hex + "*" + boost.ToString("0.00", CultureInfo.InvariantCulture) : hex;
    }

    /// <summary>Network form: 0 = no tint, otherwise boost step (bits 25-31) | 0x01RRGGBB. Older readers ignore the boost bits.</summary>
    internal static int PackTint(Color? color)
    {
        if (color == null)
            return 0;
        var boost = BoostOf(color.Value);
        var step = Mathf.Clamp(Mathf.RoundToInt((boost - 1f) / (MaxTintBoost - 1f) * BoostSteps), 0, BoostSteps);
        var c = BaseColor(color.Value, boost);
        return (step << 25) | (1 << 24) | (c.r << 16) | (c.g << 8) | c.b;
    }

    internal static Color? UnpackTint(int packed)
    {
        if (packed == 0)
            return null;
        var boost = 1f + ((packed >> 25) & BoostSteps) * (MaxTintBoost - 1f) / BoostSteps;
        return new Color(((packed >> 16) & 0xFF) / 255f * boost, ((packed >> 8) & 0xFF) / 255f * boost, (packed & 0xFF) / 255f * boost, 1f);
    }

    static Color32 BaseColor(Color color, float boost) =>
        new Color(color.r / boost, color.g / boost, color.b / boost, 1f);

    static string? Get(ItemDrop.ItemData? item, string key)
    {
        if (item?.m_customData == null)
            return null;
        return item.m_customData.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v) ? v : null;
    }

    static void Set(ItemDrop.ItemData item, string key, string? value)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (string.IsNullOrEmpty(value)) item.m_customData.Remove(key);
        else item.m_customData[key] = value!;
    }
}
