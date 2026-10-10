using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>A whole-number stepper: minus, the value, plus. Holds its own range; at either end the matching button greys out.</summary>
public sealed class UkStepper
{
    readonly Label _value;
    readonly Button _minus;
    readonly Button _plus;
    int _min;
    int _max;
    int _current;
    bool _enabled = true;

    public VisualElement Root { get; }
    public int Value => _current;

    /// <summary>Raised when the player changes the value (not by <see cref="SetValueWithoutNotify"/>).</summary>
    public Action<int>? Changed;

    public UkStepper(int min, int max, int value)
    {
        _min = min;
        _max = Math.Max(min, max);
        _current = Mathf.Clamp(value, _min, _max);

        Root = new VisualElement().Row();
        _minus = UkControls.MakeButton("−", () => Step(-1), UkButtonKind.Secondary, UkTheme.Target);
        _minus.style.paddingLeft = _minus.style.paddingRight = 0;
        _minus.style.fontSize = 20;

        var well = new VisualElement().Size(width: 64, height: UkTheme.Target).Fill(UkTheme.Field).Round(UkTheme.Radius).Border(UkTheme.Line);
        well.style.alignItems = Align.Center;
        well.style.justifyContent = Justify.Center;
        well.style.marginLeft = well.style.marginRight = 6;
        _value = UkControls.Text("", 18, UkTheme.Text, heading: true, wrap: false);
        _value.style.unityTextAlign = TextAnchor.MiddleCenter;
        well.Add(_value);

        _plus = UkControls.MakeButton("+", () => Step(+1), UkButtonKind.Secondary, UkTheme.Target);
        _plus.style.paddingLeft = _plus.style.paddingRight = 0;
        _plus.style.fontSize = 20;

        Root.Add(_minus);
        Root.Add(well);
        Root.Add(_plus);
        Paint();
    }

    public void SetValueWithoutNotify(int value)
    {
        _current = Mathf.Clamp(value, _min, _max);
        Paint();
    }

    /// <summary>Changes the range; the value is pulled into it (raising <see cref="Changed"/> if that moved it and asked to).</summary>
    public void SetRange(int min, int max, bool notifyIfClamped = false)
    {
        _min = min;
        _max = Math.Max(min, max);
        var clamped = Mathf.Clamp(_current, _min, _max);
        var moved = clamped != _current;
        _current = clamped;
        Paint();
        if (moved && notifyIfClamped)
            Changed?.Invoke(_current);
    }

    /// <summary>Greyed out as a whole (e.g. a locked style).</summary>
    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        Paint();
    }

    void Step(int delta)
    {
        if (!_enabled)
            return;
        var next = Mathf.Clamp(_current + delta, _min, _max);
        if (next == _current)
            return;
        _current = next;
        Paint();
        Changed?.Invoke(_current);
    }

    void Paint()
    {
        _value.text = _current.ToString();
        UkControls.SetButtonEnabled(_minus, _enabled && _current > _min);
        UkControls.SetButtonEnabled(_plus, _enabled && _current < _max);
        Root.style.opacity = _enabled ? 1f : 0.6f;
    }
}
