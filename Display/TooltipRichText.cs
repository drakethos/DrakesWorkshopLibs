using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Jotunn.Managers;
using UnityEngine;

namespace DrakeModsLibs.Display;

/// <summary>Guards injected tooltip strings so unclosed rich-text tags do not bleed into the rest of the tooltip.</summary>
internal static class TooltipRichText
{
    private enum RtKind
    {
        Color,
        Size
    }

    /// <summary>
    /// Appends <c>&lt;/size&gt;</c> / <c>&lt;/color&gt;</c> for each unmatched open, in correct LIFO order.
    /// Handles Valheim / TMP-style <c>&lt;color=…&gt;</c> and common <c>&lt;#RRGGBB&gt;</c> shorthand for color.
    /// </summary>
    internal static string EnsureRichTextTagsClosedForTooltip(string? text)
    {
        if (text == null || text.Length == 0)
            return "";

        string s = text;
        var stack = new Stack<RtKind>();
        for (int i = 0; i < s.Length;)
        {
            if (s[i] != '<')
            {
                i++;
                continue;
            }

            int end = s.IndexOf('>', i);
            if (end < 0)
                break;

            string tag = s.Substring(i, end - i + 1);
            if (tag.Length >= 2 && tag[1] == '/')
            {
                if (tag.StartsWith("</color", StringComparison.OrdinalIgnoreCase))
                {
                    if (stack.Count > 0 && stack.Peek() == RtKind.Color)
                        stack.Pop();
                }
                else if (tag.StartsWith("</size", StringComparison.OrdinalIgnoreCase))
                {
                    if (stack.Count > 0 && stack.Peek() == RtKind.Size)
                        stack.Pop();
                }
            }
            else if (tag.StartsWith("<color", StringComparison.OrdinalIgnoreCase))
            {
                stack.Push(RtKind.Color);
            }
            else if (IsHashColorOpenTag(tag))
            {
                stack.Push(RtKind.Color);
            }
            else if (tag.StartsWith("<size", StringComparison.OrdinalIgnoreCase))
            {
                stack.Push(RtKind.Size);
            }

            i = end + 1;
        }

        if (stack.Count == 0)
            return s;

        var sb = new StringBuilder(s, s.Length + stack.Count * 10);
        while (stack.Count > 0)
        {
            var k = stack.Pop();
            sb.Append(k == RtKind.Color ? "</color>" : "</size>");
        }

        return sb.ToString();
    }

    /// <summary>Crafted-by display: if the player did not add any color markup, wrap the string in Valheim’s tooltip stat orange (same as <see cref="GUIManager.ValheimOrange"/>).</summary>
    internal static string WrapCraftedByDisplayWithDefaultStatColorIfNeeded(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        if (HasExplicitColorMarkup(text))
            return text;
        return GetValheimTooltipStatColorOpenTag() + text + "</color>";
    }

    internal static bool HasExplicitColorMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        if (text.IndexOf("<color", StringComparison.OrdinalIgnoreCase) >= 0)
            return true;

        for (int i = 0; i < text.Length - 3; i++)
        {
            if (text[i] != '<' || text[i + 1] != '#')
                continue;
            int j = i + 2;
            while (j < text.Length && IsHex(text[j]))
                j++;
            if (j == i + 2)
                continue;
            if (j - (i + 2) < 3 || j - (i + 2) > 8)
                continue;
            if (j < text.Length && text[j] == '>')
                return true;
        }

        return false;
    }

    static bool IsHex(char c) =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

    static string GetValheimTooltipStatColorOpenTag()
    {
        try
        {
            var g = GUIManager.Instance;
            if (g != null)
            {
                Color c = g.ValheimOrange;
                return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">";
            }
        }
        catch
        {
            /* GUIManager / ColorUtility unavailable */
        }

        // Matches Jotunn “Orange” preset used elsewhere in Drake mods when GUIManager is not ready.
        return "<color=#ff8800>";
    }

    private static bool IsHashColorOpenTag(string tag)
    {
        if (tag.Length < 5 || tag[0] != '<' || tag[1] != '#' || tag[tag.Length - 1] != '>')
            return false;
        string hex = tag.Substring(2, tag.Length - 3);
        return HexColorRegex.IsMatch(hex);
    }

    private static readonly Regex HexColorRegex = new Regex("^[0-9a-fA-F]{3,8}$", RegexOptions.Compiled);
}
