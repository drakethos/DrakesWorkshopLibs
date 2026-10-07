namespace DrakeModsLibs.API;

[System.Flags]
public enum CustomizeOperation
{
    None = 0,
    RenameName = 1,
    RenameDescription = 2,
    EditCraftedBy = 4,
    SetTag = 8,
    /// <summary>Swap the inventory icon (ReskinIt). Not part of <see cref="AllEdits"/> so existing rename rules keep their meaning.</summary>
    ReskinIcon = 16,
    /// <summary>Swap the equipped model (ReskinIt).</summary>
    ReskinModel = 32,
    /// <summary>Tint the icon and/or model (ReskinIt).</summary>
    ReskinColor = 64,
    AllEdits = RenameName | RenameDescription | EditCraftedBy,
}
