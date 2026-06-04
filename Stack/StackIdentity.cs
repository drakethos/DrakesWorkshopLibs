using DrakesWorkshopLibs.Data;

using System;
using System.Text;

namespace DrakesWorkshopLibs.Stack;

/// <summary>Compares Drake-specific custom data so stacks with different rename/desc/crafted display do not merge when <see cref="policy.SeparateStacksEnabled"/> is on.</summary>
internal static class StackIdentity
{
    internal static string GetFingerprint(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null)
            return "";

        var sb = new StringBuilder();
        Append(sb, item, DrakeCustomDataKeys.Rename);
        Append(sb, item, DrakeCustomDataKeys.RenameDescription);
        Append(sb, item, DrakeCustomDataKeys.CraftedByDisplay);
        Append(sb, item, DrakeCustomDataKeys.CraftedByLineLabel);
        return sb.ToString();
    }

    private static void Append(StringBuilder sb, ItemDrop.ItemData item, string key)
    {
        if (item.m_customData!.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
            sb.Append('|').Append(v);
        else
            sb.Append('|');
    }

    internal static bool SameDrakeStackIdentity(ItemDrop.ItemData? a, ItemDrop.ItemData? b)
    {
        if (a == null || b == null)
            return false;
        return string.Equals(GetFingerprint(a), GetFingerprint(b), StringComparison.Ordinal);
    }
}
