using System;
using DrakeModsLibs.Input;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI;
using HarmonyLib;

namespace DrakeModsLibs.Patches;

/// <summary>
/// Shared inventory RMB + <see cref="DrakeIntegrationConfig.InventoryOpenModifier"/> opens
/// <see cref="DrakeTabHost"/> when any usable tab exists. Feature mods register tabs; they do not own this wrap.
/// </summary>
[HarmonyPatch]
public static class InventoryContextOpenPatch
{
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Awake))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void WrapPlayerGridRightClick(InventoryGui __instance)
    {
        if (__instance?.m_playerGrid == null)
            return;

        var previous = __instance.m_playerGrid.m_onRightClick;
        __instance.m_playerGrid.m_onRightClick = (grid, item, pos) =>
        {
            try
            {
                if (item != null
                    && MenuKeyBinding.IsHeld(DrakeIntegrationConfig.InventoryOpenModifier)
                    && DrakeTabHost.TryOpenInventoryContext(item))
                {
                    return;
                }
            }
            catch (Exception)
            {
                // Never dump into inventory click path; feature mods log their own Show failures.
            }

            previous?.Invoke(grid, item, pos);
        };
    }
}
