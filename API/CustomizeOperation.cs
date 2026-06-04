namespace DrakesWorkshopLibs.API;

[System.Flags]
public enum CustomizeOperation
{
    None = 0,
    RenameName = 1,
    RenameDescription = 2,
    EditCraftedBy = 4,
    SetTag = 8,
    AllEdits = RenameName | RenameDescription | EditCraftedBy,
}
