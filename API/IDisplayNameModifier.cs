namespace DrakeModsLibs.API;

public interface IDisplayNameModifier
{
    bool AffectsDisplay(ItemDrop.ItemData? item);
    string GetPrefixRaw(ItemDrop.ItemData? item);
}
