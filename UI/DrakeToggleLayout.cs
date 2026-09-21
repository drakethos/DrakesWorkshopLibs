using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;
using Text = UnityEngine.UI.Text;

namespace DrakeModsLibs.UI;

/// <summary>
/// Shared layout for Jotunn <see cref="GUIManager.CreateToggle"/> rows:
/// label flush left, hex checkbox flush right — same column on every row.
/// </summary>
public static class DrakeToggleLayout
{
    const float DefaultBox = 24f;
    const float LabelToBoxGap = 12f;

    /// <summary>
    /// Size the toggle root to the row and pin the checkbox to the right edge so
    /// longer labels never shove the box sideways.
    /// </summary>
    public static void ApplyLabelLeftBoxRight(
        GameObject jotunnToggleGo,
        string labelText,
        float rowWidth = 260f,
        float rowHeight = 28f,
        float boxSize = DefaultBox)
    {
        if (jotunnToggleGo == null)
            return;

        var toggleRt = jotunnToggleGo.GetComponent<RectTransform>();
        if (toggleRt != null)
        {
            toggleRt.anchorMin = toggleRt.anchorMax = new Vector2(0.5f, 0.5f);
            toggleRt.pivot = new Vector2(0.5f, 0.5f);
            toggleRt.anchoredPosition = Vector2.zero;
            toggleRt.sizeDelta = new Vector2(rowWidth > 0f ? rowWidth : 260f, rowHeight);
        }

        if (jotunnToggleGo.transform.Find("Background") is RectTransform bg)
        {
            bg.anchorMin = bg.anchorMax = new Vector2(1f, 0.5f);
            bg.pivot = new Vector2(1f, 0.5f);
            bg.anchoredPosition = new Vector2(-4f, 0f);
            bg.sizeDelta = new Vector2(boxSize, boxSize);

            if (bg.Find("Checkmark") is RectTransform mark)
            {
                mark.anchorMin = mark.anchorMax = new Vector2(0.5f, 0.5f);
                mark.pivot = new Vector2(0.5f, 0.5f);
                mark.anchoredPosition = Vector2.zero;
                mark.sizeDelta = new Vector2(boxSize * 0.67f, boxSize * 0.67f);
            }
        }

        var label = jotunnToggleGo.GetComponentInChildren<Text>(true);
        if (label == null)
            return;

        if (GUIManager.Instance != null)
        {
            label.font = GUIManager.Instance.AveriaSerifBold;
            label.fontSize = 16;
        }

        label.text = labelText ?? "";
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Truncate;
        label.raycastTarget = false;

        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.pivot = new Vector2(0f, 0.5f);
        lrt.offsetMin = new Vector2(8f, 0f);
        lrt.offsetMax = new Vector2(-(boxSize + LabelToBoxGap), 0f);
    }

    /// <summary>Create a fixed-width row parent for a toggle (centers in a wood panel).</summary>
    public static GameObject CreateRow(Transform parent, string name, Vector2 anchoredPosition, float width = 260f, float height = 28f)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var rt = row.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(width, height);
        return row;
    }
}
