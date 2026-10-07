using DrakeModsLibs.Display;
using HarmonyLib;
using UnityEngine;

namespace DrakeModsLibs.Patches;

/// <summary>
/// Shared icon override hook: inventory, hotbar, tooltips, and HUD messages all read <see cref="ItemDrop.ItemData.GetIcon"/>.
/// Hot path (per slot per refresh) — one dictionary lookup when no override is set.
/// </summary>
[HarmonyPatch(typeof(ItemDrop.ItemData), nameof(ItemDrop.ItemData.GetIcon))]
internal static class ItemIconPatches
{
    static void Postfix(ItemDrop.ItemData __instance, ref Sprite __result)
    {
        var sprite = ItemIconService.ResolveOverride(__instance);
        if (sprite)
            __result = sprite!;
    }
}
