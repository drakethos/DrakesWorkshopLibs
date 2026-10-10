using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>
/// The new-look versions of the shared dialog components. <see cref="DrakeWoodActionMenu"/>, <see cref="DrakeConfirmPanel"/> and
/// <see cref="DrakeTextPromptPanel"/> hand over to these when <see cref="DrakeUiMode.Toolkit"/> is on, so every mod that uses them
/// gets the new look without changing a line. Same behaviour, same callbacks.
/// </summary>
internal sealed class UkActionMenuView
{
    readonly UkWindow _window;
    Action? _onClosed;

    public UkActionMenuView()
    {
        _window = new UkWindow("drake_action_menu", 440f);
        _window.Closed = () =>
        {
            var closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        };
    }

    public bool IsOpen => _window.IsOpen;

    /// <summary>Opens, or refreshes in place when already open (menus re-render as their state changes).</summary>
    public void Open(string title, string? subtitle, IReadOnlyList<DrakeMenuAction> actions, string cancelLabel, Action? onClosed, bool showCancel)
    {
        _onClosed = onClosed;

        // Opened from the inventory tab host: show the item's icon and the tab row, like the other tab windows.
        var tabbed = DrakeTabHost.IsOpen && DrakeTabHost.CurrentItem != null && !string.IsNullOrEmpty(DrakeTabHost.ActiveTabId);
        _window.SetHeader(title, subtitle, tabbed ? DrakeTabHost.CurrentItem!.GetIcon() : null);
        if (tabbed)
            _window.ShowTabs(DrakeTabHost.ActiveTabId);
        else
            _window.HideTabs();

        _window.Body.Clear();
        var list = new VisualElement().Column();
        foreach (var action in actions)
        {
            var captured = action;
            var button = UkControls.MakeButton(captured.Label, () =>
            {
                if (!captured.Enabled)
                    return;
                try
                {
                    captured.OnClick();
                }
                catch (Exception)
                {
                    // Consumer should log; never dump into the UI click path.
                }
            }, UkButtonKind.Secondary);
            button.style.height = 46;
            button.style.marginBottom = 8;
            UkControls.SetButtonEnabled(button, captured.Enabled);
            list.Add(button);
        }

        if (actions.Count > 9)
        {
            var scroll = new ScrollView(ScrollViewMode.Vertical) { verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.maxHeight = 520;
            scroll.Add(list);
            _window.Body.Add(scroll);
        }
        else
        {
            _window.Body.Add(list);
        }

        _window.Footer.Clear();
        if (showCancel)
        {
            _window.Footer.Add(UkControls.Spacer());
            _window.Footer.Add(UkControls.MakeButton(string.IsNullOrEmpty(cancelLabel) ? "Close" : cancelLabel, () => _window.Close(), UkButtonKind.Secondary));
        }

        _window.ShowFooter(showCancel);
        _window.Open();
    }

    public void Close(bool invokeClosed)
    {
        if (invokeClosed)
            _window.Close();
        else
            _window.CloseSilent();
    }
}

/// <summary>Yes / No. Esc and the close button count as No.</summary>
internal sealed class UkConfirmView
{
    readonly UkWindow _window;
    readonly Label _body;
    readonly VisualElement _buttons;
    Action? _onYes;
    Action? _onNo;

    public UkConfirmView()
    {
        _window = new UkWindow("drake_confirm", 460f, UkWindow.DialogLayer, dim: true, draggable: false);
        _body = UkControls.Text("", 16, UkTheme.TextBody);
        _window.Body.Add(_body);
        _buttons = _window.Footer;
        _window.ShowFooter(true);
        _window.Closed = () =>
        {
            var no = _onNo;
            _onYes = null;
            _onNo = null;
            no?.Invoke();
        };
    }

    public bool IsOpen => _window.IsOpen;

    public void Show(string title, string body, Action? onYes, Action? onNo, string yesLabel, string noLabel)
    {
        _onYes = onYes;
        _onNo = onNo;
        _window.SetHeader(title ?? "");
        _body.text = body ?? "";

        _buttons.Clear();
        _buttons.Add(UkControls.Spacer());
        var no = UkControls.MakeButton(noLabel, Answer(no: true), UkButtonKind.Secondary);
        no.style.marginRight = 8;
        _buttons.Add(no);
        _buttons.Add(UkControls.MakeButton(yesLabel, Answer(no: false), IsDestructive(yesLabel) ? UkButtonKind.DangerFilled : UkButtonKind.Primary));
        _window.Open();
    }

    /// <summary>Hides it without answering (the caller is closing the thing that asked).</summary>
    public void Close()
    {
        _onYes = null;
        _onNo = null;
        _window.CloseSilent();
    }

    Action Answer(bool no) => () =>
    {
        var callback = no ? _onNo : _onYes;
        _onYes = null;
        _onNo = null;
        _window.CloseSilent();
        callback?.Invoke();
    };

    /// <summary>Red for the buttons that destroy something.</summary>
    static bool IsDestructive(string label)
    {
        foreach (var word in new[] { "reset", "delete", "clear", "remove", "recycle", "discard", "wipe" })
            if (label != null && label.StartsWith(word, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}

/// <summary>One line of text with OK / Cancel. Enter is OK, Esc is Cancel.</summary>
internal sealed class UkPromptView
{
    readonly UkWindow _window;
    readonly UkField _field;
    Action<string>? _onOk;
    Action? _onCancel;

    public UkPromptView()
    {
        _window = new UkWindow("drake_prompt", 460f, UkWindow.DialogLayer, dim: true, draggable: false);
        _field = new UkField("", multiline: false, tips: _window.Tips);
        _field.SubmitPressed = Ok;
        _window.Body.Add(_field.Root);
        _window.ShowFooter(true);
        _window.Closed = () =>
        {
            var cancel = _onCancel;
            _onOk = null;
            _onCancel = null;
            cancel?.Invoke();
        };
    }

    public bool IsOpen => _window.IsOpen;

    public void Show(string title, string initialText, Action<string> onOk, Action? onCancel, int charLimit, string okLabel, string cancelLabel)
    {
        _onOk = onOk;
        _onCancel = onCancel;
        _window.SetHeader(title ?? "");
        _field.SetMax(Math.Max(1, charLimit));
        _field.SetValue(initialText ?? "");

        _window.Footer.Clear();
        _window.Footer.Add(UkControls.Spacer());
        var cancel = UkControls.MakeButton(cancelLabel, () =>
        {
            var callback = _onCancel;
            Clear();
            _window.CloseSilent();
            callback?.Invoke();
        }, UkButtonKind.Secondary);
        cancel.style.marginRight = 8;
        _window.Footer.Add(cancel);
        _window.Footer.Add(UkControls.MakeButton(okLabel, Ok, UkButtonKind.Primary));
        _window.Open();
        // Focus once the window has been laid out.
        _window.Card.schedule.Execute(() => _field.Focus()).ExecuteLater(60);
    }

    public void Close()
    {
        Clear();
        _window.CloseSilent();
    }

    void Ok()
    {
        var value = (_field.Value ?? "").Trim();
        var ok = _onOk;
        Clear();
        _window.CloseSilent();
        ok?.Invoke(value);
    }

    void Clear()
    {
        _onOk = null;
        _onCancel = null;
    }
}
