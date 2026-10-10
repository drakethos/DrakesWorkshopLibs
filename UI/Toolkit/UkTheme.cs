using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>
/// The DrakeMods look: dark stained wood, brass accent, warm parchment text. Colours and spacing live here only,
/// so a restyle (or a Libs move) is one file. Built in code: runtime UI Toolkit can't load USS we author at runtime.
/// </summary>
public static class UkTheme
{
    public static readonly Color Panel = Hex("1a1612");
    public static readonly Color PanelDeep = Hex("15120e");
    public static readonly Color Raised = Hex("221d17");
    public static readonly Color Active = Hex("2a231b");
    public static readonly Color Field = Hex("0f0d0a");
    public static readonly Color Line = Hex("3a3127");
    public static readonly Color LineSoft = Hex("2c251d");
    public static readonly Color Text = Hex("efe6d2");
    public static readonly Color TextBody = Hex("d9cdb3");
    public static readonly Color TextMuted = Hex("b9ab91");
    public static readonly Color Accent = Hex("e0ad4f");
    public static readonly Color AccentHover = Hex("f0c06a");
    public static readonly Color AccentText = Hex("1a1208");
    public static readonly Color Gold = Hex("f3c873");
    public static readonly Color Danger = Hex("c9583a");
    public static readonly Color DangerText = Hex("f0a184");
    public static readonly Color DangerBg = Hex("2a1712");
    public static readonly Color DangerLine = Hex("6b3a2a");
    public static readonly Color Ok = Hex("9fbf7a");
    public static readonly Color Scrim = new(0f, 0f, 0f, 0.28f);

    public const int RadiusSmall = 8;
    public const int Radius = 10;
    public const int RadiusLarge = 16;
    /// <summary>Minimum click target height (same as the mockup's 44 px).</summary>
    public const int Target = 44;

    public static Color Hex(string rgb) =>
        ColorUtility.TryParseHtmlString("#" + rgb, out var color) ? color : Color.magenta;
}

/// <summary>Chainable inline-style shorthands (UI Toolkit styles are per-side and verbose).</summary>
public static class UkStyle
{
    public static T Pad<T>(this T e, float vertical, float horizontal) where T : VisualElement
    {
        e.style.paddingTop = e.style.paddingBottom = vertical;
        e.style.paddingLeft = e.style.paddingRight = horizontal;
        return e;
    }

    public static T Pad<T>(this T e, float all) where T : VisualElement => e.Pad(all, all);

    public static T Margin<T>(this T e, float top = 0, float right = 0, float bottom = 0, float left = 0) where T : VisualElement
    {
        e.style.marginTop = top;
        e.style.marginRight = right;
        e.style.marginBottom = bottom;
        e.style.marginLeft = left;
        return e;
    }

    public static T Border<T>(this T e, Color color, float width = 1f) where T : VisualElement
    {
        e.style.borderTopColor = e.style.borderBottomColor = e.style.borderLeftColor = e.style.borderRightColor = color;
        e.style.borderTopWidth = e.style.borderBottomWidth = e.style.borderLeftWidth = e.style.borderRightWidth = width;
        return e;
    }

    public static T BorderBottom<T>(this T e, Color color, float width = 1f) where T : VisualElement
    {
        e.style.borderBottomColor = color;
        e.style.borderBottomWidth = width;
        return e;
    }

    public static T BorderTop<T>(this T e, Color color, float width = 1f) where T : VisualElement
    {
        e.style.borderTopColor = color;
        e.style.borderTopWidth = width;
        return e;
    }

    public static T Round<T>(this T e, float radius) where T : VisualElement
    {
        e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
            e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        return e;
    }

    public static T Row<T>(this T e, Align align = Align.Center) where T : VisualElement
    {
        e.style.flexDirection = FlexDirection.Row;
        e.style.alignItems = align;
        return e;
    }

    public static T Column<T>(this T e) where T : VisualElement
    {
        e.style.flexDirection = FlexDirection.Column;
        return e;
    }

    public static T Size<T>(this T e, float? width = null, float? height = null) where T : VisualElement
    {
        if (width.HasValue)
            e.style.width = width.Value;
        if (height.HasValue)
            e.style.height = height.Value;
        return e;
    }

    public static T Grow<T>(this T e, float grow = 1f) where T : VisualElement
    {
        e.style.flexGrow = grow;
        e.style.flexShrink = 1f;
        e.style.minWidth = 0;
        return e;
    }

    public static T Fixed<T>(this T e) where T : VisualElement
    {
        e.style.flexShrink = 0f;
        e.style.flexGrow = 0f;
        return e;
    }

    public static T Fill<T>(this T e, Color color) where T : VisualElement
    {
        e.style.backgroundColor = color;
        return e;
    }

    /// <summary>Adds children with a fixed gap between them (flex <c>gap</c> isn't in UI Toolkit).</summary>
    public static T AddGapped<T>(this T parent, float gap, params VisualElement[] children) where T : VisualElement
    {
        // Call Row()/Column() on the parent first; the gap goes on the leading edge of every child after the first.
        var row = parent.style.flexDirection == FlexDirection.Row;
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0)
            {
                if (row)
                    children[i].style.marginLeft = gap;
                else
                    children[i].style.marginTop = gap;
            }

            parent.Add(children[i]);
        }

        return parent;
    }

    public static void Show(this VisualElement e, bool visible) =>
        e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
}
