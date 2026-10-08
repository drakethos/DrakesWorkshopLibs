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
/// Model: the owner publishes real -> look prefab hashes for every worn item on its ZDO; every client swaps the
/// hash at the last step (<c>VisEquipment.Set*Equipped</c>), so the game's equipment state never disagrees.
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
    //
    // The game's own equipment state (Humanoid -> VisEquipment fields -> ZDO) always keeps the real prefab hashes.
    // Only the final attach step sees the override: the owner publishes "real hash -> look hash" pairs for every
    // item it wears (any slot, hidden hand items, and slots added by other mods), and every client remaps the hash
    // right before VisEquipment attaches a model. Swapping earlier (in SetRightItem etc.) left the game's paths
    // disagreeing about the hash, so the model was destroyed and re-attached over and over: no model, flicker,
    // and equip effects firing every frame (camera shake).

    const int MaxModelSwaps = 16;
    const string ModelCountName = "drake_model_n";
    static readonly int ModelCountKey = ModelCountName.GetStableHashCode();
    static readonly string[] ModelFromNames = SwapNames("f"), ModelToNames = SwapNames("t"), ModelVariantNames = SwapNames("v");
    static readonly int[] ModelFromKeys = Hashes(ModelFromNames), ModelToKeys = Hashes(ModelToNames), ModelVariantKeys = Hashes(ModelVariantNames);

    static string[] SwapNames(string part)
    {
        var names = new string[MaxModelSwaps];
        for (var i = 0; i < MaxModelSwaps; i++)
            names[i] = $"drake_model_{part}{i}";
        return names;
    }

    static int[] Hashes(string[] names)
    {
        var keys = new int[names.Length];
        for (var i = 0; i < names.Length; i++)
            keys[i] = names[i].GetStableHashCode();
        return keys;
    }

    /// <summary>Everything the humanoid shows on its body: equipped items plus weapons hidden on the back.</summary>
    static IEnumerable<ItemDrop.ItemData> WornItems(Humanoid humanoid)
    {
        var hiddenRight = Read(HiddenRightItem, humanoid);
        if (hiddenRight != null)
            yield return hiddenRight;
        var hiddenLeft = Read(HiddenLeftItem, humanoid);
        if (hiddenLeft != null)
            yield return hiddenLeft;
        var inventory = humanoid.GetInventory();
        if (inventory == null)
            yield break;
        foreach (var item in inventory.GetAllItems())
            if (item.m_equipped)
                yield return item;
    }

    /// <summary>Owner: publish the real -> look hash pairs on the ZDO (only the values that changed).</summary>
    static void PublishModels(Humanoid humanoid, ZDO zdo)
    {
        var count = 0;
        var seen = new HashSet<int>();
        foreach (var item in WornItems(humanoid))
        {
            if (count >= MaxModelSwaps)
                break;
            if (item?.m_dropPrefab == null || !ItemLookService.TryResolveModel(item, out var to, out var variant))
                continue;
            var from = item.m_dropPrefab.name.GetStableHashCode();
            if (from == to || !seen.Add(from))
                continue;
            SetIfChanged(zdo, ModelFromKeys[count], ModelFromNames[count], from);
            SetIfChanged(zdo, ModelToKeys[count], ModelToNames[count], to);
            SetIfChanged(zdo, ModelVariantKeys[count], ModelVariantNames[count], variant);
            count++;
        }
        SetIfChanged(zdo, ModelCountKey, ModelCountName, count);
    }

    static void SetIfChanged(ZDO zdo, int key, string name, int value)
    {
        if (zdo.GetInt(key, 0) != value)
            zdo.Set(name, value);
    }

    /// <summary>Every client: real prefab hash -> published look hash (and its variant), right before attaching.</summary>
    static void MapModel(VisEquipment vis, ref int hash, ref int variant, bool hasVariant)
    {
        // Runs inside the game's per-frame visual update: never let an exception escape.
        try
        {
            if (hash == 0)
                return;
            var zdo = ZdoOf(vis);
            if (zdo == null)
                return;
            var count = Math.Min(zdo.GetInt(ModelCountKey, 0), MaxModelSwaps);
            for (var i = 0; i < count; i++)
            {
                if (zdo.GetInt(ModelFromKeys[i], 0) != hash)
                    continue;
                var to = zdo.GetInt(ModelToKeys[i], 0);
                if (to == 0 || ObjectDB.instance == null || ObjectDB.instance.GetItemPrefab(to) == null)
                    return; // unknown on this client (missing mod): keep the real model
                hash = to;
                if (hasVariant)
                    variant = zdo.GetInt(ModelVariantKeys[i], 0);
                return;
            }
        }
        catch (Exception)
        {
            // keep the real model
        }
    }

    static void MapModel(VisEquipment vis, ref int hash)
    {
        var none = 0;
        MapModel(vis, ref hash, ref none, false);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetRightHandEquipped")]
    [HarmonyPrefix]
    static void RightHand(VisEquipment __instance, ref int hash) => MapModel(__instance, ref hash);

    [HarmonyPatch(typeof(VisEquipment), "SetLeftHandEquipped")]
    [HarmonyPrefix]
    static void LeftHand(VisEquipment __instance, ref int hash, ref int variant) => MapModel(__instance, ref hash, ref variant, true);

    [HarmonyPatch(typeof(VisEquipment), "SetBackEquipped")]
    [HarmonyPrefix]
    static void Back(VisEquipment __instance, ref int leftItem, ref int rightItem, ref int leftVariant)
    {
        MapModel(__instance, ref leftItem, ref leftVariant, true);
        MapModel(__instance, ref rightItem);
    }

    [HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
    [HarmonyPrefix]
    static void Chest(VisEquipment __instance, ref int hash) => MapModel(__instance, ref hash);

    [HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
    [HarmonyPrefix]
    static void Legs(VisEquipment __instance, ref int hash) => MapModel(__instance, ref hash);

    [HarmonyPatch(typeof(VisEquipment), "SetHelmetEquipped")]
    [HarmonyPrefix]
    static void Helmet(VisEquipment __instance, ref int hash) => MapModel(__instance, ref hash);

    [HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
    [HarmonyPrefix]
    static void Shoulder(VisEquipment __instance, ref int hash, ref int variant) => MapModel(__instance, ref hash, ref variant, true);

    [HarmonyPatch(typeof(VisEquipment), "SetUtilityEquipped")]
    [HarmonyPrefix]
    static void Utility(VisEquipment __instance, ref int hash) => MapModel(__instance, ref hash);

    [HarmonyPatch(typeof(VisEquipment), "SetTrinketEquipped")]
    [HarmonyPrefix]
    static void Trinket(VisEquipment __instance, ref int hash) => MapModel(__instance, ref hash);

    // ── Model tint ──────────────────────────────────────────────────────────

    /// <summary>Owner: publish model swaps and each slot's tint on the player's ZDO (only what changed).</summary>
    [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.SetupVisEquipment))]
    [HarmonyPostfix]
    static void PublishLook(Humanoid __instance, VisEquipment visEq, bool isRagdoll)
    {
        try
        {
            if (isRagdoll || visEq == null || __instance is not Player)
                return;
            var nview = NViewField?.GetValue(visEq) as ZNetView;
            if (nview == null || !nview.IsValid() || !nview.IsOwner())
                return;
            var zdo = nview.GetZDO();
            PublishModels(__instance, zdo);
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
            // Skin-tight chest/leg armor is painted onto the body, not attached: tint those textures too.
            BodyTextureTint.Apply(__instance, zdo.GetInt(TintHash("chest"), 0), zdo.GetInt(TintHash("legs"), 0));
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
