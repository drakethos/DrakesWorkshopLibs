using System;
using System.Collections.Generic;
using System.Text;
using DrakeModsLibs.API;
using DrakeModsLibs.Tags;

namespace DrakeModsLibs.Data;

internal static class ItemCustomDataReader
{
    public static bool TryGetRawValue(ItemDrop.ItemData? item, string key, out string? value)
    {
        value = null;
        if (item?.m_customData == null || string.IsNullOrEmpty(key)) return false;
        if (!item.m_customData.TryGetValue(key, out var raw)) return false;
        value = raw;
        return true;
    }

    /// <summary>Display-oriented value: tags as set/unset, text as stored string, unknown keys as raw.</summary>
    public static string? GetDisplayValue(ItemDrop.ItemData? item, string key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        if (DrakeCustomDataCatalog.TryGetField(key, out var field))
        {
            if (field.Kind == DrakeCustomDataKind.Tag)
                return DrakeTagManager.HasTag(item, key) ? "true" : null;
            return TryGetRawValue(item, key, out var raw) ? raw : null;
        }

        if (DrakeTagManager.HasTag(item, key)) return "true";
        return TryGetRawValue(item, key, out var unknown) ? unknown : null;
    }

    public static string Dump(ItemDrop.ItemData? item, ItemCustomDataDumpOptions? options)
    {
        options ??= new ItemCustomDataDumpOptions();
        var entries = CollectEntries(item, options);
        return options.Format == ItemCustomDataDumpFormat.Json
            ? DumpJson(item, entries, options)
            : DumpNeat(item, entries, options);
    }

    static List<DumpEntry> CollectEntries(ItemDrop.ItemData? item, ItemCustomDataDumpOptions options)
    {
        var entries = new List<DumpEntry>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void Add(string key, string? display, string modId, string? label)
        {
            if (!PassesFilter(key, modId, options)) return;
            if (display == null && !options.IncludeEmpty) return;
            if (!seen.Add(key)) return;
            entries.Add(new DumpEntry(key, display ?? "", modId, label));
        }

        foreach (var field in DrakeCustomDataCatalog.GetAllFields())
            Add(field.Key, GetDisplayValue(item, field.Key), field.ModId, field.Label);

        if (item?.m_customData != null && options.IncludeUnknownKeys)
        {
            foreach (var kv in item.m_customData)
            {
                if (seen.Contains(kv.Key)) continue;
                if (!PassesFilter(kv.Key, "(unregistered)", options)) continue;
                Add(kv.Key, GetDisplayValue(item, kv.Key), "(unregistered)", null);
            }
        }

        entries.Sort((a, b) =>
        {
            int mod = string.Compare(a.ModId, b.ModId, StringComparison.Ordinal);
            return mod != 0 ? mod : string.Compare(a.Key, b.Key, StringComparison.Ordinal);
        });
        return entries;
    }

    static bool PassesFilter(string key, string modId, ItemCustomDataDumpOptions options)
    {
        if (!string.IsNullOrEmpty(options.Key) &&
            !string.Equals(key, options.Key, StringComparison.Ordinal))
            return false;

        if (!string.IsNullOrEmpty(options.ModId) &&
            !string.Equals(modId, options.ModId, StringComparison.Ordinal))
            return false;

        if (options.DrakeKeysOnly && !DrakeCustomDataCatalog.IsDrakeKey(key))
            return false;

        return true;
    }

    static string DumpNeat(ItemDrop.ItemData? item, List<DumpEntry> entries, ItemCustomDataDumpOptions options)
    {
        var sb = new StringBuilder();
        if (options.IncludeItemSummary)
            sb.AppendLine(SummarizeItem(item));

        string? currentMod = null;
        foreach (var e in entries)
        {
            if (!string.Equals(currentMod, e.ModId, StringComparison.Ordinal))
            {
                if (sb.Length > 0 && sb[sb.Length - 1] != '\n') sb.AppendLine();
                sb.Append('[').Append(e.ModId).Append(']').AppendLine();
                currentMod = e.ModId;
            }

            sb.Append("  ").Append(e.Key);
            if (!string.IsNullOrEmpty(e.Label))
                sb.Append(" (").Append(e.Label).Append(')');
            sb.Append(" = ");
            sb.AppendLine(FormatNeatValue(e.Display));
        }

        if (entries.Count == 0)
            sb.AppendLine("(no matching custom data)");
        return sb.ToString().TrimEnd();
    }

    static string DumpJson(ItemDrop.ItemData? item, List<DumpEntry> entries, ItemCustomDataDumpOptions options)
    {
        var sb = new StringBuilder("{");
        if (options.IncludeItemSummary)
        {
            AppendJsonProp(sb, "item", SummarizeItem(item), first: true);
            sb.Append(',');
        }

        bool firstKey = !options.IncludeItemSummary;
        foreach (var e in entries)
        {
            if (!firstKey) sb.Append(',');
            AppendJsonProp(sb, e.Key, e.Display, first: firstKey);
            firstKey = false;
        }

        sb.Append('}');
        return sb.ToString();
    }

    static void AppendJsonProp(StringBuilder sb, string key, string value, bool first)
    {
        if (!first) sb.Append(',');
        sb.Append('"').Append(JsonEscape(key)).Append("\":\"").Append(JsonEscape(value)).Append('"');
    }

    static string FormatNeatValue(string display) =>
        display is "true" or "false" ? display : $"\"{display}\"";

    static string SummarizeItem(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null) return "item: (null)";
        string name = item.m_shared.m_name ?? "?";
        return $"item: {name} x{item.m_stack}";
    }

    static string JsonEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < ' ') sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    readonly struct DumpEntry(string key, string display, string modId, string? label)
    {
        public string Key { get; } = key;
        public string Display { get; } = display;
        public string ModId { get; } = modId;
        public string? Label { get; } = label;
    }
}

