using System;
using System.Collections.Generic;
using Jotunn.Managers;

namespace DrakeModsLibs.Integration;

/// <summary>Libs-owned strings for the unified inventory chord (multi-mode only).</summary>
public static class InventoryContextLocalization
{
    static bool _registered;

    public static void Register()
    {
        if (_registered)
            return;
        _registered = true;

        try
        {
            var localization = LocalizationManager.Instance?.GetLocalization();
            if (localization == null)
                return;

            localization.AddTranslation("English", new Dictionary<string, string>
            {
                { InventoryContextHints.CustomizeToken, InventoryContextHints.CustomizeFallback }
            });
        }
        catch (Exception)
        {
            // Localization may not be ready at Awake; hint falls back to English constant.
            _registered = false;
        }
    }
}
