using System;
using System.Collections.Generic;
using DrakeModsLibs.UI.Toolkit;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// RenameIt-style centered wood action menu (Jotunn CreateWoodpanel / CreateButton).
/// Consumers supply title, optional subtitle, and action rows. The panel sizes to its rows
/// up to <c>MaxHeight</c>; rows past that scroll. Tab host comes later —
/// this is the shared chrome LockSmith (and eventually RenameIt) can reuse.
/// </summary>
public sealed class DrakeWoodActionMenu
{
    const float CancelWidth = 64f;
    // Title (center -40, 36 tall) ends 58 down; subtitle (center -72, 28 tall) ends 86 down.
    const float HeaderTitleOnly = 58f;
    const float HeaderWithSubtitle = 86f;
    // Cancel button: center 36 up from the bottom, 28 tall → top edge 50 up.
    const float FooterWithCancel = 50f;
    const float FooterNoCancel = 12f;
    const float ContentPadding = 8f;

    readonly string _panelName;
    readonly DrakePanelOptions _options;
    GameObject? _panel;
    RectTransform? _panelRt;
    Text? _titleText;
    Text? _subtitleText;
    ScrollRect? _scroll;
    RectTransform? _scrollRt;
    RectTransform? _content;
    readonly List<Button> _actionButtons = new List<Button>();
    Button? _cancelButton;
    EscapeCloser? _escapeCloser;
    Action? _onClosed;
    bool _showCancel;
    UkActionMenuView? _toolkit;

    public DrakeWoodActionMenu(string panelName = "drake_wood_action_menu", DrakePanelOptions? options = null)
    {
        _panelName = string.IsNullOrEmpty(panelName) ? "drake_wood_action_menu" : panelName;
        _options = options ?? new DrakePanelOptions
        {
            Width = 320f,
            MinHeight = 240f,
            MaxHeight = 640f,
            ButtonWidth = 200f,
            ButtonHeight = 32f,
            RowGap = 40f,
        };
    }

    public bool IsOpen => (_toolkit != null && _toolkit.IsOpen) || (_panel && _panel.activeSelf);

    public void Open(
        string title,
        string? subtitle,
        IReadOnlyList<DrakeMenuAction> actions,
        string cancelLabel = "Cancel",
        Action? onClosed = null,
        bool showCancel = true)
    {
        // New look first (one switch for every mod); the classic wood panel below is the fallback.
        if (DrakeUiMode.Toolkit)
        {
            try
            {
                if (_panel)
                    _panel.SetActive(false);
                _toolkit ??= new UkActionMenuView();
                _toolkit.Open(title ?? "", subtitle, actions ?? Array.Empty<DrakeMenuAction>(), cancelLabel, onClosed, showCancel);
                return;
            }
            catch (Exception ex)
            {
                DrakeUiMode.ReportFailure(ex);
            }
        }

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _onClosed = onClosed;
        _showCancel = showCancel;
        EnsurePanel();
        if (!_panel || _titleText == null)
            return;

        _titleText.text = title ?? "";
        if (_subtitleText != null)
        {
            var hasSub = !string.IsNullOrEmpty(subtitle);
            _subtitleText.gameObject.SetActive(hasSub);
            if (hasSub)
                _subtitleText.text = subtitle;
        }

        var list = actions ?? Array.Empty<DrakeMenuAction>();
        EnsureActionButtonCount(list.Count);
        for (var i = 0; i < _actionButtons.Count; i++)
        {
            var btn = _actionButtons[i];
            if (!btn)
                continue;

            if (i >= list.Count)
            {
                btn.gameObject.SetActive(false);
                continue;
            }

            var action = list[i];
            btn.gameObject.SetActive(true);
            btn.interactable = action.Enabled;
            var label = btn.GetComponentInChildren<Text>(true);
            if (label)
                label.text = action.Label;

            btn.onClick.RemoveAllListeners();
            var captured = action;
            btn.onClick.AddListener(() =>
            {
                if (!captured.Enabled)
                    return;
                try
                {
                    captured.OnClick();
                }
                catch (Exception)
                {
                    // Consumer should log; never dump into UI click path.
                }
            });
        }

        if (_cancelButton)
        {
            _cancelButton.gameObject.SetActive(showCancel);
            if (showCancel)
            {
                var cancelLabelText = _cancelButton.GetComponentInChildren<Text>(true);
                if (cancelLabelText)
                    cancelLabelText.text = cancelLabel;
            }
        }

        Layout(list.Count, !string.IsNullOrEmpty(subtitle), showCancel);
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        DrakeGuiInput.EnsureBlocked();
    }

    public void Close() => Close(invokeClosed: true);

    /// <summary>Hide without firing <c>onClosed</c> (e.g. swapping to a sub-panel).</summary>
    public void CloseSilent() => Close(invokeClosed: false);

