using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DrakeModsLibs.Display;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeModsLibs.Patches;

/// <summary>
/// Equipped-model swaps and tints.
/// <para>
/// Model: Valheim 1.0 picks equipped visuals by prefab hash (<c>VisEquipment.SetRightItem(itemHash, quality)</c> etc.).
/// The prefixes swap that hash for the override's, so the normal equipment sync carries it to every client.
/// </para>
/// <para>
/// Model tint: the owner writes one int per slot to the player's ZDO; every client tints the attached instances.
/// </para>
/// All game members are bound by name at runtime: the CI reference stubs predate 1.0's hash-based signatures and
/// the top-level <c>InventoryElement</c> class, and a direct call would throw MissingMethodException at JIT time.
/// </summary>
[HarmonyPatch]
internal static class ItemLookPatches
{
    // The Humanoid equipment fields are protected/private in the real game (only public in the publicized refs we
    // compile against), so direct access throws FieldAccessException at runtime. Read them through reflection.
    static FieldInfo? HumanoidField(string name) => AccessTools.Field(typeof(Humanoid), name);

    static readonly FieldInfo? RightItem = HumanoidField("m_rightItem");
    static readonly FieldInfo? LeftItem = HumanoidField("m_leftItem");
    static readonly FieldInfo? HiddenRightItem = HumanoidField("m_hiddenRightItem");
    static readonly FieldInfo? HiddenLeftItem = HumanoidField("m_hiddenLeftItem");
    static readonly FieldInfo? ChestItem = HumanoidField("m_chestItem");
    static readonly FieldInfo? LegItem = HumanoidField("m_legItem");
    static readonly FieldInfo? HelmetItem = HumanoidField("m_helmetItem");
    static readonly FieldInfo? ShoulderItem = HumanoidField("m_shoulderItem");
    static readonly FieldInfo? UtilityItem = HumanoidField("m_utilityItem");

    static ItemDrop.ItemData? Read(FieldInfo? field, Humanoid h) => field?.GetValue(h) as ItemDrop.ItemData;

    // Slot -> (Humanoid item field, VisEquipment instance field(s), ZDO tint key)
    static readonly (string Slot, FieldInfo? Item, string[] Instances)[] Slots =
    {
        ("right", RightItem, new[] { "m_rightItemInstance" }),
        ("left", LeftItem, new[] { "m_leftItemInstance" }),
        ("rightback", HiddenRightItem, new[] { "m_rightBackItemInstance" }),
        ("leftback", HiddenLeftItem, new[] { "m_leftBackItemInstance" }),
        ("chest", ChestItem, new[] { "m_chestItemInstances" }),
        ("legs", LegItem, new[] { "m_legItemInstances" }),
        ("helmet", HelmetItem, new[] { "m_helmetItemInstance" }),
        ("shoulder", ShoulderItem, new[] { "m_shoulderItemInstances" }),
        ("utility", UtilityItem, new[] { "m_utilityItemInstances" }),
    };

    static readonly Dictionary<string, int> TintKeyHash = new Dictionary<string, int>();
    static readonly Dictionary<string, FieldInfo?> InstanceFields = new Dictionary<string, FieldInfo?>();
    static readonly Dictionary<int, int> AppliedTint = new Dictionary<int, int>(); // renderer instance id -> packed tint
    static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
    static readonly int ColorId = Shader.PropertyToID("_Color");
    static readonly FieldInfo? NViewField = AccessTools.Field(typeof(VisEquipment), "m_nview");

    static string TintKey(string slot) => "drake_tint_" + slot;

    static int TintHash(string slot)
    {
        if (!TintKeyHash.TryGetValue(slot, out var h))
            TintKeyHash[slot] = h = TintKey(slot).GetStableHashCode();
        return h;
    }

    // ── Model swap ──────────────────────────────────────────────────────────

    static void SwapHash(VisEquipment vis, FieldInfo? slot, ref int itemHash, ref int variant, bool hasVariant)
    {
        // A throw here would abort equipment setup (and player spawn), so never let one escape.
        try
        {
            if (itemHash == 0 || vis == null || slot == null)
                return;
            var humanoid = vis.GetComponent<Humanoid>();
            var item = humanoid != null ? Read(slot, humanoid) : null;
            if (item?.m_dropPrefab == null || item.m_dropPrefab.name.GetStableHashCode() != itemHash)
                return;
            if (!ItemLookService.TryResolveModel(item, out var hash, out var v))
                return;
            itemHash = hash;
            if (hasVariant)
                variant = v;
        }
        catch (Exception)
        {
            // keep the vanilla model
        }
    }

