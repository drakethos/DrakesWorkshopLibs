using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>A labelled slider: title and value above a track you press or drag. (UI Toolkit's own slider has no look until it is themed.)</summary>
public sealed class UkSlider
{
    readonly Label _value;
    readonly VisualElement _track;
    readonly VisualElement _fill;
    readonly VisualElement _knob;
    readonly float _min;
    readonly float _max;
    readonly Func<float, string> _format;
    float _current;

    public VisualElement Root { get; }
    public float Value => _current;

    /// <summary>Raised as the value changes while dragging.</summary>
    public Action<float>? Changed;

    public UkSlider(string title, float min, float max, float value, Func<float, string>? format = null)
    {
        _min = min;
        _max = max;
        _format = format ?? (v => v.ToString("0.0"));
        Root = new VisualElement().Column();

        var head = new VisualElement().Row();
        head.Add(UkControls.Text(title, 15, UkTheme.Text, heading: true, wrap: false));
        head.Add(UkControls.Spacer());
        _value = UkControls.Text("", 14, UkTheme.TextMuted, wrap: false);
        head.Add(_value);
        Root.Add(head);

        // Tall hit area, thin visible track.
        var hit = new VisualElement();
        hit.style.height = 28;
        hit.style.justifyContent = Justify.Center;
        hit.style.marginTop = 4;
        _track = new VisualElement { pickingMode = PickingMode.Ignore }.Size(height: 6).Fill(UkTheme.Raised).Round(3);
        _fill = new VisualElement { pickingMode = PickingMode.Ignore }.Size(height: 6).Fill(UkTheme.Accent).Round(3);
        _track.Add(_fill);
        _knob = new VisualElement { pickingMode = PickingMode.Ignore }.Size(18, 18).Round(9).Fill(UkTheme.Text).Border(UkTheme.Accent, 2);
        _knob.style.position = Position.Absolute;
        _knob.style.top = 5;
        hit.Add(_track);
        hit.Add(_knob);
        Root.Add(hit);

        hit.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
                return;
            hit.CapturePointer(e.pointerId);
            Set(FromPosition(hit, e.localPosition.x), notify: true);
            e.StopPropagation();
        });
        hit.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (hit.HasPointerCapture(e.pointerId))
                Set(FromPosition(hit, e.localPosition.x), notify: true);
        });
        hit.RegisterCallback<PointerUpEvent>(e =>
        {
            if (hit.HasPointerCapture(e.pointerId))
                hit.ReleasePointer(e.pointerId);
        });

        SetValueWithoutNotify(value);
        // The track's width is only known after layout; place the knob again then.
        hit.RegisterCallback<GeometryChangedEvent>(_ => Place());
    }

    public void SetValueWithoutNotify(float value) => Set(value, notify: false);

    float FromPosition(VisualElement hit, float x)
    {
        var width = hit.resolvedStyle.width;
        var t = width > 0f ? Mathf.Clamp01(x / width) : 0f;
        return Mathf.Lerp(_min, _max, t);
    }

    void Set(float value, bool notify)
    {
        _current = Mathf.Clamp(value, _min, _max);
        _value.text = _format(_current);
        Place();
        if (notify)
            Changed?.Invoke(_current);
    }

    void Place()
    {
        var t = _max > _min ? (_current - _min) / (_max - _min) : 0f;
        _fill.style.width = new Length(t * 100f, LengthUnit.Percent);
        var width = _track.resolvedStyle.width;
        if (!float.IsNaN(width) && width > 0f)
            _knob.style.left = t * (width - 18f);
    }
}
