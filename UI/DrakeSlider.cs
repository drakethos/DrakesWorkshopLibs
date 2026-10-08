using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeModsLibs.UI;

/// <summary>
/// Valheim-style labeled float slider: <c>Label [=====o----] 2.50</c>.
/// Jotunn has no slider factory; laid out by a <see cref="HorizontalLayoutGroup"/>, so callers only pick the width.
/// </summary>
public sealed class DrakeSlider
{
    static readonly Color TrackColor = new Color(0.11f, 0.09f, 0.07f, 0.95f);
    static readonly Color HandleColor = new Color(0.96f, 0.90f, 0.78f, 1f);

    public GameObject Root { get; }
    public Slider Slider { get; }
    public Text? Label { get; }
    public Text? ValueText { get; }

    readonly Func<float, string> _format;
    Action<float>? _onChanged;

    DrakeSlider(GameObject root, Slider slider, Text? label, Text? valueText, Func<float, string> format, Action<float>? onChanged)
    {
        Root = root;
        Slider = slider;
        Label = label;
        ValueText = valueText;
        _format = format;
        _onChanged = onChanged;
        Slider.onValueChanged.AddListener(v =>
        {
            SyncText();
            _onChanged?.Invoke(v);
        });
        SyncText();
    }

    public float Value
    {
        get => Slider.value;
        set => Slider.value = value;
    }

    public void SetValueWithoutNotify(float value)
    {
        Slider.SetValueWithoutNotify(value);
        SyncText();
    }

    public void SetOnChanged(Action<float>? onChanged) => _onChanged = onChanged;

    public void SetInteractable(bool interactable) => Slider.interactable = interactable;

    /// <summary>
    /// Build a slider row centered at <paramref name="anchoredPosition"/> under <paramref name="parent"/>.
    /// <paramref name="format"/> renders the value label (default <c>0.00</c>). Returns null if GUIManager is unavailable.
    /// </summary>
    public static DrakeSlider? Create(
        Transform parent,
        Vector2 anchoredPosition,
        string label,
        float min,
        float max,
        float initial,
        Action<float>? onChanged = null,
        Func<float, string>? format = null,
        float width = 300f,
        float height = 24f,
        float labelWidth = 80f,
        float valueWidth = 48f,
        int fontSize = 13,
        bool wholeNumbers = false)
    {
        if (parent == null || GUIManager.Instance == null)
            return null;
        if (max < min)
            (min, max) = (max, min);

        var root = new GameObject("drake_slider", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(parent, false);
        var rootRt = (RectTransform)root.transform;
        rootRt.anchorMin = rootRt.anchorMax = rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = anchoredPosition;
        rootRt.sizeDelta = new Vector2(width, height);
        var layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        var labelText = MakeText(root.transform, label, labelWidth, fontSize, TextAnchor.MiddleRight, flexible: false);
        var slider = BuildSlider(root.transform, height);
        var valueText = MakeText(root.transform, "", valueWidth, fontSize, TextAnchor.MiddleLeft, flexible: false);

        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.SetValueWithoutNotify(Mathf.Clamp(initial, min, max));
        return new DrakeSlider(root, slider, labelText, valueText, format ?? (v => v.ToString("0.00")), onChanged);
    }

    void SyncText()
    {
        if (ValueText != null)
            ValueText.text = _format(Slider.value);
    }

    static Slider BuildSlider(Transform parent, float height)
    {
        var go = new GameObject("slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var element = go.GetComponent<LayoutElement>();
        element.flexibleWidth = 1f;
        element.minWidth = 60f;
        element.preferredHeight = height;

        var track = Child(go.transform, "track", TrackColor);
        Stretch(track, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));

        var fillArea = new GameObject("fill_area", typeof(RectTransform));
        fillArea.transform.SetParent(go.transform, false);
        Stretch((RectTransform)fillArea.transform, new Vector2(0f, 0.35f), new Vector2(1f, 0.65f));
        var fill = Child(fillArea.transform, "fill", GUIManager.Instance.ValheimOrange);
        Stretch(fill, Vector2.zero, new Vector2(0f, 1f));

        var handleArea = new GameObject("handle_area", typeof(RectTransform));
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRt = (RectTransform)handleArea.transform;
        Stretch(handleAreaRt, Vector2.zero, Vector2.one);
        handleAreaRt.offsetMin = new Vector2(6f, 0f);
        handleAreaRt.offsetMax = new Vector2(-6f, 0f);
        var handle = Child(handleArea.transform, "handle", HandleColor);
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(12f, 0f);

        var slider = go.GetComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        return slider;
    }

    static RectTransform Child(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = color;
        return (RectTransform)go.transform;
    }

    static void Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static Text MakeText(Transform parent, string text, float width, int fontSize, TextAnchor anchor, bool flexible)
    {
        var go = new GameObject("text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = GUIManager.Instance.AveriaSerifBold;
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.color = new Color(0.72f, 0.65f, 0.50f);
        t.text = text;
        t.raycastTarget = false;
        var element = go.GetComponent<LayoutElement>();
        element.preferredWidth = width;
        element.flexibleWidth = flexible ? 1f : 0f;
        return t;
    }
}
