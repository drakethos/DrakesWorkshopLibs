using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>
/// One formatting bar for several text fields: bold, italic, smaller, larger, a row of colours with a real colour picker, and clear. It
/// acts on the field you were last in (shown at the right) and the tags are written, nested and closed for you (see <see cref="RichModel"/>).
/// Buttons light up for what the current selection already has. Its buttons never take keyboard focus, so pressing one doesn't drop your selection.
/// </summary>
public sealed class UkRichToolbar
{
    /// <summary>Quick colours, picked to read well on a dark tooltip. The picker covers everything else.</summary>
    static readonly (string Name, string Hex)[] Presets =
    {
        ("Gold", "#f3c873"),
        ("Orange", "#e8903f"),
        ("Red", "#d8584a"),
        ("Pink", "#e58fb6"),
        ("Purple", "#b992e6"),
        ("Blue", "#6aa8e0"),
        ("Teal", "#5cc6b8"),
        ("Green", "#7fbf6a"),
        ("White", "#ffffff"),
        ("Grey", "#9a9a9a"),
    };

    const string SmallerSize = "80%";
    const string LargerSize = "130%";
    const int RecentMax = 6;

    readonly UkTooltip? _tips;
    readonly System.Func<string>? _readRecent;
    readonly System.Action<string>? _writeRecent;
    readonly Label _target;
    readonly List<(string Title, UkField Field)> _fields = new();
    readonly Button _bold;
    readonly Button _italic;
    readonly Button _smaller;
    readonly Button _larger;
    readonly List<(Button Button, string Hex)> _dots = new();
    readonly VisualElement _recentRow;
    readonly Button _customButton;
    readonly UkPopover _pickerPopover;
    readonly UkColorPicker _picker;
    int _active;
    RichSelection _selection;

    public VisualElement Root { get; }
    public bool PickerOpen => _pickerPopover.IsOpen;

    /// <param name="host">Element near the top of the window that the colour picker pops up in (so nothing below covers it).</param>
    /// <param name="readRecent">Where the recently used colours are kept (comma separated <c>#rrggbb</c>, newest first); null = not remembered.</param>
    /// <param name="writeRecent">Saves that list.</param>
    public UkRichToolbar(UkTooltip? tips, VisualElement host, System.Func<string>? readRecent, System.Action<string>? writeRecent,
        params (string Title, UkField Field)[] fields)
    {
        _tips = tips;
        _readRecent = readRecent;
        _writeRecent = writeRecent;
        Root = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius).Border(UkTheme.LineSoft).Pad(5, 6);

        // Row 1: text styles, clear, and which field it is acting on.
        var styles = new VisualElement().Row();
        _bold = Tool("B", "Bold", () => Do(RichOp.ToggleBold, null), bold: true);
        _italic = Tool("I", "Italic", () => Do(RichOp.ToggleItalic, null), italic: true);
        _smaller = Tool("A", "Smaller text", () => Do(RichOp.Size, SmallerSize), fontSize: 13);
        _larger = Tool("A", "Larger text", () => Do(RichOp.Size, LargerSize), fontSize: 20);
        styles.Add(_bold);
        styles.Add(_italic);
        styles.Add(Divider());
        styles.Add(_smaller);
        styles.Add(_larger);
        styles.Add(Divider());
        styles.Add(Tool("Clear", "Remove formatting from the selection", () => Do(RichOp.Clear, null), width: 56, fontSize: 13));
        styles.Add(UkControls.Spacer());
        _target = UkControls.Text("", 13, UkTheme.TextMuted, wrap: false);
        _target.style.marginRight = 8;
        styles.Add(_target);
        Root.Add(styles);

        // Row 2: colours. Presets, the picker, then the colours you used recently.
        var colours = new VisualElement().Row();
        colours.style.marginTop = 2;
        foreach (var (name, hex) in Presets)
        {
            var dot = ColorDot(name, hex);
            _dots.Add((dot, hex));
            colours.Add(dot);
        }

        colours.Add(Divider());
        _customButton = RainbowButton();
        colours.Add(_customButton);
        _recentRow = new VisualElement().Row();
        colours.Add(_recentRow);
        Root.Add(colours);

        _picker = new UkColorPicker();
        _picker.Picked = hex =>
        {
            Do(RichOp.Color, hex);
            RememberColour(hex);
        };
        _pickerPopover = new UkPopover(host, 310f);
        _pickerPopover.Content.Add(_picker.Root);

        foreach (var field in fields)
        {
            var index = _fields.Count;
            _fields.Add(field);
            field.Field.GotFocus = () => SetActive(index);
            field.Field.SelectionChanged = info =>
            {
                if (_active == index)
                    ShowSelection(info);
            };
        }

