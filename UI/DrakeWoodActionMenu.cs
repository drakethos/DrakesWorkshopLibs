using System;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// RenameIt-style centered wood action menu (Jotunn CreateWoodpanel / CreateButton).
/// Consumers supply title, optional subtitle, and action rows. Tab host comes later —
/// this is the shared chrome LockSmith (and eventually RenameIt) can reuse.
/// </summary>
public sealed class DrakeWoodActionMenu
{
    const float ButtonWidth = 200f;
    const float ButtonHeight = 32f;
    const float CancelWidth = 64f;
    const float RowGap = 40f;

    readonly string _panelName;
    GameObject? _panel;
    Text? _titleText;
    Text? _subtitleText;
    readonly List<Button> _actionButtons = new List<Button>();
    Button? _cancelButton;
    EscapeCloser? _escapeCloser;
    Action? _onClosed;
    bool _showCancel;

    public DrakeWoodActionMenu(string panelName = "drake_wood_action_menu")
    {
        _panelName = string.IsNullOrEmpty(panelName) ? "drake_wood_action_menu" : panelName;
    }

    public bool IsOpen => _panel && _panel.activeSelf;

    public void Open(
        string title,
        string? subtitle,
        IReadOnlyList<DrakeMenuAction> actions,
        string cancelLabel = "Cancel",
        Action? onClosed = null,
        bool showCancel = true)
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _onClosed = onClosed;
        _showCancel = showCancel;
        EnsurePanel(Math.Max(actions?.Count ?? 0, 1));
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
        if (_panel)
            _panel.SetActive(false);
        DrakeGuiInput.EnsureUnblocked();
        if (!invokeClosed)
            return;
        var closed = _onClosed;
        _onClosed = null;
        closed?.Invoke();
    }

    void EnsurePanel(int actionSlots)
    {
        if (_panel)
        {
            EnsureActionButtonCount(actionSlots);
            return;
        }

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        var height = 160f + actionSlots * RowGap;
        _panel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 0f),
            width: 320f,
            height: Mathf.Clamp(height, 280f, 480f),
            draggable: false);
        _panel.name = _panelName;

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
            width: ButtonWidth + 40f,
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
            width: ButtonWidth + 40f,
            height: 28f,
            addContentSizeFitter: false).GetComponent<Text>();
        _subtitleText.alignment = TextAnchor.MiddleCenter;
        _subtitleText.gameObject.SetActive(false);

        EnsureActionButtonCount(actionSlots);

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
        if (!_panel || GUIManager.Instance == null)
            return;

        while (_actionButtons.Count < needed)
        {
            var btn = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
                text: "",
                parent: _panel.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: Vector2.zero,
                width: ButtonWidth,
                height: ButtonHeight));
            _actionButtons.Add(btn);
        }
    }

    void Layout(int actionCount, bool hasSubtitle, bool showCancel)
    {
        if (!_panel)
            return;

        var startY = hasSubtitle ? 48f : 64f;
        for (var i = 0; i < _actionButtons.Count; i++)
        {
            var btn = _actionButtons[i];
            if (!btn || !btn.gameObject.activeSelf)
                continue;
            var rt = btn.GetComponent<RectTransform>();
            if (!rt)
                continue;
            rt.anchoredPosition = new Vector2(0f, startY - i * RowGap);
            rt.sizeDelta = new Vector2(ButtonWidth, ButtonHeight);
        }

        var panelRt = _panel.GetComponent<RectTransform>();
        if (panelRt)
        {
            var rows = Math.Max(actionCount, 1);
            var bottom = showCancel ? 150f : 110f;
            var h = bottom + rows * RowGap + (hasSubtitle ? 24f : 0f);
            panelRt.sizeDelta = new Vector2(320f, Mathf.Clamp(h, 240f, 520f));
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
