using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Configuration;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace DrakeModsLibs.Art;

/// <summary>
/// Loads every Assets/Items/&lt;id&gt; folder beside a plugin. The mod does not copy this class.
/// Call <see cref="Register"/> and pass a customize hook if you want to change name, description, scale, or materials in code.
/// </summary>
public static class ArtItemLoader
{
    public static void Register(
        ManualLogSource log,
        string pluginDirectory,
        ConfigFile? config = null,
        Action<ArtItemContext>? customize = null)
    {
        var itemsRoot = Path.Combine(pluginDirectory ?? "", "Assets", "Items");
        PrefabManager.OnVanillaPrefabsAvailable -= OnPrefabs;
        void OnPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnPrefabs;
            AddItems(log, itemsRoot, config, customize);
        }

        PrefabManager.OnVanillaPrefabsAvailable += OnPrefabs;
    }

    private static void AddItems(ManualLogSource log, string itemsRoot, ConfigFile? config, Action<ArtItemContext>? customize)
    {
        if (!Directory.Exists(itemsRoot))
        {
            log?.LogWarning($"[ArtForge] No items folder: {itemsRoot}");
            return;
        }

        var registered = 0;
        foreach (var dir in EnumerateItemFolders(itemsRoot))
        {
            try
            {
                if (AddItem(log, dir, config, customize))
                    registered++;
            }
            catch (Exception ex)
            {
                log?.LogError($"[ArtForge] Failed '{Path.GetFileName(dir)}': {ex.Message}");
            }
        }

        log?.LogInfo($"[ArtForge] Registered {registered} item(s) from {itemsRoot}");
    }

    private static IEnumerable<string> EnumerateItemFolders(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            if (File.Exists(Path.Combine(dir, "item.json")))
            {
                yield return dir;
                continue;
            }

            foreach (var nested in EnumerateItemFolders(dir))
                yield return nested;
        }
    }

    private static bool AddItem(ManualLogSource log, string folder, ConfigFile? config, Action<ArtItemContext>? customize)
    {
        var wirePath = Path.Combine(folder, "item.json");
        var bundlePath = Path.Combine(folder, "art.bundle");
        if (!File.Exists(wirePath))
        {
            log?.LogWarning($"[ArtForge] Skip {Path.GetFileName(folder)} — needs item.json.");
            return false;
        }

        var hasArtBundle = File.Exists(bundlePath);

        var wire = File.ReadAllText(wirePath);
        var id = ReadString(wire, "id") ?? Path.GetFileName(folder);
        var donor = ReadString(wire, "donor");
        if (string.IsNullOrWhiteSpace(donor))
        {
            log?.LogError($"[ArtForge] {id}: item.json is missing donor.");
            return false;
        }

        var context = new ArtItemContext
        {
            Id = id,
            SourceId = ReadString(wire, "sourceId") ?? id,
            Donor = donor,
            Folder = folder,
            DisplayName = ReadString(wire, "displayName") ?? id,
            Description = ReadString(wire, "description"),
            Scale = SanitizeScale(ReadFloat(wire, "scale") ?? 1f),
            RequirementItem = "Wood",
            RequirementAmount = 1,
        };
        context.SetMaterials(ReadStringArray(wire, "existingMaterials"));
        ApplyConfig(config, context);
        customize?.Invoke(context);
        context.Scale = SanitizeScale(context.Scale);

        if (PrefabManager.Instance.GetPrefab(context.Id) != null)
        {
            log?.LogWarning($"[ArtForge] {context.Id} already exists. Another plugin registered it first.");
            return false;
        }

        if (PrefabManager.Instance.GetPrefab(context.Donor) == null)
        {
            log?.LogError($"[ArtForge] {context.Id}: donor prefab '{context.Donor}' was not found.");
            return false;
        }

        var token = "item_artforge_" + context.Id.ToLowerInvariant();
        var descriptionToken = ResolveDescription(context, token);

        var itemConfig = new ItemConfig
        {
            Name = "$" + token,
            Description = descriptionToken,
            Amount = 1,
            CraftingStation = string.IsNullOrWhiteSpace(context.CraftingStation) ? null : context.CraftingStation,
        };
        if (!string.IsNullOrWhiteSpace(context.RequirementItem) && context.RequirementAmount > 0)
            itemConfig.AddRequirement(context.RequirementItem, context.RequirementAmount);

        var item = new CustomItem(context.Id, context.Donor, itemConfig);
        var drop = item.ItemDrop;
        if (drop?.m_itemData?.m_shared == null)
        {
            log?.LogError($"[ArtForge] {context.Id}: clone has no ItemDrop.");
            return false;
        }

        context.Prefab = item.ItemPrefab;
        context.Drop = drop;
        ApplyIcon(log, folder, drop);
        if (hasArtBundle)
        {
            if (!AttachArtPrefab(log, item.ItemPrefab, bundlePath, context))
                return false;
        }
        else
        {
            log?.LogInfo($"[ArtForge] {context.Id}: registered without art.bundle (scripts/properties / icon only).");
        }

        ItemManager.Instance.AddItem(item);
        log?.LogInfo($"[ArtForge] Registered {context.Id} scale {context.Scale.ToString(CultureInfo.InvariantCulture)} materials [{string.Join(", ", context.Materials)}]. Spawn: {context.Id}");
        return true;
    }

    private static string ResolveDescription(ArtItemContext context, string nameToken)
    {
        var loc = LocalizationManager.Instance.GetLocalization();
        loc.AddTranslation("English", nameToken, context.DisplayName);
        if (!string.IsNullOrWhiteSpace(context.Description))
        {
            if (context.Description.StartsWith("$", StringComparison.Ordinal))
                return context.Description;
            loc.AddTranslation("English", nameToken + "_desc", context.Description);
            return "$" + nameToken + "_desc";
        }

        var donorPrefab = PrefabManager.Instance.GetPrefab(context.Donor);
        var donorDesc = donorPrefab?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_description;
        if (!string.IsNullOrWhiteSpace(donorDesc))
            return donorDesc;

        loc.AddTranslation("English", nameToken + "_desc", "");
        return "$" + nameToken + "_desc";
    }

    private static void ApplyConfig(ConfigFile? config, ArtItemContext context)
    {
        if (config == null)
            return;

        var scale = config.Bind(
            context.Id,
            "Scale",
            0f,
            "0 uses the scale from item.json (the editor). A positive number overrides it without recompiling.").Value;
        if (scale > 0f)
            context.Scale = scale;

        var materials = config.Bind(
            context.Id,
            "Materials",
            "",
            "Comma-separated Valheim material names, such as iron. Empty uses item.json. The loader matches a loaded material, then an item prefab of that name.").Value;
        if (!string.IsNullOrWhiteSpace(materials))
            context.SetMaterials(materials.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries));

        var name = config.Bind(context.Id, "DisplayName", "", "Empty uses the name from item.json.").Value;
        if (!string.IsNullOrWhiteSpace(name))
            context.DisplayName = name.Trim();

        var description = config.Bind(
            context.Id,
            "Description",
            "",
            "Empty keeps the donor prefab description. A $token is used as-is. Any other text is added as an English translation.").Value;
        if (!string.IsNullOrWhiteSpace(description))
            context.Description = description.Trim();
    }

    private static void ApplyIcon(ManualLogSource log, string folder, ItemDrop drop)
    {
        var iconPath = Path.Combine(folder, "icon.png");
        if (!File.Exists(iconPath))
            return;

        try
        {
            var tex = AssetUtils.LoadTexture(iconPath, relativePath: false);
            if (tex == null)
                return;
            drop.m_itemData.m_shared.m_icons = new[]
            {
                Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f),
            };
        }
        catch (Exception ex)
        {
            log?.LogWarning($"[ArtForge] Icon skipped: {ex.Message}");
        }
    }

    private static bool AttachArtPrefab(ManualLogSource log, GameObject item, string bundlePath, ArtItemContext context)
    {
        var bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.AssetBundleModule");
        var load = bundleType?.GetMethod("LoadFromFile", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
        var bundle = load?.Invoke(null, new object[] { bundlePath });
        if (bundle == null)
        {
            log?.LogError($"[ArtForge] {context.Id}: art.bundle failed to load.");
            return false;
        }

        var loadAsset = bundleType.GetMethod("LoadAsset", new[] { typeof(string), typeof(Type) });
        var art = loadAsset?.Invoke(bundle, new object[] { "art", typeof(GameObject) }) as GameObject;
        if (art == null)
        {
            log?.LogError($"[ArtForge] {context.Id}: bundle has no prefab named 'art'.");
            return false;
        }

        var visual = UnityEngine.Object.Instantiate(art, item.transform, false);
        visual.name = "art";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * context.Scale;
        ApplyExistingMaterials(log, visual, context);

        foreach (var renderer in item.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null || renderer.transform.IsChildOf(visual.transform))
                continue;
            var typeName = renderer.GetType().Name;
            if (typeName != "MeshRenderer" && typeName != "SkinnedMeshRenderer")
                continue;
            renderer.enabled = false;
        }

        return true;
    }

    private static void ApplyExistingMaterials(ManualLogSource log, GameObject visual, ArtItemContext context)
    {
        if (context.Materials.Count == 0)
        {
            log?.LogWarning($"[ArtForge] {context.Id}: no materials set. The art mesh has nothing to draw with. Set Materials in config or the editor (try iron).");
            return;
        }

        var loaded = Resources.FindObjectsOfTypeAll<Material>();
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (context.Materials.Count == 1)
        {
            if (!TryResolveMaterial(log, loaded, context.Materials[0], context.Id, out var material))
                return;
            foreach (var renderer in renderers)
            {
                if (renderer != null)
                    renderer.sharedMaterial = material;
            }

            return;
        }

        var slot = 0;
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            var slots = renderer.sharedMaterials;
            for (var i = 0; i < slots.Length && slot < context.Materials.Count; i++, slot++)
            {
                if (TryResolveMaterial(log, loaded, context.Materials[slot], context.Id, out var material))
                    slots[i] = material;
            }

            renderer.sharedMaterials = slots;
        }
    }

    private static bool TryResolveMaterial(ManualLogSource log, Material[] loaded, string name, string id, out Material found)
    {
        if (TryFindLoadedMaterial(loaded, name, out found))
            return true;

        found = MaterialFromPrefab(name);
        if (found != null)
        {
            log?.LogInfo($"[ArtForge] {id}: material '{name}' taken from prefab '{name}' ({found.name}).");
            return true;
        }

        if (Aliases.TryGetValue(name.Trim(), out var prefabs))
        {
            foreach (var prefabName in prefabs)
            {
                found = MaterialFromPrefab(prefabName);
                if (found == null)
                    continue;
                log?.LogInfo($"[ArtForge] {id}: material '{name}' taken from prefab '{prefabName}' ({found.name}).");
                return true;
            }
        }

        log?.LogWarning($"[ArtForge] {id}: material '{name}' was not loaded. Nearby: {NearbyNames(loaded, name)}");
        found = null!;
        return false;
    }

    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["iron"] = new[] { "Iron", "IronScrap", "AxeIron", "PickaxeIron", "SwordIron", "ArmorIronChest", "HelmetIron" },
        ["wood"] = new[] { "Wood", "FineWood", "RoundLog" },
        ["bronze"] = new[] { "Bronze", "AxeBronze", "PickaxeBronze" },
        ["blackmetal"] = new[] { "BlackMetal", "AxeBlackMetal", "PickaxeBlackMetal" },
        ["silver"] = new[] { "Silver", "AxeSilver", "SwordSilver" },
        ["flametal"] = new[] { "Flametal", "AxeFlametal", "SwordFlametal" },
    };

    private static Material? MaterialFromPrefab(string prefabName)
    {
        var prefab = PrefabManager.Instance.GetPrefab(prefabName);
        if (prefab == null)
            return null;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.sharedMaterial != null)
                return renderer.sharedMaterial;
        }

        return null;
    }

    private static string NearbyNames(Material[] loaded, string name)
    {
        if (loaded == null || string.IsNullOrWhiteSpace(name))
            return "(none)";
        var hits = new List<string>();
        foreach (var material in loaded)
        {
            if (material == null)
                continue;
            var cleaned = StripInstance(material.name);
            if (cleaned.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            hits.Add(cleaned);
            if (hits.Count == 8)
                break;
        }

        return hits.Count == 0 ? "(none with that text)" : string.Join(", ", hits);
    }

    private static bool TryFindLoadedMaterial(Material[] loaded, string name, out Material found)
    {
        found = null!;
        var key = StripInstance(name);
        if (string.IsNullOrEmpty(key) || loaded == null)
            return false;

        Material? contains = null;
        var containsCount = 0;
        foreach (var material in loaded)
        {
            if (material == null)
                continue;
            var cleaned = StripInstance(material.name);
            if (string.Equals(cleaned, key, StringComparison.OrdinalIgnoreCase))
            {
                found = material;
                return true;
            }

            if (cleaned.IndexOf(key, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            contains = material;
            containsCount++;
        }

        if (containsCount == 1 && contains != null)
        {
            found = contains;
            return true;
        }

        return false;
    }

    private static string StripInstance(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "";
        const string suffix = " (Instance)";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name.Substring(0, name.Length - suffix.Length) : name;
    }

    private static float SanitizeScale(float scale) =>
        scale <= 0f || float.IsNaN(scale) || float.IsInfinity(scale) ? 1f : scale;

    private static string? ReadString(string json, string key)
    {
        var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"((?:\\\\.|[^\"\\\\])*)\"");
        return match.Success ? Regex.Unescape(match.Groups[1].Value) : null;
    }

    private static float? ReadFloat(string json, string key)
    {
        var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+(?:\\.\\d+)?)");
        if (!match.Success)
            return null;
        return float.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string[] ReadStringArray(string json, string key)
    {
        var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
        if (!match.Success)
            return Array.Empty<string>();

        var names = new List<string>();
        foreach (Match item in Regex.Matches(match.Groups[1].Value, "\"((?:\\\\.|[^\"\\\\])*)\""))
        {
            if (!string.IsNullOrWhiteSpace(item.Groups[1].Value))
                names.Add(Regex.Unescape(item.Groups[1].Value));
        }

        return names.ToArray();
    }
}
