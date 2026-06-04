using System;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace DrakesWorkshopLibs.Patches;


using DrakesWorkshopLibs.Data;
using DrakesWorkshopLibs.Runtime;
using DrakesWorkshopLibs.Stack;

/// <summary>When <see cref="policy.SeparateStacksEnabled"/> is on, only stacks with matching fingerprints merge.</summary>
/// <remarks>
/// <see cref="Inventory.FindFreeStackItem"/> is implemented as a simple loop; we replace it when we know the incoming
/// item (<see cref="IncomingStackItem"/> from <c>AddItem(ItemData)</c>) so merge decisions always include custom data.
/// A postfix on <c>ref __result</c> is unreliable across Harmony versions; a prefix that skips the original does not.
/// </remarks>
internal static class InventoryStackPatches
{
    internal static ItemDrop.ItemData? IncomingStackItem;

    internal static void Apply(Harmony harmony, ManualLogSource log)
    {
        var inv = typeof(Inventory);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        var addItemOne = AccessTools.DeclaredMethod(inv, nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) })
                         ?? AccessTools.Method(inv, nameof(Inventory.AddItem), new[] { typeof(ItemDrop.ItemData) });
        if (addItemOne != null)
        {
            harmony.Patch(
                addItemOne,
                prefix: new HarmonyMethod(typeof(InventoryStackPatches), nameof(AddItem_IncomingPrefix)),
                finalizer: new HarmonyMethod(typeof(InventoryStackPatches), nameof(AddItem_IncomingCleanup)));
        }
        else
        {
            log.LogWarning(
                "[DrakesWorkshopLibs] SeparateStacks: AddItem(ItemData) not found — incoming stack tracking disabled.");
        }

        // FindFreeStackItem has had 2- and 3-arg variants (worldLevel added in newer Valheim).
        // Harmony matches prefix params by name, so one prefix covers both — we just need to bind.
        MethodInfo? findStack = null;
        foreach (var m in inv.GetMethods(flags))
        {
            if (m.Name != "FindFreeStackItem")
                continue;
            var ps = m.GetParameters();
            if (ps.Length < 2)
                continue;
            if (ps[0].ParameterType != typeof(string) || ps[1].ParameterType != typeof(int))
                continue;
            findStack = m;
            break;
        }

        if (findStack != null)
        {
            harmony.Patch(
                findStack,
                prefix: new HarmonyMethod(typeof(InventoryStackPatches), nameof(FindFreeStackItem_Prefix)));
        }
        else
        {
            log.LogWarning(
                "[DrakesWorkshopLibs] SeparateStacks: FindFreeStackItem not found — merge-from-pickup may ignore identity.");
        }

        // AddItem cell-overload has also varied in arity; find by signature: (ItemData, int, int, int[, ...]).
        MethodInfo? addAtCell = null;
        foreach (var m in inv.GetMethods(flags))
        {
            if (m.Name != "AddItem")
                continue;
            var ps = m.GetParameters();
            if (ps.Length < 4)
                continue;
            if (ps[0].ParameterType != typeof(ItemDrop.ItemData))
                continue;
            if (ps[1].ParameterType != typeof(int) || ps[2].ParameterType != typeof(int) || ps[3].ParameterType != typeof(int))
                continue;
            addAtCell = m;
            break;
        }

        if (addAtCell != null)
        {
            harmony.Patch(
                addAtCell,
                prefix: new HarmonyMethod(typeof(InventoryStackPatches), nameof(AddItemAtCell_Prefix)));
        }
        else
        {
            log.LogWarning(
                "[DrakesWorkshopLibs] SeparateStacks: AddItem(ItemData,int,int,int...) not found — cell merge guard disabled.");
        }
    }

    internal static void AddItem_IncomingPrefix(ItemDrop.ItemData item)
    {
        IncomingStackItem = item;
    }

    internal static void AddItem_IncomingCleanup(Exception? __exception)
    {
        IncomingStackItem = null;
    }

    /// <summary>
    /// Replicates vanilla <c>FindFreeStackItem</c> and requires matching custom data when
    /// <see cref="policy.SeparateStacksEnabled"/> is on and the incoming stack is known.
    /// </summary>
    internal static bool FindFreeStackItem_Prefix(
        Inventory __instance,
        string name,
        int quality,
        ref ItemDrop.ItemData? __result)
    {
        var policy = CustomizeLibsRuntime.StackMergePolicy;
        if (policy == null || !policy.SeparateStacksEnabled || IncomingStackItem == null)
            return true;

        __result = null;
        foreach (ItemDrop.ItemData? itemData in __instance.GetAllItems())
        {
            if (TryPickStackSlot(IncomingStackItem, name, quality, itemData, ref __result))
                continue;
            break;
        }

        return false;
    }

    /// <returns><c>true</c> = keep scanning; <c>false</c> = matched a stack (also sets <paramref name="__result"/>).</returns>
    private static bool TryPickStackSlot(
        ItemDrop.ItemData incoming,
        string name,
        int quality,
        ItemDrop.ItemData? itemData,
        ref ItemDrop.ItemData? __result)
    {
        if (itemData?.m_shared == null)
            return true;
        if (itemData.m_shared.m_name != name || itemData.m_quality != quality)
            return true;
        if (itemData.m_stack >= itemData.m_shared.m_maxStackSize)
            return true;
        if (!StackIdentity.SameDrakeStackIdentity(incoming, itemData))
            return true;

        __result = itemData;
        return false;
    }

    internal static bool AddItemAtCell_Prefix(
        ItemDrop.ItemData item,
        int amount,
        int x,
        int y,
        Inventory __instance,
        ref bool __result)
    {
        var policy = CustomizeLibsRuntime.StackMergePolicy;
        if (policy == null || !policy.SeparateStacksEnabled)
            return true;

        ItemDrop.ItemData? itemAt = __instance.GetItemAt(x, y);
        if (itemAt == null)
            return true;

        if (itemAt.m_shared.m_name != item.m_shared.m_name)
            return true;
        if (itemAt.m_shared.m_maxQuality > 1 && itemAt.m_quality != item.m_quality)
            return true;

        if (!StackIdentity.SameDrakeStackIdentity(item, itemAt))
        {
            if (!policy.SeparateStacksHardLock)
                return true;

            __result = false;
            return false;
        }

        return true;
    }
}
