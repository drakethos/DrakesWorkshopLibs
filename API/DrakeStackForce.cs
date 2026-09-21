namespace DrakeModsLibs.API;

/// <summary>
/// Per-item override for inventory merge, ignoring global SeparateStacks / HardLock.
/// No TagBypass — admin cannot drag-merge around a force.
/// </summary>
public enum DrakeStackForce
{
    /// <summary>Follow SeparateStacks / SeparateStacksHardLock as usual.</summary>
    None = 0,

    /// <summary>
    /// Always fingerprint-merge: same Drake identity stacks; different identity never merges (hard).
    /// </summary>
    ByIdentity = 1,

    /// <summary>Never merge with another stack, even when fingerprints match.</summary>
    Never = 2,
}
