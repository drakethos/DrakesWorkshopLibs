using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

public enum UkButtonKind
{
    /// <summary>The one main action in a view (brass fill).</summary>
    Primary,
    /// <summary>Outlined, for secondary actions.</summary>
    Secondary,
    /// <summary>No outline until hovered (close, tabs' neighbours).</summary>
    Ghost,
    /// <summary>Destructive but not confirmed yet (red outline).</summary>
    Danger,
    /// <summary>Destructive and confirmed (red fill).</summary>
    DangerFilled,
    /// <summary>The chosen one of a group: lit text on a raised tile. Stays lit under hover.</summary>
    Selected,
}

/// <summary>Small factory for the shared look. Anything layout-driven: sizes come from content and the caller, no pixel placement.</summary>
public static class UkControls
{
    public static Label Text(string text, int size = 16, Color? color = null, bool heading = false, bool wrap = true)
    {
        var label = new Label(text) { pickingMode = PickingMode.Ignore };
        label.style.fontSize = size;
        label.style.color = color ?? UkTheme.Text;
        label.style.whiteSpace = wrap ? WhiteSpace.Normal : WhiteSpace.NoWrap;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        if (heading)
            UkFonts.ApplyHeading(label);
        return label;
    }

    public static VisualElement Spacer()
    {
        var e = new VisualElement { pickingMode = PickingMode.Ignore };
        e.style.flexGrow = 1;
        return e;
    }

    public static VisualElement Divider() => new VisualElement { pickingMode = PickingMode.Ignore }.Size(height: 1).Fill(UkTheme.LineSoft).Fixed();

    public static Button MakeButton(string text, Action onClick, UkButtonKind kind = UkButtonKind.Secondary, float? width = null)
    {
        var button = new Button(onClick) { text = text };
        button.style.height = UkTheme.Target;
        button.style.paddingLeft = button.style.paddingRight = 18;
        button.style.paddingTop = button.style.paddingBottom = 0;
        button.style.marginLeft = button.style.marginRight = button.style.marginTop = button.style.marginBottom = 0;
        button.style.fontSize = 16;
        button.style.unityTextAlign = TextAnchor.MiddleCenter;
        button.style.flexShrink = 0;
        if (width.HasValue)
            button.style.width = width.Value;
        button.Round(UkTheme.Radius);
        button.userData = kind;
        UkFonts.ApplyHeading(button);
        Restyle(button, kind, hover: false);
        button.RegisterCallback<PointerEnterEvent>(_ => Restyle(button, button.userData is UkButtonKind now ? now : kind, hover: button.enabledSelf));
        button.RegisterCallback<PointerLeaveEvent>(_ => Restyle(button, button.userData is UkButtonKind now ? now : kind, hover: false));
        return button;
    }

    /// <summary>A square icon-only button (close, reset). Label is a short glyph; give <paramref name="tooltip"/> for hover text.</summary>
    public static Button MakeIconButton(string glyph, Action onClick, string tooltip, UkButtonKind kind = UkButtonKind.Secondary)
    {
        var button = MakeButton(glyph, onClick, kind, UkTheme.Target);
        button.style.paddingLeft = button.style.paddingRight = 0;
        button.style.fontSize = 18;
        button.tooltip = tooltip;
        return button;
    }

    static void Restyle(Button b, UkButtonKind kind, bool hover)
    {
        Color bg, fg, line;
        if (!b.enabledSelf)
        {
            // Disabled: a flat, readable muted tile (fading the brass fill made dark-on-brass text unreadable).
            b.style.backgroundColor = kind == UkButtonKind.Ghost ? Color.clear : UkTheme.Raised;
            b.style.color = UkTheme.TextMuted;
            b.Border(kind == UkButtonKind.Ghost ? Color.clear : UkTheme.LineSoft);
            return;
        }

        switch (kind)
        {
            case UkButtonKind.Primary:
                bg = hover ? UkTheme.AccentHover : UkTheme.Accent;
                fg = UkTheme.AccentText;
                line = bg;
                break;
            case UkButtonKind.Danger:
                bg = hover ? UkTheme.DangerBg : Color.clear;
                fg = UkTheme.DangerText;
                line = UkTheme.DangerLine;
                break;
            case UkButtonKind.DangerFilled:
                bg = hover ? new Color(0.86f, 0.4f, 0.26f) : UkTheme.Danger;
                fg = new Color(1f, 0.96f, 0.94f);
                line = bg;
                break;
            case UkButtonKind.Selected:
                bg = UkTheme.Active;
                fg = UkTheme.Accent;
                line = hover ? UkTheme.Accent : UkTheme.Line;
                break;
            case UkButtonKind.Ghost:
                bg = hover ? UkTheme.Active : Color.clear;
                fg = hover ? UkTheme.Text : UkTheme.TextMuted;
                line = Color.clear;
                break;
            default:
                bg = hover ? UkTheme.Active : Color.clear;
                fg = UkTheme.Text;
                line = UkTheme.Line;
                break;
        }

        b.style.backgroundColor = bg;
        b.style.color = fg;
        b.Border(line);
    }

