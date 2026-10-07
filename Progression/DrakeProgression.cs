using System;
using System.Collections.Generic;
using System.Linq;

namespace DrakeModsLibs.Progression;

/// <summary>
/// Which world-progress key an item belongs to (e.g. iron gear -> <c>defeated_gdking</c>).
/// <para>
/// Base materials are anchored to the boss that unlocks them (<see cref="Anchors"/>); crafted items inherit the
/// latest key among their recipe ingredients, recursively, so modded gear built from vanilla materials sorts itself.
/// Items with no anchor and no recipe path return null (never locked).
/// </para>
/// Servers can swap a key for their own (<c>defeated_gdking=mod_iron</c>, set with the vanilla <c>setkey</c> command)
/// and pin single items (<c>SwordIron=mod_iron</c>).
/// </summary>
public static class DrakeProgression
{
    /// <summary>Vanilla boss keys in progression order.</summary>
    public static readonly string[] BossKeys =
    {
        "defeated_eikthyr", "defeated_gdking", "defeated_bonemass", "defeated_dragon",
        "defeated_goblinking", "defeated_queen", "defeated_fader",
    };

    static readonly Dictionary<string, string> BossNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["defeated_eikthyr"] = "Eikthyr",
        ["defeated_gdking"] = "The Elder",
        ["defeated_bonemass"] = "Bonemass",
        ["defeated_dragon"] = "Moder",
        ["defeated_goblinking"] = "Yagluth",
        ["defeated_queen"] = "The Queen",
        ["defeated_fader"] = "Fader",
    };

    /// <summary>Raw materials / drops tied to the boss that gates getting them. Unknown names are harmless.</summary>
    static readonly Dictionary<string, string[]> Anchors = new Dictionary<string, string[]>
    {
        ["defeated_eikthyr"] = new[]
        {
            "HardAntler", "CopperOre", "Copper", "CopperScrap", "TinOre", "Tin", "Bronze", "TrophyEikthyr",
        },
        ["defeated_gdking"] = new[]
        {
            "CryptKey", "IronScrap", "IronOre", "Iron", "ElderBark", "Chain", "Ooze", "WitheredBone", "Root",
            "Guck", "Entrails", "BloodBag", "TrophyTheElder",
        },
        ["defeated_bonemass"] = new[]
        {
            "Wishbone", "SilverOre", "Silver", "Obsidian", "WolfPelt", "WolfFang", "WolfClaw", "FreezeGland",
            "DragonEgg", "Crystal", "JuteRed", "TrophyBonemass",
        },
        ["defeated_dragon"] = new[]
        {
            "DragonTear", "BlackMetalScrap", "BlackMetal", "Barley", "Flax", "LoxPelt", "LoxMeat", "Needle", "Tar",
            "GoblinTotem", "TrophyDragonQueen",
        },
        ["defeated_goblinking"] = new[]
        {
            "YagluthDrop", "Sap", "SoftTissue", "Eitr", "BlackCore", "Carapace", "Mandible", "YggdrasilWood",
            "ScaleHide", "BlackMarble", "Bilebag", "RoyalJelly", "TrophyGoblinKing",
        },
        ["defeated_queen"] = new[]
        {
            "QueenDrop", "FlametalOre", "Flametal", "FlametalOreNew", "FlametalNew", "CharredBone", "AskHide",
            "AskBladder", "Blackwood", "Grausten", "MoltenCore", "CelestialFeather", "MorgenSinew", "MorgenHeart",
            "TrophySeekerQueen",
        },
        ["defeated_fader"] = new[] { "FaderDrop", "TrophyFader" },
    };

    static Dictionary<string, int>? _anchorIndex;
    static readonly Dictionary<string, int> Derived = new Dictionary<string, int>(StringComparer.Ordinal);
    static Dictionary<string, List<Recipe>>? _recipesByItem;
    static ObjectDB? _builtFor;

    /// <summary>Friendly name for a key: boss name for vanilla keys, otherwise the key itself.</summary>
    public static string DescribeKey(string key) =>
        BossNames.TryGetValue(key, out var boss) ? boss : key;

    /// <summary>True when the key is set on the server, or (player-based progression worlds) on the player.</summary>
    public static bool IsKeyUnlocked(string key, Player? player)
    {
        if (string.IsNullOrEmpty(key))
            return true;
        if (ZoneSystem.instance != null && ZoneSystem.instance.GetGlobalKey(key))
            return true;
        return player != null && player.HaveUniqueKey(key);
    }

    /// <summary>
    /// Key required for <paramref name="prefabName"/> after per-item overrides and key aliases, or null when never locked.
    /// </summary>
    /// <param name="overrides">Parsed <c>Item=key</c> pins (see <see cref="ParsePairs"/>).</param>
    /// <param name="aliases">Parsed <c>key=replacementKey</c> swaps.</param>
    public static string? RequiredKey(string prefabName, IReadOnlyDictionary<string, string>? overrides = null,
        IReadOnlyDictionary<string, string>? aliases = null)
    {
        if (string.IsNullOrEmpty(prefabName))
            return null;
        if (overrides != null && overrides.TryGetValue(prefabName, out var pinned))
            return string.IsNullOrWhiteSpace(pinned) || pinned.Equals("none", StringComparison.OrdinalIgnoreCase) ? null : pinned;

        var tier = TierOf(prefabName);
        if (tier < 0)
            return null;
        var key = BossKeys[tier];
        return aliases != null && aliases.TryGetValue(key, out var alias) && !string.IsNullOrWhiteSpace(alias) ? alias : key;
    }

    /// <summary>Parse <c>a=b, c=d</c> (also ';' separated) into a case-insensitive map.</summary>
    public static Dictionary<string, string> ParsePairs(string? csv)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csv))
            return map;
        foreach (var part in csv!.Split(',', ';'))
        {
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;
            var k = part.Substring(0, eq).Trim();
            if (k.Length > 0)
                map[k] = part.Substring(eq + 1).Trim();
        }
        return map;
    }

    /// <summary>Progression index (into <see cref="BossKeys"/>), or -1 when ungated.</summary>
    static int TierOf(string prefabName)
    {
        EnsureBuilt();
        return Resolve(prefabName, new HashSet<string>(StringComparer.Ordinal));
    }

    static int Resolve(string prefabName, HashSet<string> visiting)
    {
        if (Derived.TryGetValue(prefabName, out var cached))
            return cached;
        if (!visiting.Add(prefabName))
            return -1; // recipe cycle: treat as ungated on this path

        var tier = _anchorIndex!.TryGetValue(prefabName, out var anchored) ? anchored : -1;
        if (_recipesByItem!.TryGetValue(prefabName, out var recipes))
        {
            // Easiest recipe wins; within a recipe the latest ingredient decides.
            var best = int.MaxValue;
            foreach (var recipe in recipes)
            {
                var worst = -1;
                foreach (var req in recipe.m_resources ?? Array.Empty<Piece.Requirement>())
                {
                    if (req?.m_resItem != null)
                        worst = Math.Max(worst, Resolve(req.m_resItem.gameObject.name, visiting));
                }
                best = Math.Min(best, worst);
            }
            if (best != int.MaxValue)
                tier = Math.Max(tier, best);
        }

        visiting.Remove(prefabName);
        Derived[prefabName] = tier;
        return tier;
    }

    static void EnsureBuilt()
    {
        var db = ObjectDB.instance;
        if (_anchorIndex == null)
        {
            _anchorIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < BossKeys.Length; i++)
            {
                foreach (var name in Anchors[BossKeys[i]])
                    _anchorIndex[name] = i;
            }
        }
        if (_recipesByItem != null && _builtFor == db)
            return;

        _builtFor = db;
        Derived.Clear();
        _recipesByItem = new Dictionary<string, List<Recipe>>(StringComparer.Ordinal);
        if (db == null)
            return;
        foreach (var recipe in db.m_recipes.Where(r => r != null && r.m_enabled && r.m_item != null))
        {
            var name = recipe.m_item.gameObject.name;
            if (!_recipesByItem.TryGetValue(name, out var list))
                _recipesByItem[name] = list = new List<Recipe>();
            list.Add(recipe);
        }
    }
}
