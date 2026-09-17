using System;
using System.Text;
using DrakeModsLibs.Integration;
using HarmonyLib;

namespace DrakeModsLibs.Patches;

/// <summary>
/// Appends one unified yellow interact hint from usable tabs; strips legacy competing options lines.
/// </summary>
[HarmonyPatch]
public static class InventoryContextHintPatch
{
    const string HintMarker = "Right Click to ";

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Low)]
    static void AppendUnifiedHint(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
    {
        try
        {
            if (item == null || tooltip == null || __instance == null)
                return;

            var topicField = AccessTools.Field(typeof(UITooltip), "m_topic");
            var textField = AccessTools.Field(typeof(UITooltip), "m_text");
            if (topicField == null || textField == null)
                return;

            var topic = topicField.GetValue(tooltip) as string ?? "";
            var currentText = textField.GetValue(tooltip) as string ?? "";

            currentText = InventoryContextHints.StripCompetingOptionsHints(currentText);

            if (currentText.IndexOf(HintMarker, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                tooltip.Set(topic, currentText, __instance.m_tooltipAnchor);
                return;
            }

            if (!InventoryContextHints.TryFormatInteractHint(item, out var hintLine))
            {
                tooltip.Set(topic, currentText, __instance.m_tooltipAnchor);
                return;
            }

            var sb = new StringBuilder(currentText.Length + hintLine.Length + 4);
            sb.Append(currentText);
            if (currentText.Length > 0 && !currentText.EndsWith("\n", StringComparison.Ordinal))
                sb.AppendLine();
            sb.AppendLine();
            sb.Append(hintLine);

            tooltip.Set(topic, sb.ToString(), __instance.m_tooltipAnchor);
        }
        catch (Exception)
        {
            // Tooltip path must stay quiet.
        }
    }
}
