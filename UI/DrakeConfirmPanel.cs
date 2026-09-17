using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// RenameIt-standard wood confirm dialog (Are you sure?).
/// Metrics match RenameIt reset-all: 300×178, Yes/No at ±55,35.
/// </summary>
public sealed class DrakeConfirmPanel
{
    const float Width = 300f;
    const float Height = 178f;
    const float TextWidth = 272f;

    readonly string _panelName;
    GameObject? _panel;
    Text? _titleText;
    Text? _bodyText;
    Button? _yesButton;
    Button? _noButton;
    Action? _onYes;
    Action? _onNo;

    public DrakeConfirmPanel(string panelName = "drake_confirm_panel")
    {
        _panelName = string.IsNullOrEmpty(panelName) ? "drake_confirm_panel" : panelName;
    }

    public bool IsOpen => _panel && _panel.activeSelf;

    public void Show(
        string title,
        string body,
        Action? onYes,
        Action? onNo = null,
        string yesLabel = "Yes",
        string noLabel = "No")
    {
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

        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        DrakeGuiInput.EnsureBlocked();
    }

    public void Close()
    {
        if (_panel)
            _panel.SetActive(false);
        DrakeGuiInput.EnsureUnblocked();
        _onYes = null;
        _onNo = null;
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
            width: Width,
            height: Height,
            draggable: false);
        _panel.name = _panelName;

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

        _bodyText = GUIManager.Instance.CreateText(
            text: "",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 1f),
            anchorMax: new Vector2(0.5f, 1f),
            position: new Vector2(0f, -108f),
            font: GUIManager.Instance.AveriaSerifBold,
            fontSize: 14,
            color: Color.white,
            outline: true,
            outlineColor: Color.black,
            width: TextWidth,
            height: 64f,
            addContentSizeFitter: false).GetComponent<Text>();
        _bodyText.alignment = TextAnchor.UpperCenter;
        _bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _bodyText.verticalOverflow = VerticalWrapMode.Overflow;

        _yesButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Yes",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-55f, 35f),
            width: 110f,
            height: 30f));
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
            position: new Vector2(55f, 35f),
            width: 110f,
            height: 30f));
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
