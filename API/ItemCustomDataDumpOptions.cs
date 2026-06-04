namespace DrakesWorkshopLibs.API;

public sealed class ItemCustomDataDumpOptions
{
    public ItemCustomDataDumpFormat Format { get; set; } = ItemCustomDataDumpFormat.Neat;

    /// <summary>Only keys registered to this mod (e.g. <c>DrakesRenameIt</c>).</summary>
    public string? ModId { get; set; }

    /// <summary>Only this exact key (e.g. <see cref="Data.DrakeCustomDataKeys.Rename"/>).</summary>
    public string? Key { get; set; }

    /// <summary>Only keys starting with <c>Drake_</c> or registered in the catalog.</summary>
    public bool DrakeKeysOnly { get; set; }

    /// <summary>Include registered keys that are absent on the item.</summary>
    public bool IncludeEmpty { get; set; }

    /// <summary>Include keys present on the item but not registered in the catalog.</summary>
    public bool IncludeUnknownKeys { get; set; } = true;

    /// <summary>First line: prefab name and stack (Neat format only).</summary>
    public bool IncludeItemSummary { get; set; } = true;
}
