using System;
using System.Collections.Generic;
using BepInEx.Logging;

namespace DrakeModsLibs.Compat;

/// <summary>
/// Factory for per-plugin <see cref="CompatHost"/> instances. Never merge hosts across GUID.
/// </summary>
public static class CompatHosts
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, CompatHost> ByOwner =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Get or create the host for <paramref name="ownerGuid"/>.</summary>
    public static CompatHost GetOrCreate(string ownerGuid, ManualLogSource? log = null)
    {
        if (string.IsNullOrEmpty(ownerGuid))
            throw new ArgumentException("Owner GUID is required.", nameof(ownerGuid));

        lock (Gate)
        {
            if (ByOwner.TryGetValue(ownerGuid, out var existing))
                return existing;

            var host = new CompatHost(ownerGuid, log);
            ByOwner[ownerGuid] = host;
            return host;
        }
    }

    /// <summary>True when a host already exists for this owner.</summary>
    public static bool TryGet(string ownerGuid, out CompatHost? host)
    {
        host = null;
        if (string.IsNullOrEmpty(ownerGuid))
            return false;

        lock (Gate)
            return ByOwner.TryGetValue(ownerGuid, out host);
    }
}
