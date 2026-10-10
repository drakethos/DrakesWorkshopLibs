using System;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>Valheim's own Averia fonts as UI Toolkit font definitions (built once, from the legacy fonts Jotunn already holds).</summary>
public static class UkFonts
{
    static FontDefinition? _body;
    static FontDefinition? _heading;
    static bool _bodyTried;
    static bool _headingTried;

    public static FontDefinition? Body
    {
        get
        {
            if (!_bodyTried)
            {
                _bodyTried = true;
                _body = From(GUIManager.Instance != null ? GUIManager.Instance.AveriaSerif : null);
            }

            return _body;
        }
    }

    public static FontDefinition? Heading
    {
        get
        {
            if (!_headingTried)
            {
                _headingTried = true;
                _heading = From(GUIManager.Instance != null ? GUIManager.Instance.AveriaSerifBold : null) ?? Body;
            }

            return _heading;
        }
    }

    static FontDefinition? From(Font? font)
    {
        if (font == null)
            return null;
        try
        {
            var asset = FontAsset.CreateFontAsset(font);
            return asset != null ? FontDefinition.FromSDFFont(asset) : FontDefinition.FromFont(font);
        }
        catch (Exception ex)
        {
            Jotunn.Logger.LogWarning($"[UITK] Font '{font.name}' could not become a UI Toolkit font: {ex.Message}");
            return null;
        }
    }

    public static void ApplyBody(VisualElement e)
    {
        if (Body is { } f)
            e.style.unityFontDefinition = new StyleFontDefinition(f);
    }

    public static void ApplyHeading(VisualElement e)
    {
        if (Heading is { } f)
            e.style.unityFontDefinition = new StyleFontDefinition(f);
    }
}

/// <summary>
/// A full-screen UI Toolkit panel (menus). Fixed 1920x1080 reference scaling so layouts look the same on any
/// resolution. <see cref="Root"/> ignores pointer events itself; only the elements you add catch them.
/// </summary>
public sealed class UkScreen : MonoBehaviour
{
    /// <summary>Highest sorting order a window or dialog gets (<see cref="RaiseAboveCanvases"/> stops here).</summary>
    public const int TopSorting = 31000;
    /// <summary>The message stack (<see cref="DrakeModsLibs.UI.DrakeMessage"/>): above every window and dialog.</summary>
    public const int MessageSorting = 32000;

    PanelSettings _settings = null!;

    public VisualElement Root { get; private set; } = null!;

    /// <summary>
    /// Draw (and take clicks) above every screen-space-overlay canvas currently alive, e.g. Valheim's HUD, inventory and
    /// crosshair. Panels and canvases share one sorting scale, so a fixed number can end up underneath.
    /// </summary>
    public void RaiseAboveCanvases(int extra = 10)
    {
        var top = 0;
        foreach (var canvas in FindObjectsOfType<Canvas>())
        {
            if (canvas != null && canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay && canvas.sortingOrder > top)
                top = canvas.sortingOrder;
        }

        _settings.sortingOrder = Mathf.Min(Mathf.Max(top + extra, 100), TopSorting);
    }

    public float SortingOrder => _settings.sortingOrder;

    public static UkScreen Create(string name, float sortingOrder = 100f)
    {
        var go = new GameObject(name);
        // Inactive while wiring so UIDocument starts with its panel settings already set.
        go.SetActive(false);
        DontDestroyOnLoad(go);

        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = name + "_panel";
        // Runtime needs a theme object; ours is empty because every element is styled inline (UkTheme).
        settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        settings.sortingOrder = sortingOrder;

        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = settings;
        var screen = go.AddComponent<UkScreen>();
        screen._settings = settings;
        go.SetActive(true);

        screen.Root = doc.rootVisualElement;
        screen.Root.pickingMode = PickingMode.Ignore;
        screen.Root.style.position = Position.Absolute;
        screen.Root.style.left = screen.Root.style.right = screen.Root.style.top = screen.Root.style.bottom = 0;
        UkFonts.ApplyBody(screen.Root);
        return screen;
    }
}

