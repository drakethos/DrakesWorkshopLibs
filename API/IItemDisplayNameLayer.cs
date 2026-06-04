namespace DrakesWorkshopLibs.API;

public interface IItemDisplayNameLayer
{
    int Priority { get; }
    bool TryApply(ItemDrop.ItemData? item, ref string displayName);
}

public static class DisplayNameLayerPriority
{
    public const int RenameItBase = 100;
    public const int CosmeticPrefix = 150;
    public const int LootOverlay = 200;
    public const int ShopOverlay = 300;
}
