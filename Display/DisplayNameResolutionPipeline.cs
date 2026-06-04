using System.Linq;
using DrakesWorkshopLibs.API;
using DrakesWorkshopLibs.Runtime;

namespace DrakesWorkshopLibs.Display;

internal static class DisplayNameResolutionPipeline
{
    internal static void EnsureDefaultLayers()
    {
        if (CustomizeLibsRuntime.DefaultDisplayLayersRegistered)
            return;
        CustomizeLibsRuntime.DefaultDisplayLayersRegistered = true;
        RegisterLayer(new BuiltInRenameItNameLayer());
    }

    public static void RegisterLayer(IItemDisplayNameLayer layer)
    {
        if (layer == null || CustomizeLibsRuntime.DisplayNameLayers.Contains(layer))
            return;
        CustomizeLibsRuntime.DisplayNameLayers.Add(layer);
    }

    internal static string Resolve(ItemDrop.ItemData? item, bool localize)
    {
        EnsureDefaultLayers();
        if (item?.m_shared == null)
            return "";

        string displayName = item.m_shared.m_name;
        foreach (var layer in CustomizeLibsRuntime.DisplayNameLayers.OrderBy(l => l.Priority))
            layer.TryApply(item, ref displayName);

        string prefix = DisplayNameModifierHub.GetPrefixRaw(item);
        string combined = string.IsNullOrEmpty(prefix) ? displayName : prefix + " " + displayName;
        string safe = TooltipRichText.EnsureRichTextTagsClosedForTooltip(combined);
        if (!localize || Localization.instance == null)
            return safe;
        return Localization.instance.Localize(safe);
    }
}