    /// <summary>Changes a button's look (for example into or out of <see cref="UkButtonKind.Selected"/>) and keeps it through hover.</summary>
    public static void SetKind(Button b, UkButtonKind kind)
    {
        b.userData = kind;
        Restyle(b, kind, hover: false);
    }

    /// <summary>Greyed and unclickable (or back to normal). Keeps layout identical either way.</summary>
    public static void SetButtonEnabled(Button b, bool on)
    {
        b.SetEnabled(on);
        Restyle(b, b.userData is UkButtonKind kind ? kind : UkButtonKind.Secondary, hover: false);
    }

    public static VisualElement Card(Color? fill = null, bool border = true)
    {
        var card = new VisualElement().Fill(fill ?? UkTheme.PanelDeep).Round(UkTheme.Radius + 2);
        if (border)
            card.Border(UkTheme.LineSoft);
        return card;
    }

    /// <summary>Item icon box. <paramref name="icon"/> may be null (shows an empty tile).</summary>
    public static VisualElement IconTile(Sprite? icon, float size)
    {
        var tile = new VisualElement { pickingMode = PickingMode.Ignore }.Size(size, size).Fixed().Fill(UkTheme.Raised).Round(UkTheme.Radius);
        if (icon != null)
        {
            var image = new VisualElement { pickingMode = PickingMode.Ignore };
            image.style.flexGrow = 1;
            image.style.backgroundImage = new StyleBackground(icon);
            image.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
            image.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
            image.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            image.style.marginLeft = image.style.marginRight = image.style.marginTop = image.style.marginBottom = size * 0.1f;
            tile.Add(image);
        }

        return tile;
    }
}

/// <summary>
/// A labelled text input: title, live character count, optional reset, an error line, and a read-only look for "not allowed".
/// With <see cref="EnableRich"/> it becomes a rich text field that never shows tags: the box holds the plain words, a style model
/// (<see cref="RichModel"/>) remembers each character's colour / bold / size, and a coloured copy of the text is drawn over the box.
/// <see cref="Value"/> is always the finished text with tags (for saving and the preview); the box itself only ever holds words.
/// </summary>
public sealed class UkField
{
    readonly Label _count;
    readonly Label _error;
    readonly VisualElement _box;
    readonly Button _reset;
    readonly bool _multiline;
    readonly VisualElement _fieldHost;
    Label? _overlay;
    bool _inChange;
    int _max;
    string? _reason;
    bool _rich;
    RichModel _model = new();
    int _selA;
    int _selB;
    RichSelection _lastSelection;
    IVisualElementScheduledItem? _poll;
    bool _editable = true;
    bool _changed;
    bool _hasError;

    public VisualElement Root { get; }
    /// <summary>The row holding the input and its reset button; other controls (a label picker) can sit at the start of it.</summary>
    public VisualElement Line { get; }
    public TextField Input { get; }

    /// <summary>The text with its tags (rich fields) or just the text. This is what to save.</summary>
    public string Value => _rich ? _model.ToRaw() : (Input.value ?? "");

    /// <summary>How the current selection looks as a whole (rich fields), for lighting up the toolbar.</summary>
    public RichSelection CurrentSelection => _lastSelection;

    /// <summary>Raised when the input gains focus (the formatting toolbar follows the last field you were in).</summary>
    public Action? GotFocus;
    public Action<string>? Changed;
    /// <summary>Rich fields: the selection's look changed (a different selection, or a formatting button).</summary>
    public Action<RichSelection>? SelectionChanged;
    public Action? ResetClicked;
    public Action? SubmitPressed;

