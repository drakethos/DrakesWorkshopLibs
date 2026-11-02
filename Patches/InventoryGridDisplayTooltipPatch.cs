using HarmonyLib;
using DrakeModsLibs.Data;
using DrakeModsLibs.Display;
using DrakeModsLibs.Patches;

namespace DrakeModsLibs.Patches;

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

    /// <summary>Swap the item's vanilla description in a tooltip text for its custom one (no change when it has none).</summary>
    internal static string ApplyDescriptionToTooltipText(ItemDrop.ItemData? item, string currentText) =>
        UpdateDescription(item, currentText);

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
