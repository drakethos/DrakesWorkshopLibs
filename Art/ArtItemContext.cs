using System;
using System.Collections.Generic;
using UnityEngine;

namespace DrakeModsLibs.Art;

/// <summary>
/// One exported art item, after the editor and BepInEx config have been applied.
/// A generated customize method can change these fields. The loader applies them.
/// </summary>
public sealed class ArtItemContext
{
    public string Id { get; internal set; } = "";
    public string SourceId { get; internal set; } = "";
    public string Donor { get; internal set; } = "";
    public string Folder { get; internal set; } = "";

    public string DisplayName { get; set; } = "";

    /// <summary>Null or empty keeps the donor prefab description.</summary>
    public string? Description { get; set; }

    public float Scale { get; set; } = 1f;
    public string RequirementItem { get; set; } = "Wood";
    public int RequirementAmount { get; set; } = 1;

    /// <summary>Null means inventory craft, like a torch. Set a station prefab name to require one.</summary>
    public string? CraftingStation { get; set; }

    public List<string> Materials { get; } = new();

    public GameObject? Prefab { get; internal set; }
    public ItemDrop? Drop { get; internal set; }

    public void SetMaterials(params string[] names)
    {
        Materials.Clear();
        if (names == null)
            return;
        foreach (var name in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
                Materials.Add(name.Trim());
        }
    }
}