    void Close(bool invokeClosed)
    {
        if (_toolkit != null && _toolkit.IsOpen)
        {
            _toolkit.Close(invokeClosed);
            return;
        }

        if (_panel)
            _panel.SetActive(false);
        DrakeGuiInput.EnsureUnblocked();
        if (!invokeClosed)
            return;
        var closed = _onClosed;
        _onClosed = null;
        closed?.Invoke();
    }

    void EnsurePanel()
    {
        if (_panel)
            return;

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        // GUI root is rebuilt on logout/character swap: the old buttons died with the old panel.
        // Keeping their refs made the new panel think it had buttons and show none.
        _actionButtons.Clear();

        _panel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: _options.Width,
            height: _options.MinHeight,
            draggable: false);
        _panel.name = _panelName;
        _panelRt = _panel.GetComponent<RectTransform>();

        _titleText = GUIManager.Instance.CreateText(
            text: "",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -40f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 22,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: _options.ButtonWidth + 40f,
            height: 36f,
            addContentSizeFitter: false).GetComponent<Text>();
        _titleText.alignment = TextAnchor.MiddleCenter;

        _subtitleText = GUIManager.Instance.CreateText(
            text: "",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -72f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: new Color(1f, 0f, 1f),
            outline: true,
            outlineColor: Color.black,
            width: _options.ButtonWidth + 40f,
            height: 28f,
            addContentSizeFitter: false).GetComponent<Text>();
        _subtitleText.alignment = TextAnchor.MiddleCenter;
        _subtitleText.gameObject.SetActive(false);

        // Rows live in a scroll area between the header and the footer; Cancel stays on the panel.
        _scroll = DrakeLayout.CreateScrollArea(
            _panel.transform,
            "rows",
            _options.Width,
            0f,
            Vector2.zero,
            out var content);
        _content = content;
        _scrollRt = (RectTransform)_scroll.transform;

        _cancelButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(0f, 36f),
            width: CancelWidth,
            height: 28f));
        _cancelButton.onClick.AddListener(Close);

        _escapeCloser = _panel.AddComponent<EscapeCloser>();
        _escapeCloser.Bind(Close);

        DrakeButtonSfx.Soften(_panel);
        _panel.SetActive(false);
    }

    void EnsureActionButtonCount(int needed)
    {
        if (!_content || GUIManager.Instance == null)
            return;

        while (_actionButtons.Count < needed)
        {
            var btn = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
                text: "",
                parent: _content.transform,
                anchorMin: new Vector2(0.5f, 1f),
                anchorMax: new Vector2(0.5f, 1f),
                position: Vector2.zero,
                width: _options.ButtonWidth,
                height: _options.ButtonHeight));
            _actionButtons.Add(btn);
        }
    }

    void Layout(int actionCount, bool hasSubtitle, bool showCancel)
    {
        if (!_panelRt || !_scrollRt || !_scroll || !_content)
            return;

        // Size the panel to its rows first. Rows stack down from just under the header,
        // and the scroll area takes whatever room is left between header and footer.
        var header = (hasSubtitle ? HeaderWithSubtitle : HeaderTitleOnly) + ContentPadding;
        var footer = (showCancel ? FooterWithCancel : FooterNoCancel) + ContentPadding;
        var rowsHeight = Math.Max(actionCount, 1) * _options.RowGap;
        var h = Mathf.Clamp(header + rowsHeight + footer, _options.MinHeight, _options.MaxHeight);
        var viewportHeight = h - header - footer;

        _panelRt.sizeDelta = new Vector2(_options.Width, h);
        _scrollRt.anchoredPosition = new Vector2(0f, -header);
        _scrollRt.sizeDelta = new Vector2(_options.Width, viewportHeight);
        DrakeLayout.SetContentHeight(_scroll, rowsHeight, viewportHeight);

        // Buttons are top-anchored inside the content: row i's center sits RowGap/2 + i*RowGap below its top.
        for (var i = 0; i < _actionButtons.Count; i++)
        {
            var btn = _actionButtons[i];
            if (!btn || !btn.gameObject.activeSelf)
                continue;
            var rt = btn.GetComponent<RectTransform>();
            if (!rt)
                continue;
            rt.anchoredPosition = new Vector2(0f, -(_options.RowGap / 2f + i * _options.RowGap));
            rt.sizeDelta = new Vector2(_options.ButtonWidth, _options.ButtonHeight);
        }
    }

    sealed class EscapeCloser : MonoBehaviour
    {
        Action? _close;

        public void Bind(Action close) => _close = close;

        void Update()
        {
            if (!gameObject.activeInHierarchy || _close == null)
                return;
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                _close.Invoke();
        }
    }
}
