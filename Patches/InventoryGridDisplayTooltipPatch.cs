using HarmonyLib;
using DrakesWorkshopLibs.Data;
using DrakesWorkshopLibs.Display;
using DrakesWorkshopLibs.Patches;

namespace DrakesWorkshopLibs.Patches;

[HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
[HarmonyPriority(500)]
public static class InventoryGridDisplayTooltipPatch
{
    [HarmonyPostfix]
    static void ApplyDisplayTooltip(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
    {
        if (item?.m_shared == null || tooltip == null)
            return;

        var topic = ItemDisplayService.GetDisplayNameForUi(item, localize: false);
        string currentText = item.GetTooltip();
        currentText = ItemTooltipPatches.ApplyCraftedByDisplayToTooltipText(currentText, item);
        currentText = UpdateDescription(item, currentText);
        tooltip.Set(topic, currentText, __instance.m_tooltipAnchor);
    }

    private static string UpdateDescription(ItemDrop.ItemData? item, string currentText)
    {
        if (item?.m_shared == null)
            return currentText;
        if (!ItemDisplayService.HasCustomDescription(item))
            return currentText;

        string customDesc = TooltipRichText.EnsureRichTextTagsClosedForTooltip(
            ItemDisplayService.GetProperDescription(item, item.m_shared.m_description));
        string originalDesc = item.m_shared.m_description;
        if (!string.IsNullOrEmpty(originalDesc) && currentText.Contains(originalDesc))
            currentText = currentText.Replace(originalDesc, customDesc);
        return currentText;
    }
}