        RebuildRecent();
        SetActive(0);
    }

    /// <summary>Closes the picker when a click lands outside it. Returns true if it closed.</summary>
    public bool CloseIfOutside(VisualElement? target)
    {
        if (!_pickerPopover.IsOpen || _pickerPopover.Contains(target) || (target != null && _customButton.Contains(target)))
            return false;
        _pickerPopover.Close();
        return true;
    }

    public void ClosePicker() => _pickerPopover.Close();

    void SetActive(int index)
    {
        if (index < 0 || index >= _fields.Count)
            return;
        _active = index;
        _target.text = "Formatting: " + _fields[index].Title;
        ShowSelection(_fields[index].Field.CurrentSelection);
    }

    void Do(RichOp op, string? argument)
    {
        if (_active >= 0 && _active < _fields.Count)
            _fields[_active].Field.ApplyRich(op, argument);
    }

    /// <summary>Lights the buttons for what the selection already has, so a second press visibly undoes it.</summary>
    void ShowSelection(RichSelection info)
    {
        _selection = info;
        UkControls.SetKind(_bold, info.HasSelection && info.Bold ? UkButtonKind.Selected : UkButtonKind.Ghost);
        UkControls.SetKind(_italic, info.HasSelection && info.Italic ? UkButtonKind.Selected : UkButtonKind.Ghost);
        UkControls.SetKind(_smaller, info.Size == SmallerSize ? UkButtonKind.Selected : UkButtonKind.Ghost);
        UkControls.SetKind(_larger, info.Size == LargerSize ? UkButtonKind.Selected : UkButtonKind.Ghost);
        foreach (var (button, hex) in _dots)
            UkControls.SetKind(button, string.Equals(info.Color, hex, System.StringComparison.OrdinalIgnoreCase) ? UkButtonKind.Selected : UkButtonKind.Ghost);
    }

    // ---------------------------------------------------------------- recent colours

    List<string> ReadRecent()
    {
        var list = new List<string>();
        foreach (var part in (_readRecent?.Invoke() ?? "").Split(new[] { ',', ';', ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var hex = part.StartsWith("#", System.StringComparison.Ordinal) ? part : "#" + part;
            if (UkColorPicker.TryParse(hex, out _) && !list.Contains(hex.ToLowerInvariant()))
                list.Add(hex.ToLowerInvariant());
        }

        return list;
    }

    void RememberColour(string hex)
    {
        hex = hex.ToLowerInvariant();
        foreach (var (_, preset) in _dots)
            if (string.Equals(preset, hex, System.StringComparison.OrdinalIgnoreCase))
                return;

        var list = ReadRecent();
        list.Remove(hex);
        list.Insert(0, hex);
        if (list.Count > RecentMax)
            list.RemoveRange(RecentMax, list.Count - RecentMax);
        _writeRecent?.Invoke(string.Join(",", list));
        RebuildRecent();
    }

    void RebuildRecent()
    {
        _recentRow.Clear();
        foreach (var hex in ReadRecent())
            _recentRow.Add(ColorDot("Recent colour", hex));
    }

    // ---------------------------------------------------------------- pieces

    VisualElement Divider() => new VisualElement { pickingMode = PickingMode.Ignore }.Size(1, 20).Fill(UkTheme.Line).Margin(7, 5, 7, 5);

    Button Tool(string text, string tip, System.Action onClick, bool bold = false, bool italic = false, float width = 36f, int fontSize = 16)
    {
        var button = UkControls.MakeButton(text, onClick, UkButtonKind.Ghost, width);
        button.focusable = false;
        button.style.height = 34;
        button.style.paddingLeft = button.style.paddingRight = 0;
        button.style.fontSize = fontSize;
        button.style.borderTopLeftRadius = button.style.borderTopRightRadius =
            button.style.borderBottomLeftRadius = button.style.borderBottomRightRadius = 7;
        if (bold || italic)
            button.style.unityFontStyleAndWeight = italic ? FontStyle.Italic : FontStyle.Bold;
        _tips?.Attach(button, () => tip);
        return button;
    }

    Button ColorDot(string name, string hex)
    {
        var button = UkControls.MakeButton("", () => Do(RichOp.Color, hex), UkButtonKind.Ghost, 30f);
        button.focusable = false;
        button.style.height = 34;
        button.style.paddingLeft = button.style.paddingRight = 0;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        var dot = new VisualElement { pickingMode = PickingMode.Ignore }.Size(18, 18).Round(9);
        dot.style.backgroundColor = UkTheme.Hex(hex.TrimStart('#'));
        dot.Border(new Color(1f, 1f, 1f, 0.18f));
        button.Add(dot);
        _tips?.Attach(button, () => name + " " + hex);
        return button;
    }

    /// <summary>The "any colour" button: a little rainbow disc that opens the picker.</summary>
    Button RainbowButton()
    {
        var button = UkControls.MakeButton("", () =>
        {
            _picker.SetColor(_selection.Color);
            _pickerPopover.Toggle(_customButton!);
        }, UkButtonKind.Ghost, 36f);
        button.focusable = false;
        button.style.height = 34;
        button.style.paddingLeft = button.style.paddingRight = 0;
        button.style.alignItems = Align.Center;
        button.style.justifyContent = Justify.Center;
        var disc = new VisualElement { pickingMode = PickingMode.Ignore }.Size(20, 20).Round(10);
        disc.style.backgroundImage = new StyleBackground(RainbowTexture());
        disc.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        disc.Border(new Color(1f, 1f, 1f, 0.35f));
        button.Add(disc);
        _tips?.Attach(button, () => "Pick any colour");
        return button;
    }

    static Texture2D? _rainbow;

    /// <summary>The shared "any colour" disc texture (a diagonal hue sweep), for buttons that open a colour picker.</summary>
    public static Texture2D RainbowTexture()
    {
        if (_rainbow != null)
            return _rainbow;
        // A diagonal hue sweep: reads as "all colours" at a glance.
        const int size = 32;
        _rainbow = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "drakes_rainbow" };
        var pixels = new Color32[size * size];
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
                pixels[y * size + x] = Color.HSVToRGB((x + y) / (2f * size), 0.75f, 1f);
        _rainbow.SetPixels32(pixels);
        _rainbow.Apply();
        return _rainbow;
    }
}