    public UkField(string title, bool multiline, int rows = 1, string? resetTooltip = null, UkTooltip? tips = null)
    {
        _multiline = multiline;
        Root = new VisualElement().Column();

        var head = new VisualElement().Row();
        head.Margin(bottom: 6);
        head.Add(UkControls.Text(title, 15, UkTheme.Text, heading: true));
        head.Add(UkControls.Spacer());
        _count = UkControls.Text("", 13, UkTheme.TextMuted, wrap: false);
        head.Add(_count);
        Root.Add(head);

        var line = Line = new VisualElement().Row(multiline ? Align.FlexStart : Align.Center);
        Root.Add(line);

        Input = new TextField { multiline = multiline, isDelayed = false };
        Input.style.flexGrow = 1;
        Input.style.flexShrink = 1;
        Input.style.minWidth = 0;
        Input.style.marginLeft = Input.style.marginRight = Input.style.marginTop = Input.style.marginBottom = 0;
        _box = Input.Q<VisualElement>("unity-text-input") ?? Input;
        _box.Fill(UkTheme.Field).Round(UkTheme.Radius).Pad(multiline ? 10 : 0, 14);
        _box.style.minHeight = multiline ? rows * 24 + 22 : UkTheme.Target + 2;
        _box.style.fontSize = 17;
        _box.style.color = UkTheme.Text;
        _box.style.unityTextAlign = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        _box.style.justifyContent = multiline ? Justify.FlexStart : Justify.Center;
        _box.style.whiteSpace = multiline ? WhiteSpace.Normal : WhiteSpace.NoWrap;
        UkFonts.ApplyBody(_box);
        Paint();
        Input.textSelection.selectionColor = new Color(0.88f, 0.68f, 0.31f, 0.38f);
        Input.textSelection.cursorColor = UkTheme.Gold;
        Input.RegisterValueChangedCallback(OnInputChanged);
        Input.RegisterCallback<KeyDownEvent>(e =>
        {
            if (!multiline && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
            {
                e.StopPropagation();
                SubmitPressed?.Invoke();
            }
        }, TrickleDown.TrickleDown);
        Input.RegisterCallback<FocusInEvent>(_ =>
        {
            GotFocus?.Invoke();
            if (!_rich)
                return;
            _poll?.Pause();
            _poll = Input.schedule.Execute(TrackSelection).Every(40);
        });
        // No final read on blur: by then the field may already have collapsed its selection. The last timed read is the real one.
        Input.RegisterCallback<FocusOutEvent>(_ => _poll?.Pause());
        // The coloured overlay (rich fields) goes beside the text field, never inside it: a text change inside the field's own
        // subtree bubbles up as an edit of the field and loops forever.
        _fieldHost = new VisualElement();
        _fieldHost.style.flexGrow = 1;
        _fieldHost.style.flexShrink = 1;
        _fieldHost.style.minWidth = 0;
        _fieldHost.Add(Input);
        line.Add(_fieldHost);

        _reset = UkControls.MakeButton("Reset", () => ResetClicked?.Invoke(), UkButtonKind.Secondary);
        _reset.tooltip = resetTooltip ?? "Reset to default";
        _reset.style.marginLeft = 8;
        _reset.style.height = multiline ? 44 : UkTheme.Target + 2;
        _reset.style.paddingLeft = _reset.style.paddingRight = 14;
        _reset.Show(false);
        line.Add(_reset);

        _error = UkControls.Text("", 14, UkTheme.DangerText);
        _error.style.marginTop = 6;
        _error.Show(false);
        Root.Add(_error);

        // Greyed out? Hovering anywhere on the field says why (the field itself stays enabled so it still gets the hover).
        tips?.Attach(Root, () => _editable ? null : _reason);
    }

    public void SetMax(int max)
    {
        _max = max;
        if (max > 0)
            Input.maxLength = max;
        UpdateCount();
    }

    /// <summary>
    /// Rich text mode: tags are hidden, the limit counts plain characters, formatting comes from <see cref="ApplyRich"/>,
    /// and colour shows live in the box. Call once, before the first <see cref="SetValue"/>.
    /// </summary>
    public void EnableRich()
    {
        if (_rich)
            return;
        _rich = true;
        // Set by reflection: the CI reference assemblies hide these ITextSelection members, the game has them.
        foreach (var name in new[] { "selectAllOnFocus", "selectAllOnMouseUp" })
        {
            object selection = Input.textSelection;
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
            var property = selection.GetType().GetProperty(name, flags);
            if (property != null)
                property.SetValue(selection, false);
            else
                selection.GetType().GetField(name, flags)?.SetValue(selection, false);
        }
        // The coloured copy below is what shows; the box keeps only the caret and the selection highlight.
        _box.style.color = Color.clear;

        var layer = new VisualElement { pickingMode = PickingMode.Ignore };
        layer.style.position = Position.Absolute;
        layer.style.left = layer.style.right = layer.style.top = layer.style.bottom = 0;
        _overlay = UkControls.Text("", 17, UkTheme.Text, wrap: _multiline);
        _overlay.style.unityTextAlign = _multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
        // Same inset as the box's text: 1 px border + 14 px side padding (+ 10 px top padding when it has several lines).
        _overlay.style.marginLeft = _overlay.style.marginRight = 15;
        _overlay.style.marginTop = _multiline ? 11 : 0;
        _overlay.style.flexGrow = _multiline ? 0 : 1;
        UkFonts.ApplyBody(_overlay);
        layer.style.justifyContent = _multiline ? Justify.FlexStart : Justify.Center;
        layer.Add(_overlay);
        _fieldHost.Add(layer);

        _model = RichModel.FromRaw(Input.value);
        Input.SetValueWithoutNotify(_model.Plain);
        RefreshOverlay();
        UpdateCount();
    }

    public void SetValue(string value)
    {
        if (_rich)
        {
            _model = RichModel.FromRaw(value);
            Input.SetValueWithoutNotify(_model.Plain);
            RefreshOverlay();
        }
        else
        {
            Input.SetValueWithoutNotify(value ?? "");
        }

        UpdateCount();
    }

    /// <summary>The player typed, deleted or pasted: keep the style model in step and tell the owner.</summary>
    void OnInputChanged(ChangeEvent<string> e)
    {
        if (_inChange)
            return;
        _inChange = true;
        try
        {
            HandleInputChanged(e.newValue ?? "");
        }
        finally
        {
            _inChange = false;
        }
    }

    void HandleInputChanged(string text)
    {
        if (_rich)
        {
            // Angle brackets would be read back as tags; formatting comes from the toolbar.
            if (text.IndexOf('<') >= 0 || text.IndexOf('>') >= 0)
            {
                text = text.Replace("<", "").Replace(">", "");
                Input.SetValueWithoutNotify(text);
                Input.textSelection.SelectRange(text.Length, text.Length);
                Hint("Use the formatting buttons for colour and style.");
            }

            _model.Remap(text);
            RefreshOverlay();
        }

        UpdateCount();
        Changed?.Invoke(Value);
    }

    void RefreshOverlay()
    {
        if (_overlay != null)
            _overlay.text = _model.ToColorOnlyRaw();
    }

    void TrackSelection()
    {
        var selection = Input.textSelection;
        _selA = selection.cursorIndex;
        _selB = selection.selectIndex;
        PublishSelection();
    }

    void PublishSelection()
    {
        if (!_rich)
            return;
        var info = _model.Describe(Mathf.Min(_selA, _selB), Mathf.Max(_selA, _selB));
        if (info.Equals(_lastSelection))
            return;
        _lastSelection = info;
        SelectionChanged?.Invoke(info);
    }

    /// <summary>Applies a formatting button to the last selection. Tags are written, nested and closed for you.</summary>
    public void ApplyRich(RichOp op, string? argument)
    {
        var from = Mathf.Min(_selA, _selB);
        var to = Mathf.Max(_selA, _selB);
        if (!_rich || from >= to)
        {
            Hint("Select some text first.");
            return;
        }

        // Tried on a copy: a pile of formatting must not bloat what is stored.
        var trial = _model.Clone();
        trial.Apply(from, to, op, argument);
        if (_max > 0 && trial.ToRaw().Length > _max * 3 + 200)
        {
            Hint("That's a lot of formatting. Clear some first.");
            return;
        }

        _model = trial;
        RefreshOverlay();
        UpdateCount();
        Changed?.Invoke(Value);

        // The box's own text never changed, so the selection is still right; give the field its focus back.
        Input.Focus();
        Input.textSelection.SelectRange(to, from);
        _selA = to;
        _selB = from;
        PublishSelection();
    }

    /// <summary>A quiet one-line message under the field that clears itself.</summary>
    void Hint(string message)
    {
        _error.text = message;
        _error.style.color = UkTheme.TextMuted;
        _error.Show(true);
        _error.schedule.Execute(() =>
        {
            if (!_hasError)
                _error.Show(false);
        }).ExecuteLater(2500);
    }

    /// <summary>Not allowed: dimmed and read-only; <paramref name="reason"/> shows as a tooltip on hover (no dead-end popup).</summary>
    public void SetEditable(bool editable, string? reason = null)
    {
        _editable = editable;
        _reason = reason;
        Input.isReadOnly = !editable;
        Input.focusable = editable;
        Paint();
        var textColor = editable ? UkTheme.Text : UkTheme.TextMuted;
        if (_rich && _overlay != null)
            _overlay.style.color = textColor;
        else
            _box.style.color = textColor;
        Root.style.opacity = editable ? 1f : 0.6f;
    }

    public void SetResetVisible(bool visible) => _reset.Show(visible && _editable);

    /// <summary>Accent outline while the value differs from what's saved.</summary>
    public void SetChanged(bool changed)
    {
        _changed = changed;
        Paint();
    }

    public void SetError(string? message)
    {
        _hasError = !string.IsNullOrEmpty(message);
        _error.style.color = UkTheme.DangerText;
        _error.text = message ?? "";
        _error.Show(_hasError);
        Paint();
    }

    public void Focus() => Input.Focus();

    void Paint()
    {
        Color line;
        if (_hasError)
            line = UkTheme.Danger;
        else if (_changed && _editable)
            line = UkTheme.Accent;
        else
            line = _editable ? UkTheme.Line : UkTheme.LineSoft;
        _box.Border(line);
    }

    void UpdateCount()
    {
        var length = (Input.value ?? "").Length;
        var note = _rich && _model.HasFormatting ? "  \u00B7  formatting not counted" : "";
        _count.text = _max > 0 ? $"{length} / {_max}{note}" : "";
    }
}

/// <summary>An on/off switch with a label and a one-line explanation.</summary>
public sealed class UkSwitch
{
    readonly VisualElement _track;
    readonly VisualElement _knob;
    readonly Label _hint;
    bool _on;
    bool _enabled = true;

