namespace DrakeModsLibs.API;

/// <summary>
/// Maps a custom-data tag to blocked rename operations, inventory-UI handoff, and soft vs hard lock.
/// Other mods register rules via <see cref="CustomizeLibsAPI.RegisterTagBlockRule"/>;
/// RenameIt only queries <see cref="CustomizeLibsAPI.CanPerform"/> /
/// <see cref="CustomizeLibsAPI.IsRenameInventorySuppressed"/>.
/// </summary>
public readonly struct TagBlockRule
{
    public TagBlockRule(string tagKey, CustomizeOperation blockedOperations)
        : this(tagKey, blockedOperations, suppressRenameInventoryUi: false, hardLock: false)
    {
    }

    public TagBlockRule(string tagKey, CustomizeOperation blockedOperations, bool suppressRenameInventoryUi)
        : this(tagKey, blockedOperations, suppressRenameInventoryUi, hardLock: false)
    {
    }

    public TagBlockRule(
        string tagKey,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi,
        bool hardLock)
    {
        TagKey = tagKey;
        BlockedOperations = blockedOperations;
        SuppressRenameInventoryUi = suppressRenameInventoryUi;
        HardLock = hardLock;
    }

    public string TagKey { get; }
    public CustomizeOperation BlockedOperations { get; }

    /// <summary>
    /// When true and the tag is present, RenameIt inventory menu/tooltip handoff should stand down
    /// (another mod owns context). Soft rules respect admin/VIP <c>TagBypass</c>; hard rules do not.
    /// </summary>
    public bool SuppressRenameInventoryUi { get; }

    /// <summary>
    /// When true, <see cref="CustomizeLibsAPI.CanPerform"/> always denies for blocked ops —
    /// admin/VIP <c>TagBypass</c> cannot override. Also forces inventory suppress when
    /// <see cref="SuppressRenameInventoryUi"/> is set.
    /// </summary>
    public bool HardLock { get; }
}
