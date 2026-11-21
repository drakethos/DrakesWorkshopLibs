using System.Reflection;
using BepInEx.Logging;
using DrakeModsLibs.Defense;
using DrakeModsLibs.Patches;
using HarmonyLib;

namespace DrakeModsLibs;

internal static class HarmonyPatchHub
{
    /// <summary>
    /// Applies every display patch. Each step is isolated through <paramref name="guard"/>: a step or patch class
    /// that cannot be applied (game update, another mod's transpiler) is skipped and named in the log instead of
    /// aborting <c>Awake</c> and leaving the rest of the mod unpatched.
    /// </summary>
    internal static void ApplyAll(Harmony harmony, ManualLogSource log, ModGuard guard)
    {
        guard.Try("inventory stack patches", () => InventoryStackPatches.Apply(harmony, log));
        guard.Try("item tooltip patches", () => ItemTooltipPatches.Apply(harmony, log));
        guard.Try("drop HUD pending-capture patch", () => DropHudMessagePatches.ApplyDropItemPendingCapture(harmony, log));
        guard.PatchAll(typeof(HarmonyPatchHub).Assembly);
        guard.Try("HUD show-message patch", () => DropHudMessagePatches.ApplyMessageHudShowMessage(harmony, log));
        guard.Arm();
        log.LogInfo("[DrakeModsLibs] Display Harmony patches applied.");
    }
}