    public VisualElement Root { get; }
    public Action<bool>? Changed;

    public UkSwitch(string title, string hint)
    {
        Root = new VisualElement().Row().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius + 2).Border(UkTheme.LineSoft).Pad(12, 16);

        var text = new VisualElement().Column().Grow();
        text.Add(UkControls.Text(title, 16, UkTheme.Text, heading: true));
        _hint = UkControls.Text(hint, 14, UkTheme.TextMuted);
        _hint.style.marginTop = 2;
        text.Add(_hint);
        Root.Add(text);

        _track = new VisualElement().Size(52, 30).Fixed().Round(15);
        _track.style.marginLeft = 14;
        _knob = new VisualElement { pickingMode = PickingMode.Ignore }.Size(24, 24).Round(12).Fill(UkTheme.Text);
        _knob.style.position = Position.Absolute;
        _knob.style.top = 3;
        _track.Add(_knob);
        Root.Add(_track);

        Root.RegisterCallback<ClickEvent>(_ =>
        {
            if (!_enabled)
                return;
            SetValue(!_on, notify: true);
        });
        Paint();
    }

    public void SetValue(bool on, bool notify = false)
    {
        _on = on;
        Paint();
        if (notify)
            Changed?.Invoke(on);
    }

    public void SetEnabled(bool enabled, string? hint = null)
    {
        _enabled = enabled;
        if (hint != null)
            _hint.text = hint;
        Root.style.opacity = enabled ? 1f : 0.55f;
    }

