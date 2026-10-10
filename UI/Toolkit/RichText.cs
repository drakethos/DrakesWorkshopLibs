using System;
using System.Collections.Generic;
using System.Text;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>What a toolbar button does to the selected text.</summary>
public enum RichOp
{
    ToggleBold,
    ToggleItalic,
    /// <summary>Colour from the argument (<c>#rrggbb</c>); toggles off when the whole selection already has it. Null argument = default colour.</summary>
    Color,
    /// <summary>Size from the argument (<c>80%</c>, <c>130%</c>); toggles off when the whole selection already has it.</summary>
    Size,
    /// <summary>Removes every tag from the selection.</summary>
    Clear,
}

/// <summary>The result of a formatting operation on raw text: the new text and where to put the selection (raw indices).</summary>
public readonly struct RichEdit
{
    public RichEdit(string text, int selectionStart, int selectionEnd)
    {
        Text = text;
        SelectionStart = selectionStart;
        SelectionEnd = selectionEnd;
    }

    public string Text { get; }
    public int SelectionStart { get; }
    public int SelectionEnd { get; }
}

/// <summary>One character's formatting. Colour and size are the tag values as written ("#e0ad4f", "130%"), or null.</summary>
public readonly struct RichStyle : IEquatable<RichStyle>
{
    public RichStyle(bool bold, bool italic, string? color, string? size)
    {
        Bold = bold;
        Italic = italic;
        Color = color;
        Size = size;
    }

    public bool Bold { get; }
    public bool Italic { get; }
    public string? Color { get; }
    public string? Size { get; }

    public bool IsPlain => !Bold && !Italic && Color == null && Size == null;

    public RichStyle With(bool? bold = null, bool? italic = null, string? color = null, string? size = null, bool setColor = false, bool setSize = false) =>
        new RichStyle(bold ?? Bold, italic ?? Italic, setColor ? color : Color, setSize ? size : Size);

    public bool Equals(RichStyle other) =>
        Bold == other.Bold && Italic == other.Italic &&
        string.Equals(Color, other.Color, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Size, other.Size, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is RichStyle s && Equals(s);
    public override int GetHashCode() => (Bold ? 1 : 0) | (Italic ? 2 : 0);
}

/// <summary>What a selection looks like as a whole, for lighting up toolbar buttons. Size and colour are null when they differ or are default.</summary>
public readonly struct RichSelection : IEquatable<RichSelection>
{
    public RichSelection(bool hasSelection, bool bold, bool italic, string? size, string? color)
    {
        HasSelection = hasSelection;
        Bold = bold;
        Italic = italic;
        Size = size;
        Color = color;
    }

    public bool HasSelection { get; }
    public bool Bold { get; }
    public bool Italic { get; }
    public string? Size { get; }
    public string? Color { get; }

    public bool Equals(RichSelection o) =>
        HasSelection == o.HasSelection && Bold == o.Bold && Italic == o.Italic &&
        string.Equals(Size, o.Size, StringComparison.OrdinalIgnoreCase) && string.Equals(Color, o.Color, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => obj is RichSelection s && Equals(s);
    public override int GetHashCode() => (HasSelection ? 1 : 0) | (Bold ? 2 : 0) | (Italic ? 4 : 0);
}

/// <summary>
/// Rich text as one style per visible character. The editor keeps the plain words in its text box and this model beside it; every edit
/// (typing, deleting, pasting, a toolbar button) goes through here, and <see cref="ToRaw"/> writes the tags back, nested and always closed.
/// </summary>
public sealed class RichModel
{
    public readonly List<char> Chars = new();
    public readonly List<RichStyle> Styles = new();
    /// <summary>Visible characters before each raw index (only filled by <see cref="FromRaw"/>).</summary>
    public int[] VisibleBefore = Array.Empty<int>();

    public int Length => Chars.Count;
    public string Plain => new string(Chars.ToArray());

    /// <summary>True when any character has a tag.</summary>
    public bool HasFormatting
    {
        get
        {
            foreach (var style in Styles)
                if (!style.IsPlain)
                    return true;
            return false;
        }
    }

    public static RichModel FromRaw(string? raw) => RichText.Parse(raw ?? "");

    public string ToRaw() => RichText.Write(this, out _);

    /// <summary>
    /// Only the colour tags. Drawn over the text box: colour never changes how wide a letter is, so the cursor stays under the right
    /// character (bold or a bigger size would not).
    /// </summary>
    public string ToColorOnlyRaw()
    {
        var copy = new RichModel();
        copy.Chars.AddRange(Chars);
        foreach (var style in Styles)
            copy.Styles.Add(new RichStyle(false, false, style.Color, null));
        return RichText.Write(copy, out _);
    }

    public RichModel Clone()
    {
        var copy = new RichModel();
        copy.Chars.AddRange(Chars);
        copy.Styles.AddRange(Styles);
        return copy;
    }

    /// <summary>
    /// Brings the model in line with new plain text after an edit: only what changed is touched, and new characters take the style of the
    /// text they were typed next to (or the text they replaced), like any editor.
    /// </summary>
    public void Remap(string newPlain)
    {
        var old = Plain;
        if (old == newPlain)
            return;

        var prefix = 0;
        while (prefix < old.Length && prefix < newPlain.Length && old[prefix] == newPlain[prefix])
            prefix++;
        var suffix = 0;
        while (suffix < old.Length - prefix && suffix < newPlain.Length - prefix &&
               old[old.Length - 1 - suffix] == newPlain[newPlain.Length - 1 - suffix])
            suffix++;

        var removed = old.Length - prefix - suffix;
        var inserted = newPlain.Length - prefix - suffix;
        RichStyle fill = default;
        if (removed > 0)
            fill = Styles[prefix];
        else if (prefix > 0)
            fill = Styles[prefix - 1];
        else if (old.Length > 0)
            fill = Styles[0];

        var tail = Styles.GetRange(old.Length - suffix, suffix);
        Styles.RemoveRange(prefix, Styles.Count - prefix);
        for (var i = 0; i < inserted; i++)
            Styles.Add(fill);
        Styles.AddRange(tail);

        Chars.Clear();
        Chars.AddRange(newPlain);
    }

    /// <summary>Applies <paramref name="op"/> to characters [from, to). False when the range is empty.</summary>
    public bool Apply(int from, int to, RichOp op, string? argument)
    {
        from = Math.Max(0, Math.Min(from, Length));
        to = Math.Max(0, Math.Min(to, Length));
        if (from >= to)
            return false;

        bool All(Func<RichStyle, bool> test)
        {
            for (var k = from; k < to; k++)
                if (!test(Styles[k]))
                    return false;
            return true;
        }

        switch (op)
        {
            case RichOp.ToggleBold:
            {
                var on = !All(s => s.Bold);
                for (var k = from; k < to; k++)
                    Styles[k] = Styles[k].With(bold: on);
                break;
            }
            case RichOp.ToggleItalic:
            {
                var on = !All(s => s.Italic);
                for (var k = from; k < to; k++)
                    Styles[k] = Styles[k].With(italic: on);
                break;
            }
            case RichOp.Color:
            {
                var already = argument != null && All(s => string.Equals(s.Color, argument, StringComparison.OrdinalIgnoreCase));
                var value = argument == null || already ? null : argument;
                for (var k = from; k < to; k++)
                    Styles[k] = Styles[k].With(color: value, setColor: true);
                break;
            }
            case RichOp.Size:
            {
                var already = argument != null && All(s => string.Equals(s.Size, argument, StringComparison.OrdinalIgnoreCase));
                var value = argument == null || already ? null : argument;
                for (var k = from; k < to; k++)
                    Styles[k] = Styles[k].With(size: value, setSize: true);
                break;
            }
            case RichOp.Clear:
                for (var k = from; k < to; k++)
                    Styles[k] = default;
                break;
        }

        return true;
    }

    /// <summary>The look of characters [from, to) as a whole.</summary>
    public RichSelection Describe(int from, int to)
    {
        from = Math.Max(0, Math.Min(from, Length));
        to = Math.Max(0, Math.Min(to, Length));
        if (from >= to)
            return default;

        var bold = true;
        var italic = true;
        var sizeSame = true;
        var colorSame = true;
        var first = Styles[from];
        for (var k = from; k < to; k++)
        {
            var s = Styles[k];
            bold &= s.Bold;
            italic &= s.Italic;
            sizeSame &= string.Equals(s.Size, first.Size, StringComparison.OrdinalIgnoreCase);
            colorSame &= string.Equals(s.Color, first.Color, StringComparison.OrdinalIgnoreCase);
        }

        return new RichSelection(true, bold, italic, sizeSame ? first.Size : null, colorSame ? first.Color : null);
    }
}

/// <summary>
/// Rich text tags (<c>&lt;b&gt; &lt;i&gt; &lt;color=#..&gt; &lt;size=..&gt;</c>) done safely. Text is read into a <see cref="RichModel"/>
/// (one style per visible character), edited there, and written back with the minimum number of correctly nested, always-closed tags.
/// That is why formatting can't leave a tag open (which would tint the rest of a tooltip), why broken pasted tags get repaired, and
/// why the length limit can count only what the player reads. Tags it doesn't know (<c>&lt;3</c>, <c>&lt;sprite&gt;</c>...) are plain characters.
/// </summary>
public static class RichText
{
    // ---------------------------------------------------------------- reading

    internal static RichModel Parse(string raw)
    {
        var doc = new RichModel { VisibleBefore = new int[raw.Length + 1] };
        var colors = new List<string>();
        var sizes = new List<string>();
        var bold = 0;
        var italic = 0;
        var i = 0;
        while (i < raw.Length)
        {
            if (raw[i] == '<')
            {
                var close = raw.IndexOf('>', i + 1);
                if (close > i && close - i <= 40 && TryTag(raw.Substring(i + 1, close - i - 1), colors, sizes, ref bold, ref italic))
                {
                    for (var k = i; k <= close; k++)
                        doc.VisibleBefore[k] = doc.Chars.Count;
                    i = close + 1;
                    continue;
                }
            }

            doc.VisibleBefore[i] = doc.Chars.Count;
            doc.Chars.Add(raw[i]);
            doc.Styles.Add(new RichStyle(bold > 0, italic > 0,
                colors.Count > 0 ? colors[colors.Count - 1] : null,
                sizes.Count > 0 ? sizes[sizes.Count - 1] : null));
            i++;
        }

        doc.VisibleBefore[raw.Length] = doc.Chars.Count;
        return doc;
    }

    static bool TryTag(string tag, List<string> colors, List<string> sizes, ref int bold, ref int italic)
    {
        var t = tag.Trim();
        if (t.Length == 0)
            return false;
        var lower = t.ToLowerInvariant();
        switch (lower)
        {
            case "b":
                bold++;
                return true;
            case "/b":
                bold = Math.Max(0, bold - 1);
                return true;
            case "i":
                italic++;
                return true;
            case "/i":
                italic = Math.Max(0, italic - 1);
                return true;
            case "/color":
                if (colors.Count > 0)
                    colors.RemoveAt(colors.Count - 1);
                return true;
            case "/size":
                if (sizes.Count > 0)
                    sizes.RemoveAt(sizes.Count - 1);
                return true;
        }

        if (lower.StartsWith("color=", StringComparison.Ordinal))
        {
            var value = t.Substring(6).Trim().Trim('"', '\'');
            if (value.Length == 0)
                return false;
            colors.Add(value);
            return true;
        }

        if (lower.StartsWith("size=", StringComparison.Ordinal))
        {
            var value = t.Substring(5).Trim().Trim('"', '\'');
            if (value.Length == 0)
                return false;
            sizes.Add(value);
            return true;
        }

        return false;
    }

    // ---------------------------------------------------------------- writing

    /// <summary>Outer to inner: colour, size, bold, italic. A fixed order lets neighbouring runs share their outer tags.</summary>
    static List<(string Open, string Close)> Stack(RichStyle s)
    {
        var list = new List<(string, string)>();
        if (s.Color != null)
            list.Add(("<color=" + s.Color + ">", "</color>"));
        if (s.Size != null)
            list.Add(("<size=" + s.Size + ">", "</size>"));
        if (s.Bold)
            list.Add(("<b>", "</b>"));
        if (s.Italic)
            list.Add(("<i>", "</i>"));
        return list;
    }

    /// <summary>Writes the document back. <paramref name="starts"/>[j] is the raw index of visible character j.</summary>
    internal static string Write(RichModel doc, out int[] starts)
    {
        var sb = new StringBuilder();
        starts = new int[doc.Chars.Count];
        var open = new List<(string Open, string Close)>();
        for (var j = 0; j < doc.Chars.Count; j++)
        {
            var target = Stack(doc.Styles[j]);
            var common = 0;
            while (common < open.Count && common < target.Count && open[common].Open.Equals(target[common].Open, StringComparison.OrdinalIgnoreCase))
                common++;
            for (var k = open.Count - 1; k >= common; k--)
                sb.Append(open[k].Close);
            open.RemoveRange(common, open.Count - common);
            for (var k = common; k < target.Count; k++)
            {
                sb.Append(target[k].Open);
                open.Add(target[k]);
            }

            starts[j] = sb.Length;
            sb.Append(doc.Chars[j]);
        }

        for (var k = open.Count - 1; k >= 0; k--)
            sb.Append(open[k].Close);
        return sb.ToString();
    }

    // ---------------------------------------------------------------- raw-text helpers

    /// <summary>The number of characters a player reads (tags don't count).</summary>
    public static int VisibleLength(string? raw) => string.IsNullOrEmpty(raw) ? 0 : Parse(raw!).Chars.Count;

    /// <summary>True when the text contains at least one real tag.</summary>
    public static bool HasTags(string? raw) => !string.IsNullOrEmpty(raw) && VisibleLength(raw) != raw!.Length;

    /// <summary>
    /// Keeps at most <paramref name="maxVisible"/> readable characters, and at most <paramref name="maxRaw"/> characters in total
    /// (a safety cap so tags can't bloat what is stored). Text already inside both limits is returned untouched; when it has to cut,
    /// the result is re-written with every tag closed.
    /// </summary>
    public static string Truncate(string raw, int maxVisible, int maxRaw)
    {
        if (string.IsNullOrEmpty(raw) || (maxVisible <= 0 && maxRaw <= 0))
            return raw;
        var doc = Parse(raw);
        var overVisible = maxVisible > 0 && doc.Chars.Count > maxVisible;
        if (!overVisible && (maxRaw <= 0 || raw.Length <= maxRaw))
            return raw;

        if (overVisible)
        {
            doc.Chars.RemoveRange(maxVisible, doc.Chars.Count - maxVisible);
            doc.Styles.RemoveRange(maxVisible, doc.Styles.Count - maxVisible);
        }

        var text = Write(doc, out _);
        while (maxRaw > 0 && text.Length > maxRaw && doc.Chars.Count > 0)
        {
            doc.Chars.RemoveAt(doc.Chars.Count - 1);
            doc.Styles.RemoveAt(doc.Styles.Count - 1);
            text = Write(doc, out _);
        }

        return text;
    }

    /// <summary>
    /// Applies <paramref name="op"/> to a selection given in raw-text indices (either order). Null when nothing is selected.
    /// The whole string comes back normalized: tags nested in a fixed order and all closed.
    /// </summary>
    public static RichEdit? Apply(string raw, int selectionA, int selectionB, RichOp op, string? argument = null)
    {
        raw ??= "";
        var lo = Math.Max(0, Math.Min(Math.Min(selectionA, selectionB), raw.Length));
        var hi = Math.Max(0, Math.Min(Math.Max(selectionA, selectionB), raw.Length));
        var doc = Parse(raw);
        var from = doc.VisibleBefore[lo];
        var to = doc.VisibleBefore[hi];
        if (!doc.Apply(from, to, op, argument))
            return null;

        var text = Write(doc, out var starts);
        return new RichEdit(text, starts[from], starts[to - 1] + 1);
    }
}
