using System.Collections.Generic;
using DrakeModsLibs.API;

namespace DrakeModsLibs.Runtime;

internal static class CustomizeLibsRuntime
{
    internal static bool ShowItemStandItemNameWhenNoAccess { get; set; } = true;
    internal static IStackMergePolicy? StackMergePolicy { get; set; }
    internal static readonly List<IDisplayNameModifier> DisplayNameModifiers = new();
    internal static readonly List<IItemDisplayNameLayer> DisplayNameLayers = new();
    internal static bool DefaultDisplayLayersRegistered;
}
