using System.Reflection;
using BepInEx.Logging;
using DrakeModsLibs.Patches;
using HarmonyLib;

namespace DrakeModsLibs;

internal static class HarmonyPatchHub
{
    internal static void ApplyAll(Harmony harmony, ManualLogSource log)
    {
        InventoryStackPatches.Apply(harmony, log);
        ItemTooltipPatches.Apply(harmony, log);
        DropHudMessagePatches.ApplyDropItemPendingCapture(harmony, log);
        harmony.PatchAll(typeof(HarmonyPatchHub).Assembly);
        DropHudMessagePatches.ApplyMessageHudShowMessage(harmony, log);
        log.LogInfo("[DrakeModsLibs] Display Harmony patches applied.");
    }
}
