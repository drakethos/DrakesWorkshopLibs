using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>
/// A colour picker that looks like a picker: a saturation/brightness square, a hue strip, a swatch and a hex box. Any colour, not just
/// a fixed palette. Drag to choose; <see cref="Picked"/> fires when the pointer is released or a hex code is entered.
/// The two gradients are small generated textures (redrawn when the hue moves), so there is nothing to load.
/// </summary>
public sealed class UkColorPicker
{
    const float PickerWidth = 280f;
    const float SquareHeight = 168f;
    const float StripHeight = 18f;
    const int Cells = 64;

    readonly Texture2D _squareTexture;
    readonly Texture2D _stripTexture;
    readonly VisualElement _square;
    readonly VisualElement _squareMarker;
    readonly VisualElement _strip;
    readonly VisualElement _stripMarker;
    readonly VisualElement _swatch;
    readonly TextField _hex;
    float _hue;
    float _saturation = 1f;
    float _value = 1f;

    public VisualElement Root { get; }

    /// <summary>The chosen colour as <c>#rrggbb</c>.</summary>
    public Action<string>? Picked;

    public UkColorPicker()
    {
        _squareTexture = new Texture2D(Cells, Cells, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "drakes_colour_square",
        };
        _stripTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "drakes_colour_hue",
        };
        var hues = new Color32[256];
        for (var x = 0; x < 256; x++)
            hues[x] = Color.HSVToRGB(x / 255f, 1f, 1f);
        _stripTexture.SetPixels32(hues);
        _stripTexture.Apply();

        Root = new VisualElement().Column();
        Root.style.width = PickerWidth;

        _square = new VisualElement().Size(PickerWidth, SquareHeight).Round(UkTheme.RadiusSmall).Border(UkTheme.Line);
        _square.style.backgroundImage = new StyleBackground(_squareTexture);
        _square.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        _squareMarker = Marker(14, 14, round: 7);
        _square.Add(_squareMarker);
        Root.Add(_square);
        Drag(_square, OnSquare);

        _strip = new VisualElement().Size(PickerWidth, StripHeight).Round(StripHeight / 2f).Border(UkTheme.Line);
        _strip.style.marginTop = 12;
        _strip.style.backgroundImage = new StyleBackground(_stripTexture);
        _strip.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
        _stripMarker = Marker(8, StripHeight + 6, round: 4);
        _stripMarker.style.top = -4;
        _strip.Add(_stripMarker);
        Root.Add(_strip);
        Drag(_strip, OnStrip);

        var row = new VisualElement().Row();
        row.style.marginTop = 12;
        _swatch = new VisualElement { pickingMode = PickingMode.Ignore }.Size(40, 40).Fixed().Round(UkTheme.RadiusSmall).Border(UkTheme.Line);
        row.Add(_swatch);

        _hex = new TextField { isDelayed = true, maxLength = 7 };
        _hex.style.flexGrow = 1;
        _hex.style.marginLeft = 10;
        _hex.style.marginTop = _hex.style.marginBottom = _hex.style.marginRight = 0;
        var box = _hex.Q<VisualElement>("unity-text-input") ?? _hex;
        box.Fill(UkTheme.Field).Round(UkTheme.RadiusSmall).Border(UkTheme.Line).Pad(0, 12);
        box.style.height = 40;
        box.style.fontSize = 16;
        box.style.color = UkTheme.Text;
        box.style.unityTextAlign = TextAnchor.MiddleLeft;
        UkFonts.ApplyBody(box);
        _hex.textSelection.cursorColor = UkTheme.Gold;
        _hex.textSelection.selectionColor = new Color(0.88f, 0.68f, 0.31f, 0.38f);
        _hex.RegisterValueChangedCallback(e =>
        {
            if (!TryParse(e.newValue, out var color))
                return;
            Color.RGBToHSV(color, out _hue, out _saturation, out _value);
            Refresh(updateHex: false);
            Commit();
        });
        row.Add(_hex);
        Root.Add(row);

        Refresh(updateHex: true);
    }

    /// <summary>Show <paramref name="hex"/> as the current colour (a no-op if it isn't a colour).</summary>
    public void SetColor(string? hex)
    {
        if (!TryParse(hex, out var color))
            return;
        Color.RGBToHSV(color, out _hue, out _saturation, out _value);
        Refresh(updateHex: true);
    }

    public static bool TryParse(string? text, out Color color)
    {
        color = Color.white;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var t = text!.Trim();
        return ColorUtility.TryParseHtmlString(t.StartsWith("#", StringComparison.Ordinal) ? t : "#" + t, out color);
    }

    public static string ToHex(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color).ToLowerInvariant();

    string CurrentHex() => ToHex(Color.HSVToRGB(_hue, _saturation, _value));

    void OnSquare(Vector2 position)
    {
        _saturation = Mathf.Clamp01(position.x / PickerWidth);
        _value = 1f - Mathf.Clamp01(position.y / SquareHeight);
        Refresh(updateHex: true);
    }

    void OnStrip(Vector2 position)
    {
        _hue = Mathf.Clamp01(position.x / PickerWidth);
        Refresh(updateHex: true);
    }

    void Commit() => Picked?.Invoke(CurrentHex());

    /// <summary>Repaints the square (it depends on the hue), the markers, the swatch and the hex box.</summary>
    void Refresh(bool updateHex)
    {
        var pixels = new Color32[Cells * Cells];
        for (var y = 0; y < Cells; y++)
        {
            var v = y / (Cells - 1f);
            for (var x = 0; x < Cells; x++)
                pixels[y * Cells + x] = Color.HSVToRGB(_hue, x / (Cells - 1f), v);
        }

        _squareTexture.SetPixels32(pixels);
        _squareTexture.Apply();

        _squareMarker.style.left = _saturation * PickerWidth - 7f;
        _squareMarker.style.top = (1f - _value) * SquareHeight - 7f;
        _stripMarker.style.left = _hue * PickerWidth - 4f;
        var color = Color.HSVToRGB(_hue, _saturation, _value);
        _swatch.style.backgroundColor = color;
        if (updateHex)
            _hex.SetValueWithoutNotify(CurrentHex());
    }

    static VisualElement Marker(float width, float height, float round)
    {
        var marker = new VisualElement { pickingMode = PickingMode.Ignore }.Size(width, height).Round(round).Border(Color.white, 2);
        marker.style.position = Position.Absolute;
        marker.style.backgroundColor = new Color(0f, 0f, 0f, 0.25f);
        return marker;
    }

    /// <summary>Press and drag on <paramref name="element"/>; the position is in its own coordinates. Commits when released.</summary>
    void Drag(VisualElement element, Action<Vector2> onPosition)
    {
        element.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button != 0)
                return;
            element.CapturePointer(e.pointerId);
            onPosition(new Vector2(e.localPosition.x, e.localPosition.y));
            e.StopPropagation();
        });
        element.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (element.HasPointerCapture(e.pointerId))
                onPosition(new Vector2(e.localPosition.x, e.localPosition.y));
        });
        element.RegisterCallback<PointerUpEvent>(e =>
        {
            if (!element.HasPointerCapture(e.pointerId))
                return;
            element.ReleasePointer(e.pointerId);
            Commit();
        });
    }
}
