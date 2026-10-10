using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>
/// The shared window for mod UIs. A dark card with a header (icon, title, subtitle, extra buttons, close), an optional tab row that
/// follows the inventory tab host, a body, an optional footer, a toast and hover tooltips. Drag it by the header; Esc closes the
/// topmost one; game input stays blocked while any window is open (and is released again when the last one closes).
/// Put your controls in <see cref="Body"/> and <see cref="Footer"/>; sizes come from content, nothing is placed by pixel.
/// </summary>
public sealed class UkWindow
{
    /// <summary>Normal windows. Dialogs sit above them.</summary>
    public const int WindowLayer = 10;
    /// <summary>Confirm / prompt dialogs: drawn over any window, so asking "are you sure?" from a window works.</summary>
    public const int DialogLayer = 30;

    static readonly List<UkWindow> OpenWindows = new();
    static bool _weBlockedInput;
    static int _orderCounter;
    static GameObject? _runner;

    readonly UkScreen _screen;
    readonly VisualElement _scrim;
    readonly VisualElement _iconSlot;
    readonly Label _title;
    readonly Label _subtitle;
    readonly VisualElement _tabs;
    readonly int _layer;
    string? _activeTabId;
    Vector2 _offset;
    bool _dragging;
    int _openOrder;

    /// <summary>The window itself (a column). Add popovers and overlays here so nothing below covers them.</summary>
    public VisualElement Card { get; }
    /// <summary>Row in the header, left of the close button: cog menus, extra buttons.</summary>
    public VisualElement HeaderActions { get; }
    public VisualElement Body { get; }
    public VisualElement Footer { get; }
    public UkTooltip Tips { get; }
    public UkToast Toast { get; }

    /// <summary>The player closed it (close button or Esc). Not raised by <see cref="CloseSilent"/>.</summary>
    public Action? Closed;

    /// <summary>
    /// Esc goes here first: return true when you used it (closing a popover, cancelling a pending action), false to let the window close.
    /// </summary>
    public Func<bool>? EscapeHandler;

    public bool IsOpen { get; private set; }

    /// <summary>Any <see cref="UkWindow"/> is open (a cue for other code to leave Esc or clicks alone).</summary>
    public static bool AnyOpen => OpenWindows.Count > 0;

    /// <param name="name">Unity object name (shows in debuggers and logs).</param>
    /// <param name="width">Window width in 1080p layout pixels.</param>
    /// <param name="layer">Higher draws on top: <see cref="WindowLayer"/> or <see cref="DialogLayer"/>.</param>
    /// <param name="dim">Softly darken the game behind it.</param>
    /// <param name="draggable">Let the header be dragged.</param>
    public UkWindow(string name, float width, int layer = WindowLayer, bool dim = true, bool draggable = true)
    {
        _layer = layer;
        _screen = UkScreen.Create(name);

        _scrim = new VisualElement().Fill(dim ? UkTheme.Scrim : Color.clear);
        _scrim.style.position = Position.Absolute;
        _scrim.style.left = _scrim.style.right = _scrim.style.top = _scrim.style.bottom = 0;
        _scrim.style.alignItems = Align.Center;
        _scrim.style.justifyContent = Justify.Center;
        _scrim.pickingMode = PickingMode.Position; // clicks stay inside the window; nothing behind it is hit by accident
        _scrim.Show(false);
        _screen.Root.Add(_scrim);
        // After the scrim, so tooltips draw above the window.
        Tips = new UkTooltip(_screen.Root);

        Card = new VisualElement().Column().Fill(UkTheme.Panel).Round(UkTheme.RadiusLarge).Border(UkTheme.Line);
        Card.style.width = width;
        Card.style.maxHeight = 1020;
        Card.style.overflow = Overflow.Hidden;
        _scrim.Add(Card);

        var header = new VisualElement().Row().Pad(16, 22).BorderBottom(UkTheme.LineSoft);
        _iconSlot = new VisualElement().Fixed();
        _iconSlot.style.marginRight = 14;
        _iconSlot.Show(false);
        header.Add(_iconSlot);
        var titles = new VisualElement().Column().Grow();
        _title = UkControls.Text("", 22, UkTheme.Text, heading: true, wrap: false);
        _subtitle = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        _subtitle.Show(false);
        titles.Add(_title);
        titles.Add(_subtitle);
        header.Add(titles);
        HeaderActions = new VisualElement().Row();
        header.Add(HeaderActions);
        var close = UkControls.MakeIconButton("✕", () => Close(), "Close (Esc)", UkButtonKind.Secondary);
        close.style.marginLeft = 8;
        header.Add(close);
        Card.Add(header);
        if (draggable)
            MakeDraggable(header);

        _tabs = new VisualElement().Row().Pad(10, 22).BorderBottom(UkTheme.LineSoft);
        _tabs.Show(false);
        Card.Add(_tabs);

        Body = new VisualElement().Column().Pad(18, 22);
        Card.Add(Body);

        Footer = new VisualElement().Row().Pad(14, 22).BorderTop(UkTheme.LineSoft).Fill(UkTheme.PanelDeep);
        Footer.Show(false);
        Card.Add(Footer);

        Toast = new UkToast();
        Toast.Root.style.marginLeft = Toast.Root.style.marginRight = 22;
        Toast.Root.style.marginBottom = 14;
        Card.Add(Toast.Root);
    }

    // ---------------------------------------------------------------- header, tabs, footer

