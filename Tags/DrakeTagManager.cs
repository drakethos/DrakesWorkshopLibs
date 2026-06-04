using System.Collections.Generic;
using DrakesWorkshopLibs.Data;

namespace DrakesWorkshopLibs.Tags;

public static class DrakeTagManager
{
    public static bool HasTag(ItemDrop.ItemData? item, string tagKey)
    {
        if (item?.m_customData == null || string.IsNullOrEmpty(tagKey)) return false;
        return item.m_customData.TryGetValue(tagKey, out var v) &&
               (v == "1" || string.Equals(v, "true", System.StringComparison.OrdinalIgnoreCase));
    }

    public static void SetTag(ItemDrop.ItemData item, string tagKey)
    {
        if (string.IsNullOrEmpty(tagKey)) return;
        item.m_customData ??= new Dictionary<string, string>();
        item.m_customData[tagKey] = "1";
    }

    public static void ClearTag(ItemDrop.ItemData item, string tagKey)
    {
        if (item.m_customData == null || string.IsNullOrEmpty(tagKey)) return;
        item.m_customData.Remove(tagKey);
    }
}
