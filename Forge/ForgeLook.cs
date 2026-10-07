using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Jotunn.Configs;
using Jotunn.Managers;
using UnityEngine;
using Logger = Jotunn.Logger;

namespace DrakeModsLibs.Forge;

/// <summary>
/// What Drakes Asset Forge's "plain C#" export calls to dress a prefab: materials, borrowed meshes, sprites,
/// icons, fire/light colours, snap points, components. Plain static helpers: nothing runs until a mod calls them.
/// </summary>
public static class ForgeLook
{
    // ---- icons & sprites ----

    /// <summary>Sets the item's or piece's icon from an image in <paramref name="owner"/> (see <see cref="ForgeTextures"/>).</summary>
    public static void Icon(Assembly owner, GameObject prefab, string file)
    {
        if (ForgeTextures.Sprite(owner, file) is not { } sprite)
            return;
        if (prefab.GetComponent<ItemDrop>() is { } drop)
            drop.m_itemData.m_shared.m_icons = new[] { sprite };
        else if (prefab.GetComponent<Piece>() is { } piece)
            piece.m_icon = sprite;
    }

    /// <summary>A flat image under <paramref name="parent"/>; see <see cref="ForgeSprites"/>.</summary>
    public static GameObject? AddSprite(Assembly owner, Transform parent, string file, float width, float height, Vector3 position, Vector3 rotation,
        bool doubleSided = true, string name = "forge_sprite")
    {
        var texture = ForgeTextures.Load(owner, file);
        return texture == null ? null : ForgeSprites.Add(parent, texture, width, height, position, rotation, doubleSided, name);
    }

    /// <summary>Stops the model's own meshes drawing (colliders stay), e.g. when sprites replace it.</summary>
    public static void HideMesh(GameObject prefab)
    {
        foreach (var renderer in Renderers(prefab))
            renderer.enabled = false;
    }

    // ---- materials ----

