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

    /// <summary><c>RRGGBB</c> -> color; null for empty/invalid.</summary>
    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrEmpty(hex) || hex!.Length != 6
            || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return null;
        return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }

    public static string? FormatColor(Color? color)
    {
        if (color == null)
            return null;
        var c = (Color32)color.Value;
        return $"{c.r:X2}{c.g:X2}{c.b:X2}";
    }

    /// <summary>Network form: 0 = no tint, otherwise 0x01RRGGBB.</summary>
    internal static int PackTint(Color? color)
    {
        if (color == null)
            return 0;
        var c = (Color32)color.Value;
        return (1 << 24) | (c.r << 16) | (c.g << 8) | c.b;
    }

    internal static Color? UnpackTint(int packed) =>
        packed == 0 ? null : new Color(((packed >> 16) & 0xFF) / 255f, ((packed >> 8) & 0xFF) / 255f, (packed & 0xFF) / 255f, 1f);

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