    void Paint()
    {
        _track.style.backgroundColor = _on ? UkTheme.Accent : UkTheme.Raised;
        _track.Border(_on ? UkTheme.Accent : UkTheme.Line);
        _knob.style.left = _on ? 25 : 3;
        _knob.style.backgroundColor = _on ? UkTheme.AccentText : UkTheme.TextMuted;
    }
}

/// <summary>
/// "Press once, it asks; press again, it does it." Replaces Yes/No popups. Clicking Keep, pressing Esc (<see cref="Cancel"/>)
/// or waiting a few seconds puts the button back.
/// </summary>
public sealed class UkConfirm
{
    const long AutoCancelMs = 6000;

    readonly Button _ask;
    readonly VisualElement _row;
    readonly Label _question;
    IVisualElementScheduledItem? _timer;

    public VisualElement Root { get; }
    public bool IsAsking => _row.style.display == DisplayStyle.Flex;

    public UkConfirm(string buttonText, string question, string confirmText, Action onConfirm)
    {
        Root = new VisualElement().Row();

        _ask = UkControls.MakeButton(buttonText, Ask, UkButtonKind.Secondary);
        Root.Add(_ask);

        _row = new VisualElement().Row().Fill(UkTheme.DangerBg).Round(UkTheme.Radius + 2).Border(UkTheme.DangerLine).Pad(4, 4);
        _row.style.paddingLeft = 14;
        _question = UkControls.Text(question, 15, new Color(0.95f, 0.82f, 0.77f));
        _question.style.maxWidth = 300;
        _question.style.marginRight = 10;
        _row.Add(_question);
        var keep = UkControls.MakeButton("Keep", Cancel, UkButtonKind.Ghost);
        keep.style.height = 38;
        var confirm = UkControls.MakeButton(confirmText, () =>
        {
            Cancel();
            onConfirm();
        }, UkButtonKind.DangerFilled);
        confirm.style.height = 38;
        _row.Add(keep);
        _row.style.display = DisplayStyle.None;
        confirm.style.marginLeft = 4;
        _row.Add(confirm);
        Root.Add(_row);
    }

    public void SetEnabled(bool enabled) => UkControls.SetButtonEnabled(_ask, enabled);

    public void Cancel()
    {
        _timer?.Pause();
        _row.style.display = DisplayStyle.None;
        _ask.style.display = DisplayStyle.Flex;
    }

