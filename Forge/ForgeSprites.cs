using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DrakeModsLibs.Forge;

/// <summary>
/// Flat images on models (Drakes Asset Forge "sprites"): a quad with its pivot at the bottom centre, facing +Z
/// (forward) before rotation, transparent pixels cut out. Two-sided adds a mirrored back face so text reads
/// correctly from behind. Meshes and materials are built once and shared.
/// </summary>
public static class ForgeSprites
{
    private static readonly Dictionary<(float, float, bool), Mesh> Meshes = new();
    private static readonly Dictionary<Texture2D, Material> Materials = new();
    private static Material? _template;

    /// <summary>Adds a sprite under <paramref name="parent"/> and returns it (MeshFilter + MeshRenderer, no collider).</summary>
    public static GameObject Add(Transform parent, Texture2D texture, float width, float height, Vector3 position, Vector3 rotation,
        bool doubleSided = true, string name = "forge_sprite")
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(rotation);
        go.AddComponent<MeshFilter>().sharedMesh = Quad(width, height, doubleSided);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = Material(texture);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        return go;
    }

    /// <summary>The shared cutout material for <paramref name="texture"/> (built on first use).</summary>
    public static Material Material(Texture2D texture)
    {
        if (Materials.TryGetValue(texture, out var cached) && cached != null)
            return cached;
        texture.wrapMode = TextureWrapMode.Clamp;
        var material = new Material(Template()) { name = "forge_sprite_" + texture.name };
        material.SetTexture("_MainTex", texture);
        material.SetColor("_Color", Color.white);
        foreach (var slot in new[] { "_BumpMap", "_MetallicGlossMap", "_EmissionMap", "_OcclusionMap", "_DetailAlbedoMap" })
            if (material.HasProperty(slot))
                material.SetTexture(slot, null);
        material.DisableKeyword("_NORMALMAP");
        material.DisableKeyword("_EMISSION");
        material.DisableKeyword("_METALLICGLOSSMAP");
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", 0.15f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", 0.5f);
        Materials[texture] = material;
        return material;
    }

    /// <summary>Front faces +Z (clockwise winding = Unity's front); the back face's UVs are mirrored.</summary>
    public static Mesh Quad(float width, float height, bool doubleSided)
    {
        var key = (width, height, doubleSided);
        if (Meshes.TryGetValue(key, out var cached) && cached != null)
            return cached;
        var w = width / 2f;
        var vertices = new List<Vector3> { new(-w, 0, 0), new(-w, height, 0), new(w, height, 0), new(w, 0, 0) };
        var normals = Enumerable.Repeat(Vector3.forward, 4).ToList();
        var uvs = new List<Vector2> { new(1, 0), new(1, 1), new(0, 1), new(0, 0) };
        var triangles = new List<int> { 2, 1, 0, 2, 0, 3 };
        if (doubleSided)
        {
            vertices.AddRange(vertices.ToList());
            normals.AddRange(Enumerable.Repeat(Vector3.back, 4));
            uvs.AddRange(new Vector2[] { new(0, 0), new(0, 1), new(1, 1), new(1, 0) });
            triangles.AddRange(new[] { 4, 5, 6, 4, 6, 7 });
        }

        var mesh = new Mesh { name = "forge_sprite" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        Meshes[key] = mesh;
        return mesh;
    }

    /// <summary>A vanilla Standard cutout material, so the shader variant is certainly in the game build.</summary>
    private static Material Template()
    {
        if (_template != null)
            return _template;
        _template = Resources.FindObjectsOfTypeAll<Material>()
            .FirstOrDefault(m => m != null && m.shader != null && m.shader.name == "Standard" && m.IsKeywordEnabled("_ALPHATEST_ON"));
        if (_template == null)
        {
            Jotunn.Logger.LogInfo("[Forge] No vanilla cutout material found; using Standard set to cutout.");
            _template = new Material(Shader.Find("Standard"));
            _template.SetFloat("_Mode", 1f);
            _template.EnableKeyword("_ALPHATEST_ON");
            _template.SetOverrideTag("RenderType", "TransparentCutout");
            _template.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }

        return _template;
    }
}
