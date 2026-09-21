using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeModsLibs.UI;

/// <summary>
/// Valheim-style integer spin box: <c>−</c> / field / <c>+</c>.
/// Jotunn has no NumericUpDown — this is the shared Drake stand-in.
/// </summary>
public sealed class DrakeNumericStepper
{
    public GameObject Root { get; }
    public InputField? Field { get; }
    public Button? MinusButton { get; }
    public Button? PlusButton { get; }

    public int Min { get; }
    public int Max { get; }

    int _value;
    bool _suppress;
    Action<int>? _onChanged;

    DrakeNumericStepper(
        GameObject root,
        InputField? field,
        Button? minus,
        Button? plus,
        int min,
        int max,
        int initial,
        Action<int>? onChanged)
    {
        Root = root;
        Field = field;
        MinusButton = minus;
        PlusButton = plus;
        Min = min;
        Max = max;
        _onChanged = onChanged;
        _value = Mathf.Clamp(initial, min, max);

        if (MinusButton != null)
            MinusButton.onClick.AddListener(() => Step(-1));
        if (PlusButton != null)
            PlusButton.onClick.AddListener(() => Step(1));
        if (Field != null)
        {
            Field.characterLimit = Math.Max(1, max.ToString().Length);
            Field.onEndEdit.AddListener(OnEndEdit);
            if (Field.textComponent != null)
                Field.textComponent.alignment = TextAnchor.MiddleCenter;
        }

        SyncField();
    }

    public int Value
    {
        get => _value;
        set => SetValue(value, notify: true);
    }

    public void SetValueWithoutNotify(int value) => SetValue(value, notify: false);

    public void SetOnChanged(Action<int>? onChanged) => _onChanged = onChanged;

    /// <summary>
    /// Build a centered spin row under <paramref name="parent"/>.
    /// Returns null if GUIManager / CustomGUI is unavailable.
    /// </summary>
    public static DrakeNumericStepper? Create(
        Transform parent,
        Vector2 anchoredPosition,
        int min,
        int max,
        int initial,
        Action<int>? onChanged = null,
        float buttonSize = 40f,
        float fieldWidth = 64f,
        float height = 32f,
        float gap = 12f)
    {
        if (parent == null || GUIManager.Instance == null)
            return null;
        if (max < min)
            (min, max) = (max, min);

        var root = new GameObject("drake_numeric_stepper", typeof(RectTransform));
        root.transform.SetParent(parent, false);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = anchoredPosition;
        var totalW = buttonSize * 2f + fieldWidth + gap * 2f;
        rootRt.sizeDelta = new Vector2(totalW, height);

        var fieldX = 0f;
        var minusX = -(fieldWidth * 0.5f + gap + buttonSize * 0.5f);
        var plusX = fieldWidth * 0.5f + gap + buttonSize * 0.5f;

        var minus = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "−",
            parent: root.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(minusX, 0f),
            width: buttonSize,
            height: height));

        var fieldGo = GUIManager.Instance.CreateInputField(
            parent: root.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(fieldX, 0f),
            contentType: InputField.ContentType.IntegerNumber,
            placeholderText: initial.ToString(),
            fontSize: 18,
            width: fieldWidth,
            height: height);

        var plus = DrakeButtonSfx.SoftenButton(GUIManager.Instance.CreateButton(
            text: "+",
            parent: root.transform,
            anchorMin: new Vector2(0.5f, 0.5f),
            anchorMax: new Vector2(0.5f, 0.5f),
            position: new Vector2(plusX, 0f),
            width: buttonSize,
            height: height));

        var field = fieldGo != null ? fieldGo.GetComponent<InputField>() : null;
        return new DrakeNumericStepper(root, field, minus, plus, min, max, initial, onChanged);
    }

    void Step(int delta) => SetValue(_value + delta, notify: true);

    void OnEndEdit(string raw)
    {
        if (_suppress)
            return;
        if (!int.TryParse(raw, out var parsed))
        {
            SyncField();
            return;
        }

        SetValue(parsed, notify: true);
    }

    void SetValue(int value, bool notify)
    {
        var clamped = Mathf.Clamp(value, Min, Max);
        var changed = clamped != _value;
        _value = clamped;
        SyncField();
        if (notify && changed)
            _onChanged?.Invoke(_value);
    }

    void SyncField()
    {
        if (Field == null)
            return;
        _suppress = true;
        try
        {
            Field.text = _value.ToString();
        }
        finally
        {
            _suppress = false;
        }
    }
}
