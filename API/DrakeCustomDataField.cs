using System;

namespace DrakesWorkshopLibs.API;

/// <summary>Describes one known custom-data key for discovery, filtering, and formatted dumps.</summary>
public sealed class DrakeCustomDataField
{
    public DrakeCustomDataField(string key, string modId, DrakeCustomDataKind kind, string? label = null)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        ModId = modId ?? throw new ArgumentNullException(nameof(modId));
        Kind = kind;
        Label = label;
    }

    public string Key { get; }
    public string ModId { get; }
    public DrakeCustomDataKind Kind { get; }
    public string? Label { get; }
}
