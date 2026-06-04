using System.Collections.Generic;
using DrakesWorkshopLibs.Data;

namespace DrakesWorkshopLibs.Display;

public static class ItemDisplayService
{
    public static string GetProperName(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null) return "";
        return GetProperName(item, item.m_shared.m_name);
    }

    public static string GetProperName(ItemDrop.ItemData? item, string defaultName)
    {
        if (item == null) return defaultName;
        item.m_customData ??= new Dictionary<string, string>();
        return item.m_customData.TryGetValue(DrakeCustomDataKeys.Rename, out var existing)
            ? existing : defaultName;
    }

    public static string GetProperDescription(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null) return "";
        return GetProperDescription(item, item.m_shared.m_description);
    }

    public static string GetProperDescription(ItemDrop.ItemData? item, string defaultDesc)
    {
        if (item == null) return defaultDesc;
        item.m_customData ??= new Dictionary<string, string>();
        return item.m_customData.TryGetValue(DrakeCustomDataKeys.RenameDescription, out var existing)
            ? existing : defaultDesc;
    }

    public static string GetDisplayNameForUi(ItemDrop.ItemData? item, bool localize) =>
        DisplayNameResolutionPipeline.Resolve(item, localize);

    public static bool HasCustomName(ItemDrop.ItemData? item) =>
        item?.m_customData != null && item.m_customData.ContainsKey(DrakeCustomDataKeys.Rename);

    public static bool HasCustomDescription(ItemDrop.ItemData? item) =>
        item?.m_customData != null && item.m_customData.ContainsKey(DrakeCustomDataKeys.RenameDescription);

    public static bool HasCraftedByDisplayOverride(ItemDrop.ItemData? item) =>
        item?.m_customData != null &&
        item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByDisplay, out var s) && !string.IsNullOrEmpty(s);

    public static bool HasCraftedByLineLabelOverride(ItemDrop.ItemData? item) =>
        item?.m_customData != null &&
        item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByLineLabel, out var s) && !string.IsNullOrEmpty(s);

    public static bool HasAnyCustomization(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null) return false;
        if (HasCustomName(item) || HasCustomDescription(item)) return true;
        return HasCraftedByDisplayOverride(item) || HasCraftedByLineLabelOverride(item);
    }

    public static void SetCustomName(ItemDrop.ItemData item, string? name)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (string.IsNullOrEmpty(name)) item.m_customData.Remove(DrakeCustomDataKeys.Rename);
        else item.m_customData[DrakeCustomDataKeys.Rename] = name;
    }

    public static void SetCustomDescription(ItemDrop.ItemData item, string? desc)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (string.IsNullOrEmpty(desc)) item.m_customData.Remove(DrakeCustomDataKeys.RenameDescription);
        else item.m_customData[DrakeCustomDataKeys.RenameDescription] = desc;
    }

    public static string GetCraftedByDisplay(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null)
            return item?.m_crafterName ?? "";
        if (item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByDisplay, out var s) && !string.IsNullOrEmpty(s))
            return s;
        return item.m_crafterName ?? "";
    }

    public static void SetCraftedByDisplay(ItemDrop.ItemData item, string? display)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (string.IsNullOrEmpty(display))
            item.m_customData.Remove(DrakeCustomDataKeys.CraftedByDisplay);
        else
            item.m_customData[DrakeCustomDataKeys.CraftedByDisplay] = display;
    }

    public static void SetCraftedByLineLabel(ItemDrop.ItemData item, string? lineLabel)
    {
        item.m_customData ??= new Dictionary<string, string>();
        if (string.IsNullOrEmpty(lineLabel))
            item.m_customData.Remove(DrakeCustomDataKeys.CraftedByLineLabel);
        else
            item.m_customData[DrakeCustomDataKeys.CraftedByLineLabel] = lineLabel;
    }

    public static void ClearCraftedByOverrides(ItemDrop.ItemData item)
    {
        if (item.m_customData == null)
            return;
        item.m_customData.Remove(DrakeCustomDataKeys.CraftedByDisplay);
        item.m_customData.Remove(DrakeCustomDataKeys.CraftedByLineLabel);
    }
}
