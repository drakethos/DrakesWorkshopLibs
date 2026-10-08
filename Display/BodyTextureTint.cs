using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace DrakeModsLibs.Display;

/// <summary>
/// Tints skin-tight armor. Valheim draws chest and leg armor (bear, wolf, troll leather, ...) as textures painted onto
/// the player's body material (<c>_ChestTex</c> / <c>_LegsTex</c>), not as separate models, so the renderer tint used
/// for attached models never reaches them. This swaps those textures for tinted copies, made once on the GPU + CPU
/// and cached per (texture, tint). The game puts its own texture back whenever the armor changes; the next update
/// sees that and tints again.
/// </summary>
internal static class BodyTextureTint
{
    static readonly int ChestTex = Shader.PropertyToID("_ChestTex");
    static readonly int LegsTex = Shader.PropertyToID("_LegsTex");

    // Bare skin shown when the slot is empty: never tint it (a chest tint must not dye the player).
    static readonly FieldInfo? EmptyBodyTexture = AccessTools.Field(typeof(VisEquipment), "m_emptyBodyTexture");

    const float EvictAfterSeconds = 120f;
    const float SweepEverySeconds = 15f;
    const int SweepAboveCount = 8;

    sealed class Entry
    {
        public Texture Original = null!;
        public Texture2D Tinted = null!;
        public int Packed;
        public float LastUsed;
    }

    static readonly Dictionary<(int Texture, int Packed), Entry> Cache = new Dictionary<(int, int), Entry>();
    static readonly Dictionary<int, Entry> ByTinted = new Dictionary<int, Entry>();
    static float _nextSweep;

    /// <summary>Every client, each visual update: bring the body's chest/legs textures in line with the slot tints.</summary>
    internal static void Apply(VisEquipment vis, int chestPacked, int legsPacked)
    {
        var body = vis.m_bodyModel;
        if (body == null)
            return;
        var empty = EmptyBodyTexture?.GetValue(vis) as Texture;
        ApplyOne(body, ChestTex, chestPacked, empty);
        ApplyOne(body, LegsTex, legsPacked, empty);
        Sweep();
    }

    static void ApplyOne(SkinnedMeshRenderer body, int property, int packed, Texture? empty)
    {
        var shared = body.sharedMaterial;
        if (shared == null || !shared.HasProperty(property))
            return;
        var current = shared.GetTexture(property);
        if (current == null)
            return;
        ByTinted.TryGetValue(current.GetInstanceID(), out var applied);

        if (packed == 0)
        {
            if (applied != null)
                body.material.SetTexture(property, applied.Original); // tint removed: restore the game's texture
            return;
        }

        if (applied != null && applied.Packed == packed)
        {
            applied.LastUsed = Time.time;
            return;
        }

        var original = applied?.Original ?? current;
        if (original == empty)
            return;
        var entry = GetOrCreate(original, packed);
        if (entry == null)
            return;
        entry.LastUsed = Time.time;
        // .material: the game already gave each player its own body material; never touch the shared one.
        body.material.SetTexture(property, entry.Tinted);
    }

    static Entry? GetOrCreate(Texture original, int packed)
    {
        var key = (original.GetInstanceID(), packed);
        if (Cache.TryGetValue(key, out var entry) && entry.Tinted != null)
            return entry;
        var tint = ItemLookService.UnpackTint(packed);
        if (tint == null)
            return null;
        var tinted = MakeTinted(original, tint.Value);
        if (tinted == null)
            return null;
        entry = new Entry { Original = original, Tinted = tinted, Packed = packed, LastUsed = Time.time };
        Cache[key] = entry;
        ByTinted[tinted.GetInstanceID()] = entry;
        return entry;
    }

    /// <summary>Readable copy of <paramref name="source"/> (game textures aren't readable) with RGB multiplied by the tint.</summary>
    static Texture2D? MakeTinted(Texture source, Color tint)
    {
        var width = source.width;
        var height = source.height;
        if (width <= 0 || height <= 0)
            return null;
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
            {
                name = source.name + "_drake_tint",
                wrapMode = source.wrapMode,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel,
            };
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            var pixels = tex.GetPixels32();
            for (var i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i];
                p.r = Scale(p.r, tint.r);
                p.g = Scale(p.g, tint.g);
                p.b = Scale(p.b, tint.b);
                pixels[i] = p; // alpha is the armor mask: keep it
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, true); // build mips, then drop the CPU copy
            return tex;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    static byte Scale(byte value, float factor) => (byte)Mathf.Clamp(Mathf.RoundToInt(value * factor), 0, 255);

    /// <summary>Free tinted copies nobody has shown for a while (applied ones are touched every visual update).</summary>
    static void Sweep()
    {
        if (Cache.Count <= SweepAboveCount || Time.time < _nextSweep)
            return;
        _nextSweep = Time.time + SweepEverySeconds;
        var stale = new List<(int, int)>();
        foreach (var kv in Cache)
            if (Time.time - kv.Value.LastUsed > EvictAfterSeconds)
                stale.Add(kv.Key);
        foreach (var key in stale)
        {
            var entry = Cache[key];
            Cache.Remove(key);
            if (entry.Tinted == null)
                continue;
            ByTinted.Remove(entry.Tinted.GetInstanceID());
            UnityEngine.Object.Destroy(entry.Tinted);
        }
    }
}
