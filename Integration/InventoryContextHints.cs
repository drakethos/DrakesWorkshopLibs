using System;
using System.Text;
using System.Text.RegularExpressions;
using DrakeModsLibs.Input;
using DrakeModsLibs.UI;

namespace DrakeModsLibs.Integration;

/// <summary>
/// Composes the unified inventory interact hint from usable tabs.
/// Feature mods own single-mode phrases; Libs owns only the multi-mode (“customize”) verb.
/// </summary>
public static class InventoryContextHints
{
    public const string HintColor = "#ffff00";
    public const string CustomizeToken = "drake_inventory_hint_customize";
    public const string BindingId = "drake.inventory.context";

    static readonly Regex CompetingOptionsHint = new Regex(
        @"Right\s*Click\s+for\s+(?:Locksmith\s+)?options",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>English fallback when localization is not ready.</summary>
    public const string CustomizeFallback = "customize";

    public static string GetCustomizePhrase()
    {
        try
        {
            if (Localization.instance != null)
            {
                var loc = Localization.instance.Localize("$" + CustomizeToken);
                if (!string.IsNullOrEmpty(loc) && loc[0] != '$')
                    return loc;
            }
        }
        catch
        {
            /* fall through */
        }

        return CustomizeFallback;
    }

    /// <summary>
    /// Builds the yellow interact line for the current usable set, or null when usable count is 0.
    /// </summary>
    public static bool TryFormatInteractHint(ItemDrop.ItemData? item, out string line)
    {
        line = "";
        if (item == null)
            return false;

        var usable = DrakeTabHost.GetUsableTabs(item);
        if (usable.Count == 0)
            return false;

        string phrase;
        if (usable.Count == 1)
        {
            phrase = SafeHintPhrase(usable[0]);
            if (string.IsNullOrEmpty(phrase))
                phrase = GetCustomizePhrase();
        }
        else
        {
            phrase = GetCustomizePhrase();
        }

        var chord = MenuKeyBinding.FormatForDisplay(DrakeIntegrationConfig.InventoryOpenModifier, emptyFallback: "");
        var body = string.IsNullOrEmpty(chord)
            ? "Right Click to " + phrase
            : chord + " + Right Click to " + phrase;

        line = $"<color={HintColor}><b>{body}</b></color>";
        return true;
    }

    /// <summary>Remove legacy RenameIt / LockSmith competing “for options” lines.</summary>
    public static string StripCompetingOptionsHints(string tooltipText)
    {
        if (string.IsNullOrEmpty(tooltipText))
            return tooltipText;

        var lines = tooltipText.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder(tooltipText.Length);
        var wrote = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (IsCompetingOptionsLine(line))
                continue;
            if (wrote)
                sb.Append('\n');
            sb.Append(line);
            wrote = true;
        }

        return sb.ToString();
    }

    static bool IsCompetingOptionsLine(string line)
    {
        if (string.IsNullOrEmpty(line))
            return false;
        return CompetingOptionsHint.IsMatch(line);
    }

    static string SafeHintPhrase(DrakeTabRegistration reg)
    {
        try
        {
            return reg.GetHintPhrase?.Invoke()?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }
}