    void Ask()
    {
        _ask.style.display = DisplayStyle.None;
        _row.style.display = DisplayStyle.Flex;
        _timer?.Pause();
        _timer = Root.schedule.Execute(Cancel);
        _timer.ExecuteLater(AutoCancelMs);
    }
}

/// <summary>A quiet "Saved" line with Undo and a draining bar. Replaces confirm dialogs after the fact.</summary>
public sealed class UkToast
{
    readonly Label _message;
    readonly Button _undo;
    readonly VisualElement _bar;
    IVisualElementScheduledItem? _timer;
    Action? _undoAction;
    float _startedAt;
    float _durationSeconds;

    public VisualElement Root { get; }

    public UkToast()
    {
        Root = new VisualElement().Column().Fill(UkTheme.Raised).Round(UkTheme.Radius + 2).Border(UkTheme.Line);
        Root.style.overflow = Overflow.Hidden;

        var row = new VisualElement().Row().Pad(8, 14);
        row.style.paddingRight = 8;
        _message = UkControls.Text("", 15, UkTheme.Text);
        _message.style.flexGrow = 1;
        row.Add(_message);
        _undo = UkControls.MakeButton("Undo", () =>
        {
            var action = _undoAction;
            Hide();
            action?.Invoke();
        }, UkButtonKind.Ghost);
        _undo.style.height = 38;
        _undo.style.color = UkTheme.Accent;
        row.Add(_undo);
        Root.Add(row);

        var track = new VisualElement { pickingMode = PickingMode.Ignore }.Size(height: 3).Fill(UkTheme.LineSoft);
        _bar = new VisualElement { pickingMode = PickingMode.Ignore }.Size(height: 3).Fill(UkTheme.Accent);
        track.Add(_bar);
        Root.Add(track);
        Root.style.display = DisplayStyle.None;
    }

    public void Show(string message, Action? undo, float seconds = 6f)
    {
        _message.text = message;
        _undoAction = undo;
        _undo.Show(undo != null);
        _startedAt = Time.realtimeSinceStartup;
        _durationSeconds = seconds;
        _bar.style.width = new Length(100f, LengthUnit.Percent);
        Root.style.display = DisplayStyle.Flex;
        _timer?.Pause();
        _timer = Root.schedule.Execute(Tick).Every(50);
    }

    public void Hide()
    {
        _timer?.Pause();
        Root.style.display = DisplayStyle.None;
        _undoAction = null;
    }

    void Tick()
    {
        var left = 1f - (Time.realtimeSinceStartup - _startedAt) / _durationSeconds;
        if (left <= 0f)
        {
            Hide();
            return;
        }

        _bar.style.width = new Length(left * 100f, LengthUnit.Percent);
    }
}

/// <summary>
/// A pick-one list: a button showing the current choice and a popover under it. The popover lives in a host element
/// near the top of the panel (so it draws above the rows below the button, which would otherwise cover it).
/// </summary>
public sealed class UkDropdown
{
    readonly VisualElement _host;
    readonly Button _button;
    readonly Label _text;
    readonly VisualElement _popover;
    readonly System.Collections.Generic.List<string> _options = new();
    int _index;
    bool _enabled = true;
    string? _reason;

    public VisualElement Root { get; }
    public int SelectedIndex => _index;
    /// <summary>The text of the chosen entry (what the button shows).</summary>
    public string SelectedText => _options.Count > 0 ? _options[_index] : "";
    public bool IsOpen => _popover.style.display == DisplayStyle.Flex;
    public Action<int>? Changed;

    public UkDropdown(VisualElement overlayHost, UkTooltip? tips = null, float width = 190f)
    {
        _host = overlayHost;
        Root = new VisualElement().Size(width: width).Fixed();

        _button = new Button(Toggle);
        _button.style.height = UkTheme.Target + 2;
        _button.style.width = width;
        _button.style.marginLeft = _button.style.marginRight = _button.style.marginTop = _button.style.marginBottom = 0;
        _button.style.paddingLeft = 14;
        _button.style.paddingRight = 12;
        _button.style.flexDirection = FlexDirection.Row;
        _button.style.alignItems = Align.Center;
        _button.style.justifyContent = Justify.SpaceBetween;
        _button.Fill(UkTheme.Field).Round(UkTheme.Radius);
        _text = UkControls.Text("", 16, UkTheme.Text, heading: true, wrap: false);
        _button.Add(_text);
        // A drawn chevron: the font has no arrow glyph.
        var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
        chevron.style.width = chevron.style.height = 8;
        chevron.style.borderRightWidth = chevron.style.borderBottomWidth = 2;
        chevron.style.borderRightColor = chevron.style.borderBottomColor = UkTheme.TextMuted;
        chevron.style.rotate = new Rotate(new Angle(45f, AngleUnit.Degree));
        chevron.style.marginBottom = 4;
        _button.Add(chevron);
        Root.Add(_button);
        Paint(changed: false);

        _popover = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius).Border(UkTheme.Line).Pad(4);
        _popover.style.position = Position.Absolute;
        _popover.Show(false);
        overlayHost.Add(_popover);

