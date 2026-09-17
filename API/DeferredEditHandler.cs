namespace DrakeModsLibs.API;

/// <summary>
/// Owning-mod decision for a deferred edit. Return true to allow RenameIt to continue
/// to its validators; false to deny. Admin <c>TagBypass</c> is not applied automatically.
/// </summary>
public delegate bool DeferredEditHandler(
    ItemDrop.ItemData? item,
    Player? player,
    CustomizeOperation operation);
