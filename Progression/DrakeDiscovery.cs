using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace DrakeModsLibs.Progression;

/// <summary>
/// Per-character item history: "seen" (vanilla known materials — anything that has been in your inventory)
/// and "crafted" (tracked by Libs, saved in the character file under <see cref="CraftedKey"/>).
/// Crafted counts from install onward, plus any carried item whose crafter is this character.
/// </summary>
public static class DrakeDiscovery
{
    public const string CraftedKey = "Drake_Crafted";

    static Player? _cachedFor;
    static string? _cachedRaw;
    static HashSet<string> _crafted = new HashSet<string>(StringComparer.Ordinal);
    static HashSet<string>? _craftable;
    static ObjectDB? _craftableFor;

    /// <summary>Vanilla discovery: the item (by shared <c>$item_</c> name) has been in this player's inventory.</summary>
    public static bool HasSeen(Player? player, string sharedName) =>
        player != null && !string.IsNullOrEmpty(sharedName) && player.IsMaterialKnown(sharedName);

    /// <summary>
    /// True when this character has crafted <paramref name="prefabName"/>. Call <see cref="RecordCraftedFromInventory"/>
    /// once before a batch of checks so carried self-crafted items count.
    /// </summary>
    public static bool HasCrafted(Player? player, string prefabName) =>
        player != null && !string.IsNullOrEmpty(prefabName) && Load(player).Contains(prefabName);

    /// <summary>True when an enabled recipe produces <paramref name="prefabName"/>.</summary>
    public static bool IsCraftable(string prefabName)
    {
        var db = ObjectDB.instance;
        if (db == null)
            return false;
        if (_craftable == null || _craftableFor != db)
        {
            _craftableFor = db;
            _craftable = new HashSet<string>(
                db.m_recipes.Where(r => r != null && r.m_enabled && r.m_item != null).Select(r => r.m_item.gameObject.name),
                StringComparer.Ordinal);
        }
        return _craftable.Contains(prefabName);
    }

    /// <summary>Record every carried item whose crafter is <paramref name="player"/> (runs after each craft).</summary>
    public static void RecordCraftedFromInventory(Player player)
    {
        var inv = player.GetInventory();
        if (inv == null)
            return;
        var id = player.GetPlayerID();
        var set = Load(player);
        var changed = false;
        foreach (var item in inv.GetAllItems())
        {
            var prefab = item?.m_dropPrefab ? item!.m_dropPrefab.name : null;
            if (item != null && item.m_crafterID == id && !string.IsNullOrEmpty(prefab) && set.Add(prefab!))
                changed = true;
        }
        if (changed)
            Save(player, set);
    }

    static HashSet<string> Load(Player player)
    {
        player.m_customData.TryGetValue(CraftedKey, out var raw);
        raw ??= "";
        if (_cachedFor != player || !string.Equals(raw, _cachedRaw, StringComparison.Ordinal))
        {
            _cachedFor = player;
            _cachedRaw = raw;
            _crafted = new HashSet<string>(raw.Split(',').Where(s => s.Length > 0), StringComparer.Ordinal);
        }
        return _crafted;
    }

    static void Save(Player player, HashSet<string> set)
    {
        var raw = string.Join(",", set.OrderBy(s => s, StringComparer.Ordinal));
        player.m_customData[CraftedKey] = raw;
        _cachedRaw = raw;
    }

    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.DoCrafting))]
    static class CraftTrackingPatch
    {
        static void Postfix(Player player)
        {
            try
            {
                if (player != null && player == Player.m_localPlayer)
                    RecordCraftedFromInventory(player);
            }
            catch (Exception)
            {
                // never break crafting
            }
        }
    }
}
