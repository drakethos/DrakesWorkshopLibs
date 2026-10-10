using System;
using DrakeModsLibs.UI.Toolkit;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// RenameIt-standard wood confirm dialog (Are you sure?).
/// Sizes to its text: at default <see cref="DrakePanelOptions"/> short text is 300×178 with Yes/No at ±55,35.
/// Longer text grows the panel up to <c>MaxHeight</c>, then scrolls.
/// </summary>
public sealed class DrakeConfirmPanel
{
    // Body top sits this far below the panel top; the footer keeps room for the buttons below the body.
    const float BodyTop = 76f;
    const float BodyMinHeight = 64f;
    const float FooterHeight = 38f;
    const float TextInset = 14f;
    const float ButtonBottom = 35f;

    readonly string _panelName;
    readonly DrakePanelOptions _options;
    GameObject? _panel;
    RectTransform? _panelRt;
    Text? _titleText;
    ScrollRect? _bodyScroll;
    RectTransform? _bodyScrollRt;
    RectTransform? _bodyContent;
    Text? _bodyText;
    Button? _yesButton;
    Button? _noButton;
    Action? _onYes;
    Action? _onNo;
    UkConfirmView? _toolkit;
    /// <summary>
    /// True when <see cref="Show"/> acquired the BlockInput lock (nothing else was already blocking).
    /// Nested confirms over an open editor must not release the parent's lock on Close.
    /// </summary>
    bool _releaseInputOnClose;

    public DrakeConfirmPanel(string panelName = "drake_confirm_panel", DrakePanelOptions? options = null)
    {
        _panelName = string.IsNullOrEmpty(panelName) ? "drake_confirm_panel" : panelName;
        _options = options ?? new DrakePanelOptions();
    }

    public bool IsOpen => (_toolkit != null && _toolkit.IsOpen) || (_panel && _panel.activeSelf);

    float TextWidth => _options.Width - TextInset * 2f;

    public void Show(
        string title,
        string body,
        Action? onYes,
        Action? onNo = null,
        string yesLabel = "Yes",
        string noLabel = "No")
    {
        if (DrakeUiMode.Toolkit)
        {
            try
            {
                _toolkit ??= new UkConfirmView();
                _toolkit.Show(title, body, onYes, onNo, yesLabel, noLabel);
                return;
            }
            catch (Exception ex)
            {
                DrakeUiMode.ReportFailure(ex);
            }
        }

        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        Ensure();
        if (!_panel || _titleText == null || _bodyText == null || !_yesButton || !_noButton)
            return;

        _onYes = onYes;
        _onNo = onNo;
        _titleText.text = title ?? "";
        _bodyText.text = body ?? "";

        SetButtonLabel(_yesButton, yesLabel);
        SetButtonLabel(_noButton, noLabel);
        Layout();

        // If a parent wood panel already blocked input, Close must leave that block in place.
        _releaseInputOnClose = !DrakeGuiInput.IsBlocked;
        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        DrakeGuiInput.EnsureBlocked();
    }

    public void Close()
    {
        if (_toolkit != null && _toolkit.IsOpen)
        {
            _toolkit.Close();
            return;
        }

        if (_panel)
            _panel.SetActive(false);
        if (_releaseInputOnClose)
            DrakeGuiInput.EnsureUnblocked();
        _releaseInputOnClose = false;
        _onYes = null;
        _onNo = null;
    }

    /// <summary>
    /// Measures the body at its wrap width, then sizes the body viewport and the panel to fit.
    /// Text taller than the max height scrolls inside the viewport.
    /// </summary>
    void Layout()
    {
        if (!_panelRt || !_bodyText || !_bodyScrollRt || !_bodyScroll || !_bodyContent)
            return;

        var bodyRt = _bodyText.rectTransform;
        bodyRt.sizeDelta = new Vector2(TextWidth, bodyRt.sizeDelta.y);
        var textHeight = Mathf.Max(DrakeLayout.MeasureHeight(_bodyText), BodyMinHeight);

        var maxBody = Mathf.Max(BodyMinHeight, _options.MaxHeight - BodyTop - FooterHeight);
        var viewport = Mathf.Clamp(textHeight, BodyMinHeight, maxBody);
        var panelHeight = Mathf.Clamp(BodyTop + viewport + FooterHeight, _options.MinHeight, _options.MaxHeight);

        _panelRt.sizeDelta = new Vector2(_options.Width, panelHeight);
        _bodyScrollRt.sizeDelta = new Vector2(TextWidth, viewport);
        bodyRt.sizeDelta = new Vector2(TextWidth, textHeight);
        DrakeLayout.SetContentHeight(_bodyScroll, textHeight, viewport);
    }

    void Ensure()
    {
        if (_panel || GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        _panel = GUIManager.Instance.CreateWoodpanel(
            parent: GUIManager.CustomGUIFront.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: Vector2.zero,
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
            fontSize: 20,
            color: GUIManager.Instance.ValheimOrange,
            outline: true,
            outlineColor: Color.black,
            width: TextWidth,
            height: 44f,
            addContentSizeFitter: false).GetComponent<Text>();
        _titleText.alignment = TextAnchor.MiddleCenter;

        _bodyScroll = DrakeLayout.CreateScrollArea(
            _panel.transform,
            "body",
            TextWidth,
            BodyMinHeight,
            new Vector2(0f, -BodyTop),
            out _bodyContent);
        _bodyScrollRt = (RectTransform)_bodyScroll.transform;

        _bodyText = GUIManager.Instance.CreateText(
            text: "",
            parent: _bodyContent.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: Vector2.zero,
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: TextWidth,
            height: BodyMinHeight,
            addContentSizeFitter: false).GetComponent<Text>();
        _bodyText.rectTransform.pivot = new Vector2(0.5f, 1f);
        _bodyText.alignment = TextAnchor.UpperCenter;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        _yesButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Yes",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-_options.ButtonWidth / 2f, ButtonBottom),
            width: _options.ButtonWidth,
            height: _options.ButtonHeight));
        _yesButton.onClick.AddListener(() =>
        {
            var yes = _onYes;
            Close();
            yes?.Invoke();
        });

        _noButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "No",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(_options.ButtonWidth / 2f, ButtonBottom),
            width: _options.ButtonWidth,
            height: _options.ButtonHeight));
        _noButton.onClick.AddListener(() =>
        {
            var no = _onNo;
            Close();
            no?.Invoke();
        });

        DrakeButtonSfx.Soften(_panel);
        _panel.SetActive(false);
    }

    static void SetButtonLabel(Button button, string label)
    {
        var text = button.GetComponentInChildren<Text>(true);
        if (text)
            text.text = label;
    }
}
