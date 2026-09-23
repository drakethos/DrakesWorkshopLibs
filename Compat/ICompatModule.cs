using HarmonyLib;

namespace DrakeModsLibs.Compat;

/// <summary>
/// Optional soft-compat module. Soft-resolved — never a hard BepInEx requirement.
/// Each Drake consumer owns a <see cref="CompatHost"/>; mutually exclusive foreign mods
/// are not the host's problem — every present module is activated and run.
/// </summary>
public interface ICompatModule
{
    /// <summary>Stable id for logs and dedupe (e.g. <c>WardIsLove</c>).</summary>
    string Id { get; }

    /// <summary>
    /// Optional BepInEx GUID for SoftDependency (load-order only). Null when type-scan only.
    /// </summary>
    string? SoftDependencyGuid { get; }

    /// <summary>
    /// Lower runs Harmony patches first. Vanilla = <see cref="CompatPriority.Vanilla"/>.
    /// Third-party default = <see cref="CompatPriority.ThirdParty"/>.
    /// </summary>
    int Priority { get; }

    /// <summary>True after a successful <see cref="TryActivate"/>.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Probe for the foreign mod. Return true to keep this module registered and active.
    /// Must never throw; absent mods return false.
    /// </summary>
    bool TryActivate();

    /// <summary>Apply soft Harmony patches when this module is active. No-op when nothing to patch.</summary>
    void ApplyHarmonyPatches(Harmony harmony);
}
