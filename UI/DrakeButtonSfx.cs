using UnityEngine;
using UnityEngine.UI;

namespace DrakeModsLibs.UI;

/// <summary>
/// Jotunn ApplyButtonStyle adds ButtonSfx with click + select; a mouse click fires both.
/// Keep click only — same soften used by RenameIt.
/// </summary>
public static class DrakeButtonSfx
{
    public static void Soften(GameObject? root)
    {
        if (!root)
            return;

        foreach (var sfx in root.GetComponentsInChildren<ButtonSfx>(true))
        {
            if (!sfx)
                continue;
            sfx.m_selectSfxPrefab = null;
            sfx.m_selectSfxPrefabVibrationOnly = null;
            sfx.m_enterSfxPrefab = null;
            sfx.m_enterSfxPrefabVibrationOnly = null;
        }
    }

    public static Button SoftenButton(GameObject buttonGo)
    {
        Soften(buttonGo);
        return buttonGo.GetComponent<Button>();
    }
}
