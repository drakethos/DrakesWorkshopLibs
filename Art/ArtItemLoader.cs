using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
/// Loads Assets/Items beside a plugin. Supports both layouts:
/// <list type="bullet">
/// <item>Folder pack: Assets/Items/keys/keys.bundle + keymaker.json, masterkey.json, …</item>
/// <item>Legacy: Assets/Items/keymaker/item.json + art.bundle</item>
/// </list>
/// Call <see cref="Register"/> and pass a customize hook if you want to change name, description, scale, or materials in code.
/// </summary>
public static class ArtItemLoader
{
    /// <summary>
    /// Unity refuses to LoadFromFile the same AssetBundle path twice. Folder packs share one
    /// keys.bundle across many JSON items — cache by full path for the process lifetime.
    /// </summary>
    private static readonly Dictionary<string, object> BundleCache =
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

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
        foreach (var entry in EnumerateItemEntries(itemsRoot))
        {
            try
            {
                if (AddItem(log, entry, config, customize))
                    registered++;
            }
            catch (Exception ex)
            {
                log?.LogError($"[ArtForge] Failed '{entry.Id}': {ex.Message}");
            }
        }

        log?.LogInfo($"[ArtForge] Registered {registered} item(s) from {itemsRoot}");
    }

    private sealed class ItemEntry
    {
        public string Id = "";
        public string WirePath = "";
        public string Folder = "";
        public string? BundlePath;
        public string? IconPath;
        public string PrefabInBundle = "art";
    }

    private static IEnumerable<ItemEntry> EnumerateItemEntries(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        foreach (var dir in Directory.EnumerateDirectories(root))
        {
            var folderName = Path.GetFileName(dir);

            // Folder pack: keys/keys.bundle + keymaker.json, masterkey.json, …
            var folderBundle = Path.Combine(dir, folderName + ".bundle");
            if (!File.Exists(folderBundle))
            {
                folderBundle = Directory.EnumerateFiles(dir, "*.bundle", SearchOption.TopDirectoryOnly)
                    .FirstOrDefault(f => !Path.GetFileName(f).Equals("art.bundle", StringComparison.OrdinalIgnoreCase));
            }

            var packJsons = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
                .Where(j => !Path.GetFileName(j).Equals("item.json", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (folderBundle != null && File.Exists(folderBundle) && packJsons.Count > 0)
            {
                foreach (var json in packJsons)
                {
                    var leaf = Path.GetFileNameWithoutExtension(json);
                    var icon = Path.Combine(dir, leaf + ".png");
                    if (!File.Exists(icon))
                        icon = Path.Combine(dir, leaf + "_icon.png");

                    yield return new ItemEntry
                    {
                        Id = leaf,
                        WirePath = json,
                        Folder = dir,
                        BundlePath = folderBundle,
                        IconPath = File.Exists(icon) ? icon : null,
                        PrefabInBundle = leaf,
                    };
                }

                continue;
            }

            // Legacy: …/keymaker/item.json (+ art.bundle)
            var legacyWire = Path.Combine(dir, "item.json");
            if (File.Exists(legacyWire))
            {
                var legacyBundle = Path.Combine(dir, "art.bundle");
                if (!File.Exists(legacyBundle))
                {
                    var named = Path.Combine(dir, folderName + ".bundle");
                    if (File.Exists(named))
                        legacyBundle = named;
                    else
                        legacyBundle = null;
                }

                var icon = Path.Combine(dir, "icon.png");
                yield return new ItemEntry
                {
                    Id = folderName,
                    WirePath = legacyWire,
                    Folder = dir,
                    BundlePath = legacyBundle != null && File.Exists(legacyBundle) ? legacyBundle : null,
                    IconPath = File.Exists(icon) ? icon : null,
                    PrefabInBundle = "art",
                };
                continue;
            }

            foreach (var nested in EnumerateItemEntries(dir))
                yield return nested;
        }
    }

    private static bool AddItem(
        ManualLogSource log,
        ItemEntry entry,
        ConfigFile? config,
        Action<ArtItemContext>? customize)
    {
        if (!File.Exists(entry.WirePath))
        {
            log?.LogWarning($"[ArtForge] Skip {entry.Id} — wire json missing.");
            return false;
        }

        var wire = File.ReadAllText(entry.WirePath);
        var id = ReadString(wire, "id") ?? entry.Id;
        var donor = ReadString(wire, "donor");
        if (string.IsNullOrWhiteSpace(donor))
        {
            log?.LogError($"[ArtForge] {id}: json is missing donor.");
            return false;
        }

        var prefabInBundle = ReadString(wire, "artPrefab")
                             ?? ReadString(wire, "prefabInBundle")
                             ?? entry.PrefabInBundle;

        var context = new ArtItemContext
        {
            Id = id,
            SourceId = ReadString(wire, "sourceId") ?? id,
            Donor = donor,
            Folder = entry.Folder,
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
        ApplyIcon(log, entry.IconPath, drop);
        if (context.UseDonorVisual)
        {
            // Keep CryptKey / swamp-key mesh; only retint when materials were requested.
            if (context.Materials.Count > 0 && item.ItemPrefab != null)
                ApplyExistingMaterials(log, item.ItemPrefab, context);
            log?.LogInfo($"[ArtForge] {context.Id}: UseDonorVisual — kept donor mesh.");
        }
        else if (!string.IsNullOrWhiteSpace(entry.BundlePath) && File.Exists(entry.BundlePath))
        {
            if (!AttachArtPrefab(log, item.ItemPrefab, entry.BundlePath, context, prefabInBundle))
                return false;
        }
        else
        {
            log?.LogInfo($"[ArtForge] {context.Id}: registered without art bundle (scripts/properties / icon only).");
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

    private static void ApplyIcon(ManualLogSource log, string? iconPath, ItemDrop drop)
    {
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
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

    private static object? LoadOrGetBundle(string bundlePath, ManualLogSource log)
    {
        if (string.IsNullOrWhiteSpace(bundlePath))
            return null;

        var key = Path.GetFullPath(bundlePath);
        if (BundleCache.TryGetValue(key, out var cached) && cached != null)
            return cached;

        var bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.AssetBundleModule");
        var load = bundleType?.GetMethod(
            "LoadFromFile",
            BindingFlags.Public | BindingFlags.Static,
            null,
            new[] { typeof(string) },
            null);
        var bundle = load?.Invoke(null, new object[] { key });
        if (bundle == null)
            return null;

        BundleCache[key] = bundle;
        return bundle;
    }

    private static bool AttachArtPrefab(
        ManualLogSource log,
        GameObject item,
        string bundlePath,
        ArtItemContext context,
        string preferredPrefabName)
    {
        var bundleType = Type.GetType("UnityEngine.AssetBundle, UnityEngine.AssetBundleModule");
        var bundle = LoadOrGetBundle(bundlePath, log);
        if (bundle == null || bundleType == null)
        {
            log?.LogError($"[ArtForge] {context.Id}: art bundle failed to load ({Path.GetFileName(bundlePath)}).");
            return false;
        }

        var loadAsset = bundleType.GetMethod("LoadAsset", new[] { typeof(string), typeof(Type) });
        GameObject? art = null;
        foreach (var name in PrefabNameCandidates(preferredPrefabName, context))
        {
            art = loadAsset?.Invoke(bundle, new object[] { name, typeof(GameObject) }) as GameObject;
            if (art != null)
                break;
        }

        if (art == null)
        {
            log?.LogError(
                $"[ArtForge] {context.Id}: bundle '{Path.GetFileName(bundlePath)}' has no prefab " +
                $"'{preferredPrefabName}' (also tried art / sourceId).");
            return false;
        }

        var visual = UnityEngine.Object.Instantiate(art, FindHoldAttach(item) ?? item.transform, false);
        visual.name = "art";
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one * context.Scale;
        OrientHeldArtForHand(log, visual);
        ApplyExistingMaterials(log, visual, context);
        ApplyBundleDiffuse(log, bundle, bundleType, loadAsset, visual, context);

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

    /// <summary>
    /// If the art / folder pack includes a diffuse Texture2D (compile-time PNG), stamp it onto
    /// the Valheim materials already applied so the custom albedo shows with a game shader.
    /// </summary>
    private static void ApplyBundleDiffuse(
        ManualLogSource log,
        object bundle,
        Type bundleType,
        MethodInfo? loadAsset,
        GameObject visual,
        ArtItemContext context)
    {
        var tex = FindBundleDiffuse(bundle, bundleType, loadAsset, context);
        if (tex == null)
            return;

        var stamped = 0;
        foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null)
                continue;

            // .materials clones shared mats so we don't mutate global Valheim iron/bronze.
            var mats = renderer.materials;
            for (var i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                if (mat == null)
                    continue;
                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", tex);
                    stamped++;
                }
                else if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", tex);
                    stamped++;
                }
            }

            renderer.materials = mats;
        }

        if (stamped > 0)
            log?.LogInfo($"[ArtForge] {context.Id}: stamped bundle diffuse '{tex.name}' on {stamped} material slot(s).");
    }

    private static Texture2D? FindBundleDiffuse(
        object bundle,
        Type bundleType,
        MethodInfo? loadAsset,
        ArtItemContext context)
    {
        var candidates = new[]
        {
            context.Id + "_diffuse",
            (context.SourceId ?? "") + "_diffuse",
            "diffuse",
            context.Id,
        };

        foreach (var name in candidates)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var tex = loadAsset?.Invoke(bundle, new object[] { name, typeof(Texture2D) }) as Texture2D;
            if (tex != null)
                return tex;
        }

        var loadAll = bundleType.GetMethod("LoadAllAssets", new[] { typeof(Type) });
        if (loadAll?.Invoke(bundle, new object[] { typeof(Texture2D) }) is not Array all)
            return null;

        Texture2D? matched = null;
        foreach (var obj in all)
        {
            if (obj is not Texture2D tex)
                continue;
            var n = tex.name ?? "";
            // Only this item's diffuse — never stamp another key's albedo onto KeyMaker / siblings.
            if (n.IndexOf(context.Id, StringComparison.OrdinalIgnoreCase) >= 0 &&
                n.IndexOf("diffuse", StringComparison.OrdinalIgnoreCase) >= 0)
                return tex;
            if (!string.IsNullOrEmpty(context.SourceId) &&
                n.IndexOf(context.SourceId, StringComparison.OrdinalIgnoreCase) >= 0 &&
                n.IndexOf("diffuse", StringComparison.OrdinalIgnoreCase) >= 0)
                matched ??= tex;
        }

        return matched;
    }

    /// <summary>
    /// Prefer the donor's hold/attach transform (OpenHold / attach) so the art sits in the open hand.
    /// </summary>
    private static Transform? FindHoldAttach(GameObject item)
    {
        Transform? ranked = null;
        var best = 0;
        foreach (var t in item.GetComponentsInChildren<Transform>(true))
        {
            if (t == null || t == item.transform)
                continue;
            var n = t.name;
            var score = 0;
            if (n.IndexOf("OpenHold", StringComparison.OrdinalIgnoreCase) >= 0)
                score = 3;
            else if (n.Equals("attach", StringComparison.OrdinalIgnoreCase))
                score = 2;
            else if (n.IndexOf("attach", StringComparison.OrdinalIgnoreCase) >= 0)
                score = 1;
            if (score > best)
            {
                best = score;
                ranked = t;
            }
        }

        return ranked;
    }

    /// <summary>
    /// Hand pose for held art (keys etc.): roll so the skull/detail face shows,
    /// then slide so the handle/grip sits on the hold attach and the teeth point out.
    /// Same approach as LockSmith's FixKeyHandAttach.
    /// </summary>
    private static void OrientHeldArtForHand(ManualLogSource log, GameObject visual)
    {
        try
        {
            if (!TryGetArtLocalBounds(visual, out var bounds))
                return;

            var size = bounds.size;
            var axis = 0;
            if (size.y > size[axis])
                axis = 1;
            if (size.z > size[axis])
                axis = 2;

            // Roll 180° around the shaft — skull face toward camera, not the underside.
            var roll = axis == 0
                ? Quaternion.Euler(180f, 0f, 0f)
                : axis == 1
                    ? Quaternion.Euler(0f, 180f, 0f)
                    : Quaternion.Euler(0f, 0f, 180f);
            visual.transform.localRotation = roll * visual.transform.localRotation;

            // Handle/grip at the hold point (hand). Teeth/bit at the far end of the shaft.
            const bool handleAtMax = false;
            var handleLocal = bounds.center;
            handleLocal[axis] = handleAtMax ? bounds.max[axis] : bounds.min[axis];

            var scaled = Vector3.Scale(handleLocal, visual.transform.localScale);
            visual.transform.localPosition -= visual.transform.localRotation * scaled;

            log?.LogInfo(
                $"[ArtForge] Hold orient '{visual.name}': shaft-roll180 axis={axis}, " +
                $"handle=min, bounds={size}.");
        }
        catch (Exception ex)
        {
            log?.LogWarning($"[ArtForge] Hold orient skipped: {ex.Message}");
        }
    }

    private static bool TryGetArtLocalBounds(GameObject visual, out Bounds bounds)
    {
        bounds = default;
        var first = true;
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter?.sharedMesh == null)
                continue;
            var b = filter.sharedMesh.bounds;
            // Mesh bounds are in filter-local space — convert into visual-local.
            var corners = new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };
            foreach (var c in corners)
            {
                var world = filter.transform.TransformPoint(c);
                var local = visual.transform.InverseTransformPoint(world);
                if (first)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        foreach (var skinned in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (skinned?.sharedMesh == null)
                continue;
            var b = skinned.localBounds;
            var corners = new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };
            foreach (var c in corners)
            {
                var world = skinned.transform.TransformPoint(c);
                var local = visual.transform.InverseTransformPoint(world);
                if (first)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return !first;
    }

    private static IEnumerable<string> PrefabNameCandidates(string preferred, ArtItemContext context)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
            yield return preferred.Trim();
        if (!string.IsNullOrWhiteSpace(context.Id) &&
            !context.Id.Equals(preferred, StringComparison.OrdinalIgnoreCase))
            yield return context.Id;
        if (!string.IsNullOrWhiteSpace(context.SourceId) &&
            !context.SourceId.Equals(preferred, StringComparison.OrdinalIgnoreCase) &&
            !context.SourceId.Equals(context.Id, StringComparison.OrdinalIgnoreCase))
            yield return context.SourceId;
        yield return "art";
    }

    private static void ApplyExistingMaterials(ManualLogSource log, GameObject visual, ArtItemContext context)
    {
        if (context.Materials.Count == 0)
        {
            // Held art with stripped mats is a black silhouette — use iron until Customize/config sets one.
            context.SetMaterials("iron");
            log?.LogInfo($"[ArtForge] {context.Id}: no materials set — defaulting to iron for visibility.");
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