    /// <summary>
    /// Replaces matching materials (by name, by slot, or every one when both are null) with an edited copy, starting
    /// from <paramref name="fromPrefab"/>/<paramref name="fromMaterial"/> when given, else from the original.
    /// </summary>
    public static void Material(GameObject prefab, string? target, int? slot, string? fromPrefab, string? fromMaterial, string? shader, Action<Material>? edit)
    {
        var built = new Dictionary<Material, Material>();
        var index = 0;
        var matched = false;
        foreach (var renderer in Renderers(prefab))
        {
            var materials = renderer.sharedMaterials;
            var changed = false;
            for (var i = 0; i < materials.Length; i++, index++)
            {
                var original = materials[i];
                if (original == null)
                    continue;
                // Match on the original name, also after an earlier edit renamed it "<name>_forge".
                var name = MaterialName(original);
                if (name.EndsWith("_forge", StringComparison.Ordinal))
                    name = name.Substring(0, name.Length - "_forge".Length);
                if (target != null ? name != target : slot.HasValue && slot.Value != index)
                    continue;
                matched = true;
                if (!built.TryGetValue(original, out var result))
                    built[original] = result = Build(Source(fromPrefab, fromMaterial) ?? original, shader, edit);
                materials[i] = result;
                changed = true;
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }

        if (!matched)
            Logger.LogWarning($"[Forge] {prefab.name}: no material {(target ?? (slot.HasValue ? "slot " + slot : "at all"))}");
    }

    /// <summary>Chest/legs armour: the material Valheim paints on the player's body when worn.</summary>
    public static void ArmorMaterial(GameObject prefab, string? fromPrefab, string? fromMaterial, string? shader, Action<Material>? edit)
    {
        var shared = prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared;
        if (shared?.m_armorMaterial == null)
        {
            Logger.LogWarning($"[Forge] {prefab.name}: no body armour material (only chest and leg armour have one)");
            return;
        }

        Material? source = null;
        if (fromPrefab != null)
            source = PrefabManager.Instance.GetPrefab(fromPrefab)?.GetComponent<ItemDrop>()?.m_itemData?.m_shared?.m_armorMaterial;
        else if (fromMaterial != null)
            source = PrefabManager.Cache.GetPrefab<Material>(fromMaterial);
        shared.m_armorMaterial = Build(source ?? shared.m_armorMaterial, shader, edit);
    }

    public static void SetTexture(Assembly owner, Material material, string property, string file)
    {
        var normal = property.IndexOf("Bump", StringComparison.OrdinalIgnoreCase) >= 0 || property.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0;
        if (ForgeTextures.Load(owner, file, normal) is { } texture)
            material.SetTexture(property, texture);
    }

    /// <summary>Glow colour (HDR: channels above 1 are brighter). Also turns emission on for materials that had none.</summary>
    public static void Emission(Material material, Color color)
    {
        material.SetColor("_EmissionColor", color);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
    }

    private static Material Build(Material source, string? shader, Action<Material>? edit)
    {
        if (shader == null && edit == null)
            return source;
        var material = new Material(source) { name = MaterialName(source) + "_forge" };
        if (shader != null)
        {
            if (Shader.Find(shader) is { } found)
                material.shader = found;
            else
                Logger.LogWarning($"[Forge] Shader {shader} not found");
        }

        edit?.Invoke(material);
        return material;
    }

    private static Material? Source(string? fromPrefab, string? fromMaterial)
    {
        if (fromPrefab != null)
        {
            var materials = PrefabManager.Instance.GetPrefab(fromPrefab) is { } prefab
                ? Renderers(prefab).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList()
                : new List<Material>();
            return fromMaterial == null ? materials.FirstOrDefault() : materials.FirstOrDefault(m => MaterialName(m) == fromMaterial);
        }

        return fromMaterial != null ? PrefabManager.Cache.GetPrefab<Material>(fromMaterial) : null;
    }

    // ---- shape ----

    /// <summary>Shows another prefab's (highest-detail) static meshes instead of this one's.</summary>
    public static void BorrowMesh(GameObject prefab, string sourceName)
    {
        var source = PrefabManager.Instance.GetPrefab(sourceName);
        if (source == null)
        {
            Logger.LogWarning($"[Forge] {prefab.name}: mesh source {sourceName} not found");
            return;
        }

        var lod = source.GetComponentInChildren<LODGroup>(true);
        var sourceRenderers = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers.Where(r => r != null) : Renderers(source);
        HideMesh(prefab);
        var visual = new GameObject("forge_visual");
        visual.transform.SetParent(prefab.transform, false);
        var root = source.transform;
        foreach (var renderer in sourceRenderers)
        {
            if (renderer is not MeshRenderer || renderer.GetComponent<MeshFilter>() is not { } filter)
                continue;
            var part = new GameObject(renderer.name);
            part.transform.SetParent(visual.transform, false);
            part.transform.localPosition = root.InverseTransformPoint(renderer.transform.position);
            part.transform.localRotation = Quaternion.Inverse(root.rotation) * renderer.transform.rotation;
            var rootScale = root.lossyScale;
            var scale = renderer.transform.lossyScale;
            part.transform.localScale = new Vector3(scale.x / rootScale.x, scale.y / rootScale.y, scale.z / rootScale.z);
            part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
    }

    /// <summary>Where the visible model lives: items show <c>attach</c> both dropped and held, pieces their root.</summary>
    public static Transform VisualRoot(GameObject prefab) =>
        prefab.GetComponent<ItemDrop>() != null && prefab.transform.Find("attach") is { } attach ? attach : prefab.transform;

    /// <summary>Scales the whole model (on items the held/dropped visual, on pieces the root and its collision).</summary>
    public static void Scale(GameObject prefab, Vector3 scale)
    {
        var root = VisualRoot(prefab);
        root.localScale = Vector3.Scale(root.localScale, scale);
    }

    /// <summary>
    /// Kitbashing: copies another vanilla prefab's highest-detail meshes onto this model and returns the new part
    /// (use <see cref="Material"/> on it to restyle just that part). Position/rotation/scale are in the prefab's own
    /// space; <paramref name="child"/> keeps only meshes whose name (or a parent's) contains it.
    /// </summary>
    public static GameObject? AddPart(GameObject prefab, string sourcePrefab, string? child, Vector3 position, Vector3 rotation, Vector3 scale)
    {
        var source = PrefabManager.Instance.GetPrefab(sourcePrefab);
        if (source == null)
        {
            Logger.LogWarning($"[Forge] {prefab.name}: part source {sourcePrefab} not found");
            return null;
        }

        var visualRoot = VisualRoot(prefab);
        var holder = visualRoot.Find("forge_parts");
        if (holder == null)
        {
            holder = new GameObject("forge_parts").transform;
            holder.SetParent(visualRoot, false);
            holder.position = prefab.transform.position;
            holder.rotation = prefab.transform.rotation;
            holder.localScale = Vector3.one;
        }

        var part = new GameObject($"part{holder.childCount}_{sourcePrefab}");
        part.transform.SetParent(holder, false);
        part.transform.localPosition = position;
        part.transform.localRotation = Quaternion.Euler(rotation);
        part.transform.localScale = scale;

        var lod = source.GetComponentInChildren<LODGroup>(true);
        var renderers = lod != null && lod.GetLODs().Length > 0 ? lod.GetLODs()[0].renderers.Where(r => r != null) : Renderers(source);
        var root = source.transform;
        var copied = 0;
        foreach (var renderer in renderers)
        {
            var mesh = renderer switch
            {
                MeshRenderer when renderer.GetComponent<MeshFilter>() is { } filter => filter.sharedMesh,
                SkinnedMeshRenderer skinned => skinned.sharedMesh,
                _ => null
            };
            if (mesh == null || (child != null && !NameMatches(renderer.transform, root, child)))
                continue;
            var copy = new GameObject(renderer.name);
            copy.transform.SetParent(part.transform, false);
            copy.transform.localPosition = root.InverseTransformPoint(renderer.transform.position);
            copy.transform.localRotation = Quaternion.Inverse(root.rotation) * renderer.transform.rotation;
            var rootScale = root.lossyScale;
            var s = renderer.transform.lossyScale;
            copy.transform.localScale = new Vector3(s.x / rootScale.x, s.y / rootScale.y, s.z / rootScale.z);
            copy.AddComponent<MeshFilter>().sharedMesh = mesh;
            copy.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
            copied++;
        }

        if (copied == 0)
            Logger.LogWarning($"[Forge] {prefab.name}: no meshes in {sourcePrefab}{(child != null ? $" named like '{child}'" : "")}");
        return part;
    }

    private static bool NameMatches(Transform t, Transform root, string child)
    {
        for (; t != null && t != root; t = t.parent)
            if (t.name.IndexOf(child, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        return false;
    }

    /// <summary>Valheim snap points: direct children tagged "snappoint". <paramref name="replace"/> removes the existing ones first.</summary>
    public static void SnapPoints(GameObject piece, bool replace, params Vector3[] points)
    {
        var existing = new List<Transform>();
        foreach (Transform child in piece.transform)
            if (child.CompareTag("snappoint"))
                existing.Add(child);
        var active = existing.Count > 0 && existing[0].gameObject.activeSelf;
        if (replace)
            foreach (var child in existing)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        foreach (var p in points)
        {
            var point = new GameObject("_snappoint") { tag = "snappoint" };
            point.transform.SetParent(piece.transform, false);
            point.transform.localPosition = p;
            point.SetActive(active);
        }
    }

    // ---- components ----

    public static void Remove<T>(GameObject prefab) where T : Component
    {
        foreach (var component in prefab.GetComponents<T>())
            UnityEngine.Object.DestroyImmediate(component);
        if (prefab.GetComponent<T>() != null)
            Logger.LogWarning($"[Forge] {prefab.name}: Unity kept {typeof(T).Name} (another component requires it)");
    }

    public static T Add<T>(GameObject prefab) where T : Component => prefab.GetComponent<T>() ?? prefab.AddComponent<T>();

    // ---- fire & lights ----

    /// <summary>Every Light (except Forge's glow) and particle effect: colour, brightness and reach multipliers, flame tint.</summary>
    public static void Effects(GameObject prefab, Color? lightColor, float intensity, float range, Color? flameTint)
    {
        var glow = prefab.transform.Find("forge_glow");
        foreach (var light in prefab.GetComponentsInChildren<Light>(true))
        {
            if (glow != null && light.transform.IsChildOf(glow))
                continue;
            if (lightColor is { } c)
                light.color = new Color(c.r, c.g, c.b, light.color.a);
            light.intensity *= intensity;
            light.range *= range;
        }

        if (flameTint is not { } tint)
            return;
        foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(tint.r, tint.g, tint.b, main.startColor.color.a));
            var col = ps.colorOverLifetime;
            if (!col.enabled)
                continue;
            // Keep the gradient's alpha (the fade), swap its colours.
            var alpha = col.color.mode == ParticleSystemGradientMode.Gradient ? col.color.gradient.alphaKeys : new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) };
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(tint, 0f), new GradientColorKey(tint, 1f) }, alpha);
            col.color = new ParticleSystem.MinMaxGradient(gradient);
        }
    }

    /// <summary>A point light on the item (torch-like glow).</summary>
    public static void Glow(GameObject prefab, Color color, float intensity, float range, Vector3 offset, bool nightOnly)
    {
        var glow = new GameObject("forge_glow");
        glow.transform.SetParent(prefab.transform, false);
        glow.transform.localPosition = offset;
        var light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.shadows = LightShadows.None;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        if (nightOnly)
            glow.AddComponent<ForgeNightOnlyLight>();
    }

    // ---- names ----

    /// <summary>Friendly station names ("workbench", "forge"…) to Valheim prefab names; anything else passes through.</summary>
    public static string? Station(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        null or "" or "none" => null,
        "workbench" => CraftingStations.Workbench,
        "forge" => CraftingStations.Forge,
        "stonecutter" => CraftingStations.Stonecutter,
        "cauldron" => CraftingStations.Cauldron,
        "artisan" or "artisantable" => CraftingStations.ArtisanTable,
        "blackforge" => CraftingStations.BlackForge,
        "galdr" or "galdrtable" => CraftingStations.GaldrTable,
        "meadketill" => CraftingStations.MeadKetill,
        "preptable" or "foodpreparationtable" => CraftingStations.FoodPreparationTable,
        _ => name
    };

    /// <summary>"hammer", "hoe", "cultivator", "servingtray" to Valheim piece tables; anything else passes through.</summary>
    public static string PieceTable(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        null or "" or "hammer" => PieceTables.Hammer,
        "hoe" => PieceTables.Hoe,
        "cultivator" => PieceTables.Cultivator,
        "servingtray" => PieceTables.ServingTray,
        _ => name!
    };

    private static IEnumerable<Renderer> Renderers(GameObject root) =>
        root.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer);

    private static string MaterialName(Material material) => material.name.Replace(" (Instance)", "").Trim();
}

/// <summary>Turns a glow light on only at night (added by <see cref="ForgeLook.Glow"/>).</summary>
public sealed class ForgeNightOnlyLight : MonoBehaviour
{
    private Light? _light;
    private float _nextCheck;

    private void Awake() => _light = GetComponent<Light>();

    private void Update()
    {
        if (_light == null || Time.time < _nextCheck)
            return;
        _nextCheck = Time.time + 2f;
        _light.enabled = EnvMan.IsNight();
    }
}