        tips?.Attach(Root, () => _enabled ? null : _reason);
    }

    public void SetOptions(System.Collections.Generic.IList<string> options, int selected)
    {
        _options.Clear();
        _options.AddRange(options);
        _index = Mathf.Clamp(selected, 0, Math.Max(0, _options.Count - 1));
        _text.text = _options.Count > 0 ? _options[_index] : "";
        Rebuild();
    }

    /// <summary>Choose an entry without raising <see cref="Changed"/> (loading saved state).</summary>
    public void SetIndex(int index)
    {
        _index = Mathf.Clamp(index, 0, Math.Max(0, _options.Count - 1));
        _text.text = _options.Count > 0 ? _options[_index] : "";
        Rebuild();
    }

    public void SetChanged(bool changed) => Paint(changed);

    /// <summary>Greyed out with a hover reason, like the fields.</summary>
    public void SetEnabled(bool enabled, string? reason = null)
    {
        _enabled = enabled;
        _reason = reason;
        Root.style.opacity = enabled ? 1f : 0.6f;
        if (!enabled)
            Close();
    }

    /// <summary>True when <paramref name="target"/> is part of this control (so an outside click can be told apart).</summary>
    public bool Contains(VisualElement? target) => target != null && (Root.Contains(target) || _popover.Contains(target));

    public void Close() => _popover.Show(false);

    void Toggle()
    {
        if (!_enabled)
            return;
        if (IsOpen)
        {
            Close();
            return;
        }

        var anchor = Root.worldBound;
        var host = _host.worldBound;
        _popover.style.left = anchor.x - host.x;
        _popover.style.top = anchor.yMax - host.y + 4;
        _popover.style.width = Mathf.Max(anchor.width, 190f);
        _popover.Show(true);
        _popover.BringToFront();
    }

    void Rebuild()
    {
        _popover.Clear();
        for (var i = 0; i < _options.Count; i++)
        {
            var captured = i;
            var row = UkControls.MakeButton(_options[i], () => Pick(captured), UkButtonKind.Ghost);
            row.style.height = 40;
            row.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.style.paddingLeft = 12;
            if (i == _index)
                row.style.color = UkTheme.Accent;
            _popover.Add(row);
        }
    }

    void Pick(int index)
    {
        Close();
        if (index == _index)
            return;
        _index = index;
        _text.text = _options[index];
        Rebuild();
        Changed?.Invoke(index);
    }

    void Paint(bool changed) => _button.Border(changed ? UkTheme.Accent : UkTheme.Line);
}

/// <summary>Small drawn icons (the font has no symbol glyphs). Painted with the vector API, so they take any colour and size.</summary>
public static class UkIcons
{
    /// <summary>A cog: a small ring with eight spokes.</summary>
    public static VisualElement Gear(float size, Color color)
    {
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = icon.style.height = size;
        icon.generateVisualContent += ctx =>
        {
            var p = ctx.painter2D;
            var c = new Vector2(size * 0.5f, size * 0.5f);
            p.lineWidth = Mathf.Max(1.5f, size * 0.09f);
            p.strokeColor = color;
            p.lineCap = LineCap.Round;
            p.BeginPath();
            p.Arc(c, size * 0.16f, 0f, 360f);
            p.Stroke();
            for (var i = 0; i < 8; i++)
            {
                var a = i * Mathf.PI / 4f;
                var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p.BeginPath();
                p.MoveTo(c + d * size * 0.30f);
                p.LineTo(c + d * size * 0.46f);
                p.Stroke();
            }
        };
        return icon;
    }

    /// <summary>The recycle mark: three chasing arrows round a circle, drawn (the font has no such glyph).</summary>
    public static VisualElement Recycle(float size, Color color)
    {
        var icon = new VisualElement { pickingMode = PickingMode.Ignore };
        icon.style.width = icon.style.height = size;
        icon.generateVisualContent += ctx =>
        {
            var p = ctx.painter2D;
            var c = new Vector2(size * 0.5f, size * 0.5f);
            var r = size * 0.36f;
            p.lineWidth = Mathf.Max(1.8f, size * 0.11f);
            p.strokeColor = color;
            p.fillColor = color;
            p.lineCap = LineCap.Round;
            for (var i = 0; i < 3; i++)
            {
                var start = i * 120f + 14f;
                var end = start + 82f;
                p.BeginPath();
                p.Arc(c, r, start, end);
                p.Stroke();
                // Arrow head at the end of each arc, pointing along the circle.
                var a = end * Mathf.Deg2Rad;
                var tip = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                var tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                var normal = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var head = size * 0.17f;
                p.BeginPath();
                p.MoveTo(tip + tangent * head);
                p.LineTo(tip + normal * head * 0.8f);
                p.LineTo(tip - normal * head * 0.8f);
                p.ClosePath();
                p.Fill();
            }
        };
        return icon;
    }
}

