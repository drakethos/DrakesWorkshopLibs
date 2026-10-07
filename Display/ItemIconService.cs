using System.Collections.Generic;
using DrakeModsLibs.Data;
using UnityEngine;

namespace DrakeModsLibs.Display;

/// <summary>
/// Per-stack icon override: <see cref="DrakeCustomDataKeys.IconOverride"/> stores a source reference
/// (<c>PrefabName</c> or <c>PrefabName#variant</c>) and <see cref="ItemDrop.ItemData.GetIcon"/> returns that prefab's sprite.
/// Only a name is saved — no image data — so overrides survive saves, chests, and trades.
/// </summary>
public static class ItemIconService
{
    static readonly Dictionary<string, Sprite?> Cache = new Dictionary<string, Sprite?>();
    static ObjectDB? _cacheDb;

    public static string? GetIconOverride(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null)
            return null;
        return item.m_customData.TryGetValue(DrakeCustomDataKeys.IconOverride, out var v) && !string.IsNullOrEmpty(v) ? v : null;
    }

    public static bool HasIconOverride(ItemDrop.ItemData? item) => GetIconOverride(item) != null;

    public static void SetIconOverride(ItemDrop.ItemData item, string? sourceRef)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (string.IsNullOrEmpty(sourceRef)) item.m_customData.Remove(DrakeCustomDataKeys.IconOverride);
        else item.m_customData[DrakeCustomDataKeys.IconOverride] = sourceRef!;
    }

    public static void ClearIconOverride(ItemDrop.ItemData item) => SetIconOverride(item, null);

    /// <summary>Sprite for an override on <paramref name="item"/>, or null when none / unresolvable (vanilla icon stays).</summary>
    public static Sprite? ResolveOverride(ItemDrop.ItemData? item)
    {
        var sourceRef = GetIconOverride(item);
        return sourceRef == null ? null : ResolveSourceRef(sourceRef);
    }

    /// <summary>Resolve <c>PrefabName</c> / <c>PrefabName#variant</c> to a sprite via ObjectDB (cached per ObjectDB instance).</summary>
    public static Sprite? ResolveSourceRef(string sourceRef)
    {
        var db = ObjectDB.instance;
        if (db == null)
            return null;
        if (_cacheDb != db)
        {
            Cache.Clear();
            _cacheDb = db;
        }

        if (Cache.TryGetValue(sourceRef, out var cached))
            return cached;

        Sprite? sprite = null;
        ParseSourceRef(sourceRef, out var prefabName, out var variant);
        var icons = db.GetItemPrefab(prefabName)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_icons;
        if (icons != null && icons.Length > 0)
            sprite = icons[Mathf.Clamp(variant, 0, icons.Length - 1)];

        Cache[sourceRef] = sprite;
        return sprite;
    }

    public static string MakeSourceRef(string prefabName, int variant) =>
        variant > 0 ? prefabName + "#" + variant : prefabName;

    public static void ParseSourceRef(string sourceRef, out string prefabName, out int variant)
    {
        variant = 0;
        prefabName = sourceRef;
        var hash = sourceRef.LastIndexOf('#');
        if (hash > 0 && int.TryParse(sourceRef.Substring(hash + 1), out var v))
        {
            prefabName = sourceRef.Substring(0, hash);
            variant = v;
        }
    }
}
