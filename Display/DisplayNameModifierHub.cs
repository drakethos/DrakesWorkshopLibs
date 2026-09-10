using DrakeModsLibs.Runtime;

namespace DrakeModsLibs.Display;

internal static class DisplayNameModifierHub
{
    public static bool AffectsDisplay(ItemDrop.ItemData? item)
    {
        foreach (var m in CustomizeLibsRuntime.DisplayNameModifiers)
            if (m.AffectsDisplay(item)) return true;
        return false;
    }

    public static string GetPrefixRaw(ItemDrop.ItemData? item)
    {
        foreach (var m in CustomizeLibsRuntime.DisplayNameModifiers)
        {
            var p = m.GetPrefixRaw(item);
            if (!string.IsNullOrEmpty(p)) return p;
        }
        return "";
    }
}
