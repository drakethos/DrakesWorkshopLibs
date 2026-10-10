using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrakeModsLibs.Display;

/// <summary>
/// A reskinned item's colour as it looks lying on the ground, on an item stand, a rack or a wall: the model tint goes onto every
/// renderer the way worn equipment gets it (a property block, so shared materials stay untouched).
/// </summary>
public static class WorldLook
{
    static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly HashSet<int> Tinted = new HashSet<int>(); // renderer instance ids we have coloured

    /// <summary>
    /// Colour every renderer under <paramref name="root"/> with <paramref name="tint"/> (null puts back what we changed).
    /// <paramref name="skip"/> leaves chosen renderers alone (ink, glow).
    /// </summary>
    public static void Tint(GameObject? root, Color? tint, Func<Renderer, bool>? skip = null)
    {
        if (!root)
            return;
        foreach (var renderer in root!.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer || (skip != null && skip(renderer)))
                continue;
            var id = renderer.GetInstanceID();
            if (tint == null && !Tinted.Contains(id))
                continue;
            renderer.GetPropertyBlock(Block);
            Block.SetColor(ColorId, tint ?? Color.white);
            renderer.SetPropertyBlock(Block);
            if (tint == null)
            {
                Tinted.Remove(id);
            }
            else
            {
                if (Tinted.Count > 8192)
                    Tinted.Clear(); // ids of destroyed renderers
                Tinted.Add(id);
            }
        }
    }

    /// <summary>Tint <paramref name="root"/> with what <paramref name="item"/> carries (its model tint), if anything.</summary>
    public static void TintFor(GameObject? root, ItemDrop.ItemData? item, Func<Renderer, bool>? skip = null)
    {
        var tint = ItemLookService.GetModelTint(item);
        if (tint != null)
            Tint(root, tint, skip);
    }
}
