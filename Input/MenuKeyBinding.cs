using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DrakeModsLibs.Input;

/// <summary>Shared parser for modifier chords (Shift, Ctrl+Alt, F1, None).</summary>
public static class MenuKeyBinding
{
    public static bool IsHeld(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return false;

        var trimmed = binding!.Trim();
        if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            return true;

        foreach (var token in SplitTokens(trimmed))
        {
            if (!IsTokenHeld(token))
                return false;
        }

        return true;
    }

    public static string FormatForDisplay(string? binding, string emptyFallback = "Key")
    {
        if (string.IsNullOrWhiteSpace(binding))
            return emptyFallback;

        var trimmed = binding!.Trim();
        if (string.Equals(trimmed, "None", StringComparison.OrdinalIgnoreCase))
            return "";

        var tokens = SplitTokens(trimmed).ToList();
        return tokens.Count == 0 ? trimmed : string.Join(" + ", tokens.Select(FormatToken));
    }

    static IEnumerable<string> SplitTokens(string binding) =>
        binding.Split(new[] { '+', ',', '&', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0);

    static bool IsTokenHeld(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "shift":
                return UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift);
            case "ctrl":
            case "control":
                return UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl);
            case "alt":
                return UnityEngine.Input.GetKey(KeyCode.LeftAlt) || UnityEngine.Input.GetKey(KeyCode.RightAlt);
            default:
                return Enum.TryParse(token, true, out KeyCode key) && UnityEngine.Input.GetKey(key);
        }
    }

    static string FormatToken(string token)
    {
        switch (token.ToLowerInvariant())
        {
            case "shift": return "Shift";
            case "ctrl":
            case "control": return "Ctrl";
            case "alt": return "Alt";
            default:
                return Enum.TryParse(token, true, out KeyCode key) ? key.ToString() : token;
        }
    }
}

