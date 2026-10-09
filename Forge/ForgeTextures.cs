using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace DrakeModsLibs.Forge;

/// <summary>
/// Images for Drakes Asset Forge items, loaded once and cached. Looks inside the calling mod's DLL first
/// (embedded resources: immune to Hexium/Gale flattening plugin folders), then for a loose file with that name
/// anywhere under the mod's folder.
/// </summary>
public static class ForgeTextures
{
    private static readonly Dictionary<string, Texture2D?> Textures = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Sprite?> Sprites = new(StringComparer.OrdinalIgnoreCase);

    // Unity 6 adds a ReadOnlySpan overload net481 can't compile against; bind the byte[] one explicitly.
    private static readonly Func<Texture2D, byte[], bool> LoadImage = (Func<Texture2D, byte[], bool>)Delegate.CreateDelegate(
        typeof(Func<Texture2D, byte[], bool>),
        typeof(ImageConversion).GetMethod(nameof(ImageConversion.LoadImage), new[] { typeof(Texture2D), typeof(byte[]) })!);

    /// <param name="owner">The mod's assembly (<c>typeof(MyPlugin).Assembly</c>): where embedded images live.</param>
    /// <param name="file">Pack-relative path such as "textures/paper_sheet.png"; only the file name has to match.</param>
    /// <param name="normalMap">Linear and repacked to Unity's (A = x, G = y) layout, as a PNG isn't imported as a normal map at runtime.</param>
    public static Texture2D? Load(Assembly owner, string file, bool normalMap = false)
    {
        var key = owner.GetName().Name + "|" + file + (normalMap ? "|normal" : "");
        if (Textures.TryGetValue(key, out var cached))
            return cached;

        var bytes = ReadEmbedded(owner, file) ?? ReadLoose(owner, file);
        Texture2D? texture = null;
        if (bytes == null)
        {
            Jotunn.Logger.LogWarning($"[Forge] {owner.GetName().Name}: image {file} not found (embedded or beside the DLL).");
        }
        else
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, normalMap) { name = Path.GetFileNameWithoutExtension(file) };
            if (!LoadImage(texture, bytes))
            {
                Jotunn.Logger.LogWarning($"[Forge] {file} is not a readable PNG or JPG.");
                texture = null;
            }
            else if (normalMap)
            {
                var pixels = texture.GetPixels32();
                for (var i = 0; i < pixels.Length; i++)
                    pixels[i] = new Color32(255, pixels[i].g, pixels[i].g, pixels[i].r);
                texture.SetPixels32(pixels);
                texture.Apply(true);
            }
        }

        Textures[key] = texture;
        return texture;
    }

    /// <summary>A PNG/JPG already in memory (e.g. a .glb's embedded texture). Not cached.</summary>
    internal static Texture2D? FromBytes(byte[] bytes, string name)
    {
        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = name };
        return LoadImage(texture, bytes) ? texture : null;
    }

    /// <summary>The image as a centred sprite (item and piece icons). Cached.</summary>
    public static Sprite? Sprite(Assembly owner, string file)
    {
        var key = owner.GetName().Name + "|" + file;
        if (Sprites.TryGetValue(key, out var cached))
            return cached;
        var texture = Load(owner, file);
        Sprite? sprite = null;
        if (texture != null)
        {
            sprite = UnityEngine.Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
        }

        Sprites[key] = sprite;
        return sprite;
    }

    /// <summary>Embedded resources are named "&lt;RootNamespace&gt;.&lt;folders&gt;.&lt;file&gt;"; match the file name at the end.</summary>
    internal static byte[]? ReadEmbedded(Assembly owner, string file)
    {
        var leaf = Path.GetFileName(file.Replace('\\', '/'));
        var name = owner.GetManifestResourceNames().FirstOrDefault(n => n.Equals(leaf, StringComparison.OrdinalIgnoreCase) ||
                                                                         n.EndsWith("." + leaf, StringComparison.OrdinalIgnoreCase));
        if (name == null)
            return null;
        using var stream = owner.GetManifestResourceStream(name);
        if (stream == null)
            return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    /// <summary>A loose file: the exact relative path first, then the same file name anywhere under the mod's folder.</summary>
    internal static byte[]? ReadLoose(Assembly owner, string file)
    {
        var dir = Path.GetDirectoryName(owner.Location);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            return null;
        var exact = Path.Combine(dir, file.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(exact))
            return File.ReadAllBytes(exact);
        var leaf = Path.GetFileName(exact);
        var found = Directory.EnumerateFiles(dir, leaf, SearchOption.AllDirectories).FirstOrDefault();
        return found != null ? File.ReadAllBytes(found) : null;
    }
}