    /// <summary>Title, an optional second line, and an optional item icon.</summary>
    public void SetHeader(string title, string? subtitle = null, Sprite? icon = null)
    {
        _title.text = title ?? "";
        _subtitle.text = subtitle ?? "";
        _subtitle.Show(!string.IsNullOrEmpty(subtitle));
        _iconSlot.Clear();
        _iconSlot.Show(icon != null);
        if (icon != null)
            _iconSlot.Add(UkControls.IconTile(icon, 52));
    }

    /// <summary>The footer row is only drawn when you ask for it (it holds the window's main buttons).</summary>
    public void ShowFooter(bool visible) => Footer.Show(visible);

    /// <summary>
    /// Shows the inventory tab host's tabs (Rename, Reskin, Lock...) as a row, the active one lit. Only when more than one mod has a
    /// tab for the item. Switching tabs closes this window quietly and opens the other mod's.
    /// </summary>
    public void ShowTabs(string? activeTabId)
    {
        _activeTabId = activeTabId;
        _tabs.Clear();
        var item = DrakeTabHost.CurrentItem;
        if (!DrakeTabHost.IsOpen || item == null)
        {
            _tabs.Show(false);
            return;
        }

        var tabs = DrakeTabHost.GetUsableTabs(item)
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Id, StringComparer.Ordinal)
            .ToList();
        _tabs.Show(tabs.Count > 1);
        foreach (var tab in tabs)
        {
            var id = tab.Id;
            var button = UkControls.MakeButton(tab.Title, () => SwitchTab(id), UkButtonKind.Ghost);
            button.style.height = 40;
            button.style.marginRight = 6;
            if (id == activeTabId)
                UkControls.SetKind(button, UkButtonKind.Selected);
            _tabs.Add(button);
        }

        // Our own tab row replaces the host's old strip (it comes back for tabs that haven't moved to this look).
        DrakeTabHost.HideStripForToolkit();
    }

    public void HideTabs()
    {
        _tabs.Clear();
        _tabs.Show(false);
    }

    void SwitchTab(string id)
    {
        if (id == _activeTabId)
            return;
        var item = DrakeTabHost.CurrentItem;
        if (item == null)
            return;
        CloseSilent();
        DrakeTabHost.OpenForItem(item, id);
    }

    // ---------------------------------------------------------------- open / close

    public void Open()
    {
        if (IsOpen)
        {
            _screen.RaiseAboveCanvases(_layer);
            return;
        }

        IsOpen = true;
        _openOrder = ++_orderCounter;
        OpenWindows.Add(this);
        // Block game input once for however many windows are open; only release what we took.
        if (OpenWindows.Count == 1 && !DrakeGuiInput.IsBlocked)
        {
            DrakeGuiInput.EnsureBlocked();
            _weBlockedInput = true;
        }

        EnsureRunner();
        _screen.RaiseAboveCanvases(_layer);
        _scrim.Show(true);
        ApplyOffset();
    }

    /// <summary>The player's close: raises <see cref="Closed"/>.</summary>
    public void Close() => Close(notify: true);

    /// <summary>Hide without raising <see cref="Closed"/> (switching tabs, swapping to another window).</summary>
    public void CloseSilent() => Close(notify: false);

    void Close(bool notify)
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        _scrim.Show(false);
        Tips.Hide();
        Toast.Hide();
        OpenWindows.Remove(this);
        if (OpenWindows.Count == 0 && _weBlockedInput)
        {
            DrakeGuiInput.EnsureUnblocked();
            _weBlockedInput = false;
        }

        if (notify)
            Closed?.Invoke();
    }

    /// <summary>Esc pressed while this is the topmost window.</summary>
    void HandleEscape()
    {
        if (EscapeHandler?.Invoke() == true)
            return;
        Close();
    }

    // ---------------------------------------------------------------- drag

    /// <summary>Drag the window by its header (not by the buttons in it). It stays inside the screen so it can't get lost.</summary>
    void MakeDraggable(VisualElement handle)
    {
        handle.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0 || e.target is Button)
                return;
            _dragging = true;
            handle.CapturePointer(e.pointerId);
            e.StopPropagation();
        });
        handle.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (!_dragging)
                return;
            _offset += new Vector2(e.deltaPosition.x, e.deltaPosition.y);
            ApplyOffset();
        });
        handle.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!_dragging)
                return;
            _dragging = false;
            handle.ReleasePointer(e.pointerId);
        });
        handle.RegisterCallback<PointerCaptureOutEvent>(_ => _dragging = false);
    }

    void ApplyOffset()
    {
        var rootW = _scrim.resolvedStyle.width;
        var rootH = _scrim.resolvedStyle.height;
        var cardW = Card.resolvedStyle.width;
        var cardH = Card.resolvedStyle.height;
        if (rootW > 0f && cardW > 0f)
        {
            var left = (rootW - cardW) * 0.5f;
            var top = (rootH - cardH) * 0.5f;
            _offset.x = Mathf.Clamp(_offset.x, -(left + cardW - 140f), rootW - 140f - left);
            _offset.y = Mathf.Clamp(_offset.y, -top, rootH - 70f - top);
        }

        Card.style.translate = new Translate(_offset.x, _offset.y);
    }

    // ---------------------------------------------------------------- Esc

    static void EnsureRunner()
    {
        if (_runner != null)
            return;
        _runner = new GameObject("drake_window_runner");
        UnityEngine.Object.DontDestroyOnLoad(_runner);
        _runner.AddComponent<WindowRunner>();
    }

    /// <summary>One Update for all windows: Esc closes the topmost.</summary>
    sealed class WindowRunner : MonoBehaviour
    {
        void Update()
        {
            if (OpenWindows.Count == 0 || !UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return;
            OpenWindows.OrderBy(w => w._layer).ThenBy(w => w._openOrder).Last().HandleEscape();
        }
    }
}
