namespace DrakeModsLibs.API;

/// <summary>
/// Consumer-owned stack merge policy. Registered via <see cref="CustomizeLibsAPI.RegisterStackMergePolicy"/>.
/// </summary>
public interface IStackMergePolicy
{
    bool SeparateStacksEnabled { get; }
    bool SeparateStacksHardLock { get; }

    /// <summary>
    /// Per-item force that ignores SeparateStacks / HardLock for that item.
    /// Return <see cref="DrakeStackForce.None"/> to follow SeparateStacks as usual.
    /// When two items disagree, <see cref="DrakeStackForce.Never"/> wins.
    /// </summary>
    DrakeStackForce GetStackForce(ItemDrop.ItemData? item);
}
