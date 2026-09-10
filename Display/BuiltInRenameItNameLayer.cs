using DrakeModsLibs.API;
using DrakeModsLibs.Data;

namespace DrakeModsLibs.Display;

internal sealed class BuiltInRenameItNameLayer : IItemDisplayNameLayer
{
    public int Priority => DisplayNameLayerPriority.RenameItBase;

    public bool TryApply(ItemDrop.ItemData? item, ref string displayName)
    {
        if (item?.m_customData == null) return false;
        if (!item.m_customData.TryGetValue(DrakeCustomDataKeys.Rename, out var custom) || string.IsNullOrEmpty(custom))
            return false;
        displayName = custom;
        return true;
    }
}