/// <summary>
/// A small floating panel anchored under a button (the cog menu). Like <see cref="UkDropdown"/>'s list it lives in a host element near
/// the top of the window and is brought to the front when opened, so rows below the button can't cover it.
/// </summary>
public sealed class UkPopover
{
    readonly VisualElement _host;
    readonly VisualElement _panel;
    readonly float _width;

    /// <summary>Put your controls in here.</summary>
    public VisualElement Content => _panel;
    public bool IsOpen => _panel.style.display == DisplayStyle.Flex;

    public UkPopover(VisualElement host, float width)
    {
        _host = host;
        _width = width;
        _panel = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius + 2).Border(UkTheme.Line).Pad(14);
        _panel.style.position = Position.Absolute;
        _panel.style.width = width;
        _panel.Show(false);
        host.Add(_panel);
    }

    /// <summary>Open under <paramref name="anchor"/>, right edges lined up; or close if already open.</summary>
    public void Toggle(VisualElement anchor)
    {
        if (IsOpen)
        {
            Close();
            return;
        }

        var a = anchor.worldBound;
        var h = _host.worldBound;
        _panel.style.top = a.yMax - h.y + 6f;
        _panel.style.left = Mathf.Max(8f, a.xMax - h.x - _width);
        _panel.Show(true);
        _panel.BringToFront();
    }

    public void Close() => _panel.Show(false);

    public bool Contains(VisualElement? target) => target != null && _panel.Contains(target);
}

/// <summary>A row of exclusive choices (a segmented control). The chosen one stays highlighted, hover or not.</summary>
public sealed class UkSegmented
{
    readonly System.Collections.Generic.List<Button> _buttons = new();
    int _index;

    public VisualElement Root { get; }
    public int SelectedIndex => _index;
    public Action<int>? Changed;

    public UkSegmented(params string[] labels)
    {
        Root = new VisualElement().Row().Fill(UkTheme.Raised).Round(UkTheme.Radius).Pad(4);
        for (var i = 0; i < labels.Length; i++)
        {
            var captured = i;
            var button = UkControls.MakeButton(labels[i], () => Pick(captured), UkButtonKind.Ghost);
            button.style.height = 36;
            button.style.flexGrow = 1;
            button.style.flexBasis = 0;
            button.style.paddingLeft = button.style.paddingRight = 6;
            button.style.fontSize = 14;
            if (i < labels.Length - 1)
                button.style.marginRight = 4;
            _buttons.Add(button);
            Root.Add(button);
        }

        Paint();
    }

    /// <summary>Choose an entry without raising <see cref="Changed"/> (loading saved state).</summary>
    public void SetIndex(int index)
    {
        _index = Mathf.Clamp(index, 0, Math.Max(0, _buttons.Count - 1));
        Paint();
    }

    void Pick(int index)
    {
        if (index == _index)
            return;
        _index = index;
        Paint();
        Changed?.Invoke(index);
    }

    void Paint()
    {
        for (var i = 0; i < _buttons.Count; i++)
            UkControls.SetKind(_buttons[i], i == _index ? UkButtonKind.Selected : UkButtonKind.Ghost);
    }
}

/// <summary>A small on/off pill: lit brass-on-dark when on. For short lists of options.</summary>
public sealed class UkChip
{
    readonly Button _button;
    bool _on;

    public VisualElement Root => _button;
    public bool IsOn => _on;
    public Action<bool>? Changed;

    public UkChip(string text)
    {
        _button = UkControls.MakeButton(text, () =>
        {
            _on = !_on;
            Paint();
            Changed?.Invoke(_on);
        }, UkButtonKind.Secondary);
        _button.style.height = 36;
        _button.style.paddingLeft = _button.style.paddingRight = 14;
        _button.style.fontSize = 14;
        _button.style.marginRight = 6;
        _button.style.marginBottom = 6;
    }

    public void SetOn(bool on)
    {
        _on = on;
        Paint();
    }

    void Paint() => UkControls.SetKind(_button, _on ? UkButtonKind.Selected : UkButtonKind.Secondary);
}
