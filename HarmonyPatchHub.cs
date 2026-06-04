using System.Reflection;
using BepInEx.Logging;
using DrakesWorkshopLibs.Patches;
using HarmonyLib;

namespace DrakesWorkshopLibs;

internal static class HarmonyPatchHub
{
    internal static void ApplyAll(Harmony harmony, ManualLogSource log)
    {
        InventoryStackPatches.Apply(harmony, log);
        ItemTooltipPatches.Apply(harmony, log);
        DropHudMessagePatches.ApplyDropItemPendingCapture(harmony, log);
        harmony.PatchAll(typeof(HarmonyPatchHub).Assembly);
        DropHudMessagePatches.ApplyMessageHudShowMessage(harmony, log);
        log.LogInfo("[DrakesWorkshopLibs] Display Harmony patches applied.");
    }
}
