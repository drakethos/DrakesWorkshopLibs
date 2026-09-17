using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>Simple wood panel with one input field (Relabel) — RenameIt name-editor chrome, stripped down.</summary>
public sealed class DrakeTextPromptPanel
{
    const float Width = 360f;
    const float Height = 200f;

    readonly string _panelName;
    GameObject? _panel;
    Text? _titleText;
    InputField? _input;
    Button? _okButton;
    Button? _cancelButton;
    Action<string>? _onOk;

    public DrakeTextPromptPanel(string panelName = "drake_text_prompt")
    {
        _panelName = string.IsNullOrEmpty(panelName) ? "drake_text_prompt" : panelName;
    }

    public bool IsOpen => _panel && _panel.activeSelf;

    public void Show(
        string title,
        string initialText,
        Action<string> onOk,
        Action? onCancel = null,
        int charLimit = 64,
        string okLabel = "Ok",
        string cancelLabel = "Cancel")
    {
        if (GUIManager.Instance == null || !GUIManager.CustomGUIFront)
            return;

        Ensure();
        if (!_panel || _titleText == null || !_input || !_okButton || !_cancelButton)
            return;

        _onOk = onOk;
        _titleText.text = title ?? "";
        _input.characterLimit = Math.Max(1, charLimit);
        _input.text = initialText ?? "";

        SetButtonLabel(_okButton, okLabel);
        SetButtonLabel(_cancelButton, cancelLabel);

        _cancelButton.onClick.RemoveAllListeners();
        _cancelButton.onClick.AddListener(() =>
        {
            Close();
            onCancel?.Invoke();
        });

        _okButton.onClick.RemoveAllListeners();
        _okButton.onClick.AddListener(() =>
        {
            var value = _input != null ? _input.text.Trim() : "";
            var ok = _onOk;
            Close();
            ok?.Invoke(value);
        });

        _panel.SetActive(true);
        _panel.transform.SetAsLastSibling();
        DrakeGuiInput.EnsureBlocked();
        _input.ActivateInputField();
    }

    public void Close()
    {
        if (_panel)
            _panel.SetActive(false);
        DrakeGuiInput.EnsureUnblocked();
        _onOk = null;
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
            width: 300f,
            height: 36f,
            addContentSizeFitter: false).GetComponent<Text>();
        _titleText.alignment = TextAnchor.MiddleCenter;

        _input = GUIManager.Instance.CreateInputField(
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(0f, 8f),
            contentType: InputField.ContentType.Standard,
            placeholderText: "",
            fontSize: 18,
            width: 300f,
            height: 34f).GetComponent<InputField>();

        _cancelButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Cancel",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(-70f, 36f),
            width: 110f,
            height: 30f));

        _okButton = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "Ok",
            parent: _panel.transform,
            anchorMin: new Vector2(0.5f, 0f),
            anchorMax: new Vector2(0.5f, 0f),
            position: new Vector2(70f, 36f),
            width: 110f,
            height: 30f));

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
