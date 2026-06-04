using System;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using DrakesWorkshopLibs.Display;
using HarmonyLib;

namespace DrakesWorkshopLibs.Patches;


using DrakesWorkshopLibs.Data;
using DrakesWorkshopLibs.Runtime;
using DrakesWorkshopLibs.Stack;

/// <summary>
/// Replaces visible crafted-by text when <see cref="DrakeCustomDataKeys.CraftedByDisplay"/> and/or
/// <see cref="DrakeCustomDataKeys.CraftedByLineLabel"/> is set.
/// </summary>
/// <remarks>
/// Vanilla builds the line as <c>\n$item_crafter: {m_crafterName}</c> then localizes; the UI often wraps the name in
/// <c>&lt;color&gt;</c> tags, so a plain <see cref="string.Replace(string, string)"/> on <see cref="ItemDrop.ItemData.m_crafterName"/> misses.
/// Use plain reflection for lookup — <see cref="AccessTools.Method(System.Type,string,System.Type[])"/> logs HarmonyX warnings when a signature is absent.
/// </remarks>
internal static class ItemTooltipPatches
{
    /// <summary>
    /// Walk <see cref="ItemDrop.ItemData"/> and base types with <see cref="BindingFlags.DeclaredOnly"/> —
    /// <see cref="Type.GetMethods(BindingFlags)"/> with <c>NonPublic</c> does not return inherited private/internal members,
    /// so a <c>GetTooltip</c> moved to a base type can be missed by a flat scan on <see cref="ItemDrop.ItemData"/> alone.
    /// </summary>
    const BindingFlags DeclaredTooltipFlags =
        BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    internal static void Apply(Harmony harmony, ManualLogSource log)
    {
        var target = ResolveGetTooltipPatchTarget(typeof(ItemDrop.ItemData));

        if (target == null)
        {
            // InventoryGridTooltipPatch (CreateItemTooltip postfix) calls ApplyCraftedByDisplayToTooltipText after item.GetTooltip().
            log.LogDebug(
                "[DrakesWorkshopLibs] Crafted-by display: no patchable GetTooltip overload on ItemData hierarchy; " +
                "grid tooltips still apply crafted-by via CreateItemTooltip.");
            return;
        }

        harmony.Patch(target, postfix: SelectPostfix(target));
    }

    static MethodInfo? ResolveGetTooltipPatchTarget(Type leafType)
    {
        MethodInfo? staticThree = null;
        MethodInfo? instZero = null;
        MethodInfo? instIntBool = null;

        for (var t = leafType; t != null && t != typeof(object); t = t.BaseType)
        {
            foreach (var m in t.GetMethods(DeclaredTooltipFlags))
            {
                if (m.Name != "GetTooltip" || m.ReturnType != typeof(string))
                    continue;

                if (m.IsStatic)
                {
                    var p = m.GetParameters();
                    if (p.Length == 3 &&
                        p[0].ParameterType == typeof(ItemDrop.ItemData) &&
                        p[1].ParameterType == typeof(int) &&
                        p[2].ParameterType == typeof(bool))
                        staticThree ??= m;
                    continue;
                }

                var ip = m.GetParameters();
                if (ip.Length == 0)
                    instZero ??= m;
                else if (ip.Length == 2 &&
                         ip[0].ParameterType == typeof(int) &&
                         ip[1].ParameterType == typeof(bool))
                    instIntBool ??= m;
            }
        }

        return staticThree ?? instZero ?? instIntBool;
    }

    static HarmonyMethod SelectPostfix(MethodInfo target)
    {
        if (target.IsStatic)
            return new HarmonyMethod(typeof(ItemTooltipPatches), nameof(CraftedByDisplayStaticPostfix));

        var ps = target.GetParameters();
        if (ps.Length == 0)
            return new HarmonyMethod(typeof(ItemTooltipPatches), nameof(CraftedByDisplayInstancePostfix));
        if (ps.Length == 2 &&
            ps[0].ParameterType == typeof(int) &&
            ps[1].ParameterType == typeof(bool))
            return new HarmonyMethod(typeof(ItemTooltipPatches), nameof(CraftedByDisplayInstanceQualityCraftingPostfix));

        throw new InvalidOperationException("ResolveGetTooltipPatchTarget and SelectPostfix are out of sync.");
    }

    internal static void CraftedByDisplayStaticPostfix(
        ItemDrop.ItemData item,
        int qualityLevel,
        bool crafting,
        ref string __result)
    {
        __result = ApplyCraftedByDisplayToTooltipText(__result, item);
    }

    internal static void CraftedByDisplayInstancePostfix(ItemDrop.ItemData __instance, ref string __result)
    {
        __result = ApplyCraftedByDisplayToTooltipText(__result, __instance);
    }

    internal static void CraftedByDisplayInstanceQualityCraftingPostfix(
        ItemDrop.ItemData __instance,
        int qualityLevel,
        bool crafting,
        ref string __result)
    {
        __result = ApplyCraftedByDisplayToTooltipText(__result, __instance);
    }

    /// <summary>Inventory / shared: rewrite the crafted-by segment like description replacement (handles color tags + localization).</summary>
    internal static string ApplyCraftedByDisplayToTooltipText(string text, ItemDrop.ItemData item)
    {
        if (string.IsNullOrEmpty(text) || item?.m_customData == null)
            return text;
        if (item.m_crafterID == 0L)
            return text;
        var oldName = item.m_crafterName ?? "";
        if (string.IsNullOrEmpty(oldName))
            return text;

        bool hasDisp = item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByDisplay, out var dispRaw) &&
                       !string.IsNullOrEmpty(dispRaw);
        bool hasLine = item.m_customData.TryGetValue(DrakeCustomDataKeys.CraftedByLineLabel, out var lineRaw) &&
                       !string.IsNullOrEmpty(lineRaw);
        if (!hasDisp && !hasLine)
            return text;

