namespace DrakeModsLibs.API;

/// <summary>
/// Prefab / shared-name family rule registered by other mods at startup.
/// Match uses the same tokens as RenameIt exclusions: localization token
/// (<c>m_shared.m_name</c>), drop prefab name, or localized English display name.
/// </summary>
public readonly struct ItemExclusionRule
{
    public ItemExclusionRule(
        string match,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi = false,
        bool hardLock = false,
        string? deferredAuthorityId = null)
    {
        Match = match ?? "";
        BlockedOperations = blockedOperations;
        SuppressRenameInventoryUi = suppressRenameInventoryUi;
        HardLock = hardLock;
        DeferredAuthorityId = string.IsNullOrWhiteSpace(deferredAuthorityId)
            ? null
            : deferredAuthorityId!.Trim();
    }

    public string Match { get; }
    public CustomizeOperation BlockedOperations { get; }
    public bool SuppressRenameInventoryUi { get; }
    public bool HardLock { get; }

    /// <summary>
    /// When set, matching items are deferred to this authority (not soft/hard blocked by this rule).
    /// Pair with <see cref="CustomizeLibsAPI.RegisterDeferredEditAuthority"/>.
    /// </summary>
    public string? DeferredAuthorityId { get; }

    public bool IsDeferred => DeferredAuthorityId != null;
}
