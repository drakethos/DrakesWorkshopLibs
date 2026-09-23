namespace DrakeModsLibs.Compat;

/// <summary>
/// Patch order for compat modules. Lower = earlier for Harmony patches.
/// Consumers may use higher values for domain-specific override semantics.
/// </summary>
public static class CompatPriority
{
    /// <summary>Vanilla game baseline — always first.</summary>
    public const int Vanilla = 0;

    /// <summary>Built-in soft modules shipped inside a Drake consumer.</summary>
    public const int BuiltIn = 100;

    /// <summary>Suggested default for third-party modules registered via a consumer API.</summary>
    public const int ThirdParty = 200;
}
