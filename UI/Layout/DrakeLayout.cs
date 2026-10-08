using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// Layout helpers under the wood panels. Panels measure their content and scroll past their
/// max height, so a text or button-count change doesn't need a Libs release to fit.
/// </summary>
internal static class DrakeLayout
{
    /// <summary>Height the text needs at its current rect width, wrapping included.</summary>
    public static float MeasureHeight(Text text) => Mathf.Ceil(text.preferredHeight);

    /// <summary>
    /// Top-centered scroll area at <paramref name="anchoredPosition"/> inside <paramref name="parent"/>.
    /// Put children under <paramref name="content"/> and set its height with <see cref="SetContentHeight"/>.
    /// </summary>
    public static ScrollRect CreateScrollArea(
        Transform parent,
        string name,
        float width,
        float height,
        Vector2 anchoredPosition,
        out RectTransform content)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        root.transform.SetParent(parent, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(width, height);

        // Transparent. It only catches wheel input while the content overflows (see SetContentHeight).
        var image = root.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        var viewport = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(root.transform, false);
        var viewportRt = (RectTransform)viewport.transform;
        viewportRt.anchorMin = Vector2.zero;
        viewportRt.anchorMax = Vector2.one;
        viewportRt.offsetMin = Vector2.zero;
        viewportRt.offsetMax = Vector2.zero;

        var contentGo = new GameObject("content", typeof(RectTransform));
        contentGo.transform.SetParent(viewport.transform, false);
        content = (RectTransform)contentGo.transform;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, height);

        var scroll = root.GetComponent<ScrollRect>();
        scroll.viewport = viewportRt;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        return scroll;
    }

    /// <summary>
    /// Sets the content height and enables scrolling only when the content doesn't fit the viewport.
    /// </summary>
    public static void SetContentHeight(ScrollRect scroll, float contentHeight, float viewportHeight)
    {
        var content = scroll.content;
        content.sizeDelta = new Vector2(content.sizeDelta.x, contentHeight);

        var overflow = contentHeight > viewportHeight + 0.5f;
        scroll.vertical = overflow;
        scroll.GetComponent<Image>().raycastTarget = overflow;
        scroll.verticalNormalizedPosition = 1f;
    }
}