    [HarmonyPatch(typeof(VisEquipment), "SetRightItem")]
    [HarmonyPrefix]
    static void SetRight(VisEquipment __instance, ref int itemHash)
    {
        var none = 0;
        SwapHash(__instance, RightItem, ref itemHash, ref none, false);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetLeftItem")]
    [HarmonyPrefix]
    static void SetLeft(VisEquipment __instance, ref int itemHash, ref int variant) =>
        SwapHash(__instance, LeftItem, ref itemHash, ref variant, true);

    [HarmonyPatch(typeof(VisEquipment), "SetRightBackItem")]
    [HarmonyPrefix]
    static void SetRightBack(VisEquipment __instance, ref int itemHash)
    {
        var none = 0;
        SwapHash(__instance, HiddenRightItem, ref itemHash, ref none, false);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetLeftBackItem")]
    [HarmonyPrefix]
    static void SetLeftBack(VisEquipment __instance, ref int itemHash, ref int variant) =>
        SwapHash(__instance, HiddenLeftItem, ref itemHash, ref variant, true);

    [HarmonyPatch(typeof(VisEquipment), "SetChestItem")]
    [HarmonyPrefix]
    static void SetChest(VisEquipment __instance, ref int itemHash)
    {
        var none = 0;
        SwapHash(__instance, ChestItem, ref itemHash, ref none, false);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetLegItem")]
    [HarmonyPrefix]
    static void SetLegs(VisEquipment __instance, ref int itemHash)
    {
        var none = 0;
        SwapHash(__instance, LegItem, ref itemHash, ref none, false);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetHelmetItem")]
    [HarmonyPrefix]
    static void SetHelmet(VisEquipment __instance, ref int itemHash)
    {
        var none = 0;
        SwapHash(__instance, HelmetItem, ref itemHash, ref none, false);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetShoulderItem")]
    [HarmonyPrefix]
    static void SetShoulder(VisEquipment __instance, ref int itemHash, ref int variant) =>
        SwapHash(__instance, ShoulderItem, ref itemHash, ref variant, true);

    [HarmonyPatch(typeof(VisEquipment), "SetUtilityItem")]
    [HarmonyPrefix]
    static void SetUtility(VisEquipment __instance, ref int itemHash)
    {
        var none = 0;
        SwapHash(__instance, UtilityItem, ref itemHash, ref none, false);
    }

    // ── Model tint ──────────────────────────────────────────────────────────

    /// <summary>Owner: publish each slot's tint on the player's ZDO (only when it changed).</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
    [HarmonyPostfix]
    static void PublishTints(Humanoid __instance, VisEquipment visEq, bool isRagdoll)
    {
        try
        {
            if (isRagdoll || visEq == null || __instance is not Player)
                return;
            var nview = NViewField?.GetValue(visEq) as ZNetView;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;
            var zdo = nview.GetZDO();
            foreach (var (slot, itemField, _) in Slots)
            {
                var packed = ItemLookService.PackTint(ItemLookService.GetModelTint(Read(itemField, __instance)));
                if (zdo.GetInt(TintHash(slot), 0) != packed)
                    zdo.Set(TintKey(slot), packed);
            }
        }
        catch (Exception)
        {
            // never break equipment setup
        }
    }

    /// <summary>Every client: tint the attached instances from the ZDO values (re-applied when instances are rebuilt).</summary>
    [HarmonyPatch(typeof(VisEquipment), "UpdateEquipmentVisuals")]
    [HarmonyPostfix]
    static void ApplyTints(VisEquipment __instance)
    {
        try
        {
            var zdo = ZdoOf(__instance);
            if (zdo == null)
                return;
            foreach (var (slot, _, instanceFields) in Slots)
            {
                var packed = zdo.GetInt(TintHash(slot), 0);
                foreach (var fieldName in instanceFields)
                {
                    var value = InstanceField(fieldName)?.GetValue(__instance);
                    if (value is GameObject go)
                        Tint(go, packed);
                    else if (value is IEnumerable list)
                        foreach (var entry in list)
                            if (entry is GameObject g)
                                Tint(g, packed);
                }
            }
        }
        catch (Exception)
        {
            // never break visuals
        }
    }

    static void Tint(GameObject go, int packed)
    {
        if (!go)
            return;
        foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
        {
            var id = renderer.GetInstanceID();
            AppliedTint.TryGetValue(id, out var current);
            if (current == packed)
                continue;
            renderer.GetPropertyBlock(Block);
            Block.SetColor(ColorId, ItemLookService.UnpackTint(packed) ?? Color.white);
            renderer.SetPropertyBlock(Block);
            if (AppliedTint.Count > 4096)
                AppliedTint.Clear(); // ids of destroyed renderers; worst case we re-apply once
            if (packed == 0) AppliedTint.Remove(id);
            else AppliedTint[id] = packed;
        }
    }

    static ZDO? ZdoOf(VisEquipment vis)
    {
        var nview = NViewField?.GetValue(vis) as ZNetView;
        return nview != null && nview.IsValid() ? nview.GetZDO() : null;
    }

    static FieldInfo? InstanceField(string name)
    {
        if (!InstanceFields.TryGetValue(name, out var f))
            InstanceFields[name] = f = AccessTools.Field(typeof(VisEquipment), name);
        return f;
    }

    // ── Icon tint (inventory grids + hotbar) ────────────────────────────────

    static readonly FieldInfo? GridElements = AccessTools.Field(typeof(InventoryGrid), "m_elements");
    static readonly FieldInfo? GridInventory = AccessTools.Field(typeof(InventoryGrid), "m_inventory");
    static readonly FieldInfo? HotbarElements = AccessTools.Field(typeof(HotkeyBar), "m_elements");
    static readonly FieldInfo? HotbarItems = AccessTools.Field(typeof(HotkeyBar), "m_items");
    static readonly HashSet<Image> TintedIcons = new HashSet<Image>();
    static readonly Dictionary<Type, (FieldInfo? Icon, MemberInfo? Pos)> ElementMembers = new Dictionary<Type, (FieldInfo?, MemberInfo?)>();

    static (FieldInfo? Icon, MemberInfo? Pos) MembersOf(Type t)
    {
        if (!ElementMembers.TryGetValue(t, out var m))
            ElementMembers[t] = m = (AccessTools.Field(t, "m_icon"),
                (MemberInfo?)AccessTools.Property(t, "Position") ?? AccessTools.Field(t, "m_pos"));
        return m;
    }

    static object? Read(MemberInfo? member, object target) => member switch
    {
        PropertyInfo p => p.GetValue(target),
        FieldInfo f => f.GetValue(target),
        _ => null,
    };

    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    [HarmonyPostfix]
    static void TintGrid(InventoryGrid __instance)
    {
        try
        {
            if (GridElements?.GetValue(__instance) is not IList elements || GridInventory?.GetValue(__instance) is not Inventory inv)
                return;
            foreach (var element in elements)
            {
                if (element == null)
                    continue;
                var members = MembersOf(element.GetType());
                var icon = members.Icon?.GetValue(element) as Image;
                var pos = Read(members.Pos, element);
                if (icon == null || pos is not Vector2i p)
                    continue;
                ApplyIconTint(icon, inv.GetItemAt(p.x, p.y));
            }
        }
        catch (Exception)
        {
            // never break the inventory
        }
    }

    [HarmonyPatch(typeof(HotkeyBar), "UpdateIcons")]
    [HarmonyPostfix]
    static void TintHotbar(HotkeyBar __instance)
    {
        try
        {
            if (HotbarElements?.GetValue(__instance) is not IList elements || HotbarItems?.GetValue(__instance) is not IList items)
                return;
            var bySlot = new Dictionary<int, ItemDrop.ItemData>();
            foreach (var o in items)
                if (o is ItemDrop.ItemData item)
                    bySlot[item.m_gridPos.x] = item;
            for (var i = 0; i < elements.Count; i++)
            {
                var element = elements[i];
                var icon = element == null ? null : MembersOf(element.GetType()).Icon?.GetValue(element) as Image;
                if (icon != null)
                    ApplyIconTint(icon, bySlot.TryGetValue(i, out var it) ? it : null);
            }
        }
        catch (Exception)
        {
            // never break the hotbar
        }
    }

    static void ApplyIconTint(Image icon, ItemDrop.ItemData? item)
    {
        var tint = ItemLookService.GetIconTint(item);
        if (tint != null)
        {
            icon.color = tint.Value;
            TintedIcons.Add(icon);
        }
        else if (TintedIcons.Remove(icon))
        {
            icon.color = Color.white;
        }
    }
}