        string displayPart = hasDisp ? dispRaw! : oldName;
        if (hasDisp)
        {
            displayPart = TooltipRichText.EnsureRichTextTagsClosedForTooltip(displayPart);
            displayPart = TooltipRichText.WrapCraftedByDisplayWithDefaultStatColorIfNeeded(displayPart);
        }

        string? lineOverride = hasLine ? lineRaw : null;
        return ReplaceCraftedBySegment(text, oldName, displayPart, lineOverride);
    }

    /// <summary>Match vanilla <c>\n$item_crafter: name</c>, localized "Crafted by: …", and common &lt;color&gt; wrapping around the name.</summary>
    internal static string ReplaceCraftedBySegment(string text, string oldName, string newDisplay, string? lineLabelOverride = null)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(oldName))
            return text;

        if (!string.IsNullOrEmpty(lineLabelOverride))
        {
            var custom = TryReplaceCraftedByWithCustomLineLabel(text, oldName, newDisplay, lineLabelOverride!);
            if (custom != null)
                return custom;
        }

        // Exact fragment from ItemData.GetTooltip StringBuilder (often before full localization pass)
        var tokenSpaced = "\n$item_crafter: " + oldName + " ";
        if (text.Contains(tokenSpaced))
            return text.Replace(tokenSpaced, "\n$item_crafter: " + newDisplay + " ");
        var tokenTight = "\n$item_crafter: " + oldName;
        if (text.Contains(tokenTight))
            return text.Replace(tokenTight, "\n$item_crafter: " + newDisplay);

        string label = "Crafted by";
        if (Localization.instance != null)
        {
            var loc = Localization.instance.Localize("$item_crafter");
            if (!string.IsNullOrEmpty(loc))
                label = loc;
        }

        // Plain localized line (no color tags)
        foreach (var suffix in new[] { " ", "" })
        {
            var needle = "\n" + label + ": " + oldName + suffix;
            if (text.Contains(needle))
                return text.Replace(needle, "\n" + label + ": " + newDisplay + (suffix == " " ? " " : ""));
        }

        // Name wrapped in <color=…>…</color> after "Label:"
        try
        {
            string escLabel = Regex.Escape(label);
            string escOld = Regex.Escape(oldName);
            var rxColor = new Regex(
                @"(?<prefix>\n[^\n]*?" + escLabel + @"\s*:\s*)(?:<color[^>]*>)?" + escOld + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxColor.IsMatch(text))
                return rxColor.Replace(text, m => m.Groups["prefix"].Value + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        // Same line, $item_crafter key still present but localized label unknown
        try
        {
            var rxTok = new Regex(
                @"(?<prefix>\n[^\n]*?\$item_crafter\s*:\s*)(?:<color[^>]*>)?" + Regex.Escape(oldName) + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxTok.IsMatch(text))
                return rxTok.Replace(text, m => m.Groups["prefix"].Value + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        // First line that looks like crafted-by: replace only that occurrence of oldName
        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.IndexOf("$item_crafter", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (line.IndexOf(oldName, StringComparison.Ordinal) < 0)
                continue;
            int idx = line.IndexOf(oldName, StringComparison.Ordinal);
            lines[i] = line.Remove(idx, oldName.Length).Insert(idx, newDisplay);
            return string.Join("\n", lines);
        }

        return text;
    }

    static string? TryReplaceCraftedByWithCustomLineLabel(string text, string oldName, string newDisplay, string lineLabelOverride)
    {
        var tokenSpaced = "\n$item_crafter: " + oldName + " ";
        if (text.Contains(tokenSpaced))
            return text.Replace(tokenSpaced, "\n" + lineLabelOverride + ": " + newDisplay + " ");
        var tokenTight = "\n$item_crafter: " + oldName;
        if (text.Contains(tokenTight))
            return text.Replace(tokenTight, "\n" + lineLabelOverride + ": " + newDisplay);

        string label = "Crafted by";
        if (Localization.instance != null)
        {
            var loc = Localization.instance.Localize("$item_crafter");
            if (!string.IsNullOrEmpty(loc))
                label = loc;
        }

        foreach (var suffix in new[] { " ", "" })
        {
            var needle = "\n" + label + ": " + oldName + suffix;
            if (text.Contains(needle))
                return text.Replace(needle, "\n" + lineLabelOverride + ": " + newDisplay + (suffix == " " ? " " : ""));
        }

        try
        {
            string escLabel = Regex.Escape(label);
            string escOld = Regex.Escape(oldName);
            var rxColor = new Regex(
                @"(?<whole>\n[^\n]*?" + escLabel + @"\s*:\s*)(?:<color[^>]*>)?" + escOld + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxColor.IsMatch(text))
                return rxColor.Replace(text, _ => "\n" + lineLabelOverride + ": " + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        try
        {
            var rxTok = new Regex(
                @"(?<whole>\n[^\n]*?\$item_crafter\s*:\s*)(?:<color[^>]*>)?" + Regex.Escape(oldName) + @"(?:</color>)?",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(250));
            if (rxTok.IsMatch(text))
                return rxTok.Replace(text, _ => "\n" + lineLabelOverride + ": " + newDisplay);
        }
        catch (RegexMatchTimeoutException)
        {
            /* ignore */
        }

        var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.IndexOf("$item_crafter", StringComparison.OrdinalIgnoreCase) < 0 &&
                line.IndexOf(label, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (line.IndexOf(oldName, StringComparison.Ordinal) < 0)
                continue;
            lines[i] = lineLabelOverride + ": " + newDisplay;
            return string.Join("\n", lines);
        }

        return null;
    }
}