/// <summary>
/// UI Toolkit drawn into a <see cref="RenderTexture"/> instead of the screen: the foundation for paper text (a rich-text
/// page that ends up as a texture on the paper mesh, replacing TextMeshPro). Dispose when the page goes away.
/// </summary>
public sealed class UkTextureSurface : MonoBehaviour
{
    public VisualElement Root { get; private set; } = null!;
    public RenderTexture Texture { get; private set; } = null!;

    public static UkTextureSurface Create(string name, int width, int height)
    {
        var go = new GameObject(name);
        go.SetActive(false);
        DontDestroyOnLoad(go);

        var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) { name = name + "_rt", filterMode = FilterMode.Point };
        texture.Create();

        var settings = ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = name + "_panel";
        settings.themeStyleSheet = ScriptableObject.CreateInstance<ThemeStyleSheet>();
        settings.targetTexture = texture;
        settings.scaleMode = PanelScaleMode.ConstantPixelSize;
        settings.clearColor = true;
        settings.colorClearValue = new Color(0f, 0f, 0f, 0f);

        var doc = go.AddComponent<UIDocument>();
        doc.panelSettings = settings;
        var surface = go.AddComponent<UkTextureSurface>();
        go.SetActive(true);

        surface.Texture = texture;
        surface.Root = doc.rootVisualElement;
        surface.Root.style.flexGrow = 1;
        UkFonts.ApplyBody(surface.Root);
        return surface;
    }

    void OnDestroy()
    {
        if (Texture != null)
        {
            Texture.Release();
            Destroy(Texture);
        }
    }
}

/// <summary>
/// A hover tooltip for screen panels (runtime UI Toolkit doesn't draw the built-in <c>tooltip</c> text). Attach it to an element
/// that is always enabled: disabled elements get no pointer events, so wrap greyed-out controls and attach to the wrapper.
/// </summary>
public sealed class UkTooltip
{
    readonly VisualElement _root;
    readonly Label _label;

    public UkTooltip(VisualElement root)
    {
        _root = root;
        _label = new Label { pickingMode = PickingMode.Ignore };
        _label.style.position = Position.Absolute;
        _label.style.maxWidth = 320;
        _label.style.whiteSpace = WhiteSpace.Normal;
        _label.style.fontSize = 14;
        _label.style.color = UkTheme.Text;
        _label.style.unityTextAlign = TextAnchor.MiddleLeft;
        _label.Fill(UkTheme.Field).Border(UkTheme.Line).Round(UkTheme.Radius).Pad(8, 12);
        UkFonts.ApplyBody(_label);
        _label.Show(false);
        root.Add(_label);
    }

    /// <summary>Shows <paramref name="text"/>() while the pointer is over <paramref name="host"/>; nothing when it returns null or empty.</summary>
    public void Attach(VisualElement host, Func<string?> text)
    {
        host.RegisterCallback<PointerEnterEvent>(e => Show(text(), e.position));
        host.RegisterCallback<PointerMoveEvent>(e =>
        {
            if (_label.style.display == DisplayStyle.Flex)
                Place(e.position);
            else
                Show(text(), e.position);
        });
        host.RegisterCallback<PointerLeaveEvent>(_ => Hide());
        host.RegisterCallback<PointerDownEvent>(_ => Hide());
    }

    public void Hide() => _label.Show(false);

    void Show(string? text, Vector3 position)
    {
        if (string.IsNullOrEmpty(text))
        {
            Hide();
            return;
        }

        _label.text = text;
        _label.Show(true);
        _label.BringToFront();
        Place(position);
    }

    void Place(Vector3 position)
    {
        var width = _root.resolvedStyle.width;
        var x = position.x + 16f;
        if (!float.IsNaN(width) && width > 0f && x + 340f > width)
            x = width - 340f - 8f;
        _label.style.left = x;
        _label.style.top = position.y + 20f;
    }
}
