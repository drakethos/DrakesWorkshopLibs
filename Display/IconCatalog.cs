using System.Collections.Generic;
using UnityEngine;

namespace DrakeModsLibs.Display;

/// <summary>Picker grouping for icons; derived from <see cref="ItemDrop.ItemData.ItemType"/>.</summary>
public enum IconCategory
{
    Weapons,
    Armor,
    Tools,
    Food,
    Materials,
    Trophies,
    Misc,
}

public sealed class IconCatalogEntry
{
    internal IconCatalogEntry(string prefabName, int variant, Sprite sprite, string nameToken, IconCategory category,
        ItemDrop.ItemData.ItemType itemType)
    {
        ItemType = itemType;
        PrefabName = prefabName;
        Variant = variant;
        Sprite = sprite;
        NameToken = nameToken;
        Category = category;
    }

    public string PrefabName { get; }
    public int Variant { get; }
    public Sprite Sprite { get; }

    /// <summary>Unlocalized <c>$item_*</c> token; localize at display time.</summary>
    public string NameToken { get; }

    public IconCategory Category { get; }

    /// <summary>Exact item type, used to decide which models fit which slot.</summary>
    public ItemDrop.ItemData.ItemType ItemType { get; }

    /// <summary>Value to store via <see cref="ItemIconService.SetIconOverride"/>.</summary>
    public string SourceRef => ItemIconService.MakeSourceRef(PrefabName, Variant);
}

/// <summary>Every distinct item icon in ObjectDB (includes modded items). Built lazily, rebuilt when ObjectDB changes.</summary>
public static class IconCatalog
{
    static readonly List<IconCatalogEntry> Entries = new List<IconCatalogEntry>();
    static ObjectDB? _builtFor;
    static int _builtItemCount;

    public static IReadOnlyList<IconCatalogEntry> GetAll()
    {
        var db = ObjectDB.instance;
        if (db == null)
            return Entries;
        if (_builtFor != db || _builtItemCount != db.m_items.Count)
            Build(db);
        return Entries;
    }

    public static IconCategory CategoryOf(ItemDrop.ItemData? item) =>
        item?.m_shared == null ? IconCategory.Misc : CategoryOf(item.m_shared.m_itemType);

    public static IconCategory CategoryOf(ItemDrop.ItemData.ItemType type)
    {
        switch (type)
        {
            case ItemDrop.ItemData.ItemType.OneHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeapon:
            case ItemDrop.ItemData.ItemType.TwoHandedWeaponLeft:
            case ItemDrop.ItemData.ItemType.Bow:
            case ItemDrop.ItemData.ItemType.Ammo:
                return IconCategory.Weapons;
            case ItemDrop.ItemData.ItemType.Helmet:
            case ItemDrop.ItemData.ItemType.Chest:
            case ItemDrop.ItemData.ItemType.Legs:
            case ItemDrop.ItemData.ItemType.Shoulder:
            case ItemDrop.ItemData.ItemType.Shield:
            case ItemDrop.ItemData.ItemType.Utility:
                return IconCategory.Armor;
            case ItemDrop.ItemData.ItemType.Tool:
            case ItemDrop.ItemData.ItemType.Torch:
                return IconCategory.Tools;
            case ItemDrop.ItemData.ItemType.Consumable:
                return IconCategory.Food;
            case ItemDrop.ItemData.ItemType.Material:
                return IconCategory.Materials;
            case ItemDrop.ItemData.ItemType.Trophy:
                return IconCategory.Trophies;
            default:
                return IconCategory.Misc;
        }
    }

    static void Build(ObjectDB db)
    {
        Entries.Clear();
        _builtFor = db;
        _builtItemCount = db.m_items.Count;

        var seen = new HashSet<Sprite>();
        foreach (var go in db.m_items)
        {
            if (!go)
                continue;
            var shared = go.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
            var icons = shared?.m_icons;
            if (shared == null || icons == null)
                continue;
            var category = CategoryOf(shared.m_itemType);
            for (var v = 0; v < icons.Length; v++)
            {
                var sprite = icons[v];
                if (!sprite || !seen.Add(sprite))
                    continue;
                Entries.Add(new IconCatalogEntry(go.name, v, sprite, shared.m_name ?? go.name, category, shared.m_itemType));
            }
        }
    }
}
