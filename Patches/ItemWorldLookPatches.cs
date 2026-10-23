using System;
using System.Collections.Generic;
using DrakeModsLibs.Display;
using HarmonyLib;
using UnityEngine;

namespace DrakeModsLibs.Patches;

/// <summary>
/// Shows a reskinned item's model swap and colour in the world: on the ground (<see cref="ItemDrop"/>) and on item stands.
/// Items nobody reskinned are untouched and cost nothing beyond one dictionary lookup when they load.
/// </summary>
[HarmonyPatch]
internal static class ItemWorldLookPatches
{
    // ---- dropped items ----

    [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Load))]
    [HarmonyPostfix]
    static void DropLoaded(ItemDrop __instance)
    {
        try
        {
            var item = __instance.m_itemData;
            if (item?.m_customData == null || (ItemLookService.GetModelOverride(item) == null && ItemLookService.GetModelTint(item) == null))
                return;
            var look = __instance.GetComponent<DroppedLook>() ?? __instance.gameObject.AddComponent<DroppedLook>();
            look.Apply(item);
        }
        catch (Exception)
        {
            // never break item loading
        }
    }

    // ---- item stands ----
    // The stand keeps the REAL item hash: SetVisualItem's hash is what the stand drops when the item is taken off (and what
    // boss altars check), so swapping it turned a reskinned stone axe into a real flint axe. Only the visual's meshes change.

    static readonly System.Reflection.FieldInfo? VisualItem = AccessTools.Field(typeof(ItemStand), "m_visualItem");

    [HarmonyPatch(typeof(ItemStand), "SetVisualItem")]
    [HarmonyPostfix]
    static void StandLook(ItemStand __instance)
    {
        try
        {
            if (VisualItem?.GetValue(__instance) is not GameObject visual || !visual)
                return;
            var item = ItemStandPatch.TryGetStandItemForDisplay(__instance, out _);
            if (item?.m_customData == null || (ItemLookService.GetModelOverride(item) == null && ItemLookService.GetModelTint(item) == null))
                return;
            var look = visual.GetComponent<DroppedLook>() ?? visual.AddComponent<DroppedLook>();
            look.Apply(item, attachedOnly: true);
        }
        catch (Exception)
        {
            // never break stands
        }
    }
}

/// <summary>Added only to ground items (and item stand visuals) that carry a reskin: swaps the model's meshes once and colours it.</summary>
internal sealed class DroppedLook : MonoBehaviour
{
    string _signature = "";
    GameObject? _swapped;
    readonly List<Renderer> _hidden = new List<Renderer>();
    readonly List<LODGroup> _lods = new List<LODGroup>();

    bool _attachedOnly;

    /// <param name="attachedOnly">An item stand shows only the prefab's "attach" child, so the swap uses the source's attach child too.</param>
    public void Apply(ItemDrop.ItemData item, bool attachedOnly = false)
    {
        _attachedOnly = attachedOnly;
        var tint = ItemLookService.GetModelTint(item);
        var sig = (ItemLookService.GetModelOverride(item) ?? "") + "|" + ItemLookService.FormatColor(tint);
        if (sig == _signature)
            return;
        _signature = sig;

        Restore();
        if (ItemLookService.TryResolveModel(item, out var hash, out _) && ObjectDB.instance != null)
            Swap(ObjectDB.instance.GetItemPrefab(hash));

        WorldLook.Tint(gameObject, tint);
    }

    void Swap(GameObject? source)
    {
        if (!source)
            return;
        var reference = source!.transform;
        if (_attachedOnly)
        {
            var attach = source.transform.Find("attach");
            if (attach == null)
                return;
            reference = attach;
        }
        var parts = new List<MeshFilter>();
        var lodGroup = reference.GetComponent<LODGroup>();
        if (lodGroup != null && lodGroup.GetLODs().Length > 0)
        {
            foreach (var r in lodGroup.GetLODs()[0].renderers)
                if (r != null && r.GetComponent<MeshFilter>() is { sharedMesh: not null } mf)
                    parts.Add(mf);
        }
        else
        {
            foreach (var mf in reference.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.GetComponent<MeshRenderer>() != null)
                    parts.Add(mf);
        }

        if (parts.Count == 0)
            return;

        _swapped = new GameObject("drake_swapped_model");
        _swapped.transform.SetParent(transform, false);
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r.transform.IsChildOf(_swapped.transform) || !r.enabled)
                continue;
            r.enabled = false;
            _hidden.Add(r);
        }

        foreach (var lod in GetComponentsInChildren<LODGroup>(true))
        {
            if (!lod.enabled)
                continue;
            lod.enabled = false;
            _lods.Add(lod);
        }

        foreach (var mf in parts)
        {
            var go = new GameObject(mf.name);
            go.transform.SetParent(_swapped.transform, false);
            var m = reference.worldToLocalMatrix * mf.transform.localToWorldMatrix;
            go.transform.localPosition = m.GetColumn(3);
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mf.GetComponent<MeshRenderer>().sharedMaterials;
        }
    }

    void Restore()
    {
        if (_swapped)
            Destroy(_swapped);
        _swapped = null;
        foreach (var r in _hidden)
            if (r)
                r.enabled = true;
        _hidden.Clear();
        foreach (var l in _lods)
            if (l)
                l.enabled = true;
        _lods.Clear();
    }
}
