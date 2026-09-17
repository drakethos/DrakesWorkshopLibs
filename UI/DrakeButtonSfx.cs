using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace DrakeModsLibs.UI;

/// <summary>
/// Jotunn ApplyButtonStyle adds ButtonSfx with click + select; a mouse click fires both.
/// Keep click only — same soften used by RenameIt.
/// Prefab fields vary by Valheim / stub assembly; clear via reflection so CI stubs compile.
/// </summary>
public static class DrakeButtonSfx
{
    private static readonly string[] OptionalClearFieldNames =
    {
        "m_selectSfxPrefab",
        "m_selectSfxPrefabVibrationOnly",
        "m_enterSfxPrefab",
        "m_enterSfxPrefabVibrationOnly",
    };

    private static readonly FieldInfo[] SoftenFields = ResolveSoftenFields();

    private static FieldInfo[] ResolveSoftenFields()
    {
        var list = new List<FieldInfo>();
        var type = typeof(ButtonSfx);
        foreach (var name in OptionalClearFieldNames)
        {
            var field = AccessTools.Field(type, name);
            if (field != null)
                list.Add(field);
        }

        return list.ToArray();
    }

    public static void Soften(GameObject? root)
    {
        if (!root)
            return;

        foreach (var sfx in root.GetComponentsInChildren<ButtonSfx>(true))
        {
            if (!sfx)
                continue;

            foreach (var field in SoftenFields)
                field.SetValue(sfx, null);
        }
    }

    public static Button SoftenButton(GameObject buttonGo)
    {
        Soften(buttonGo);
        return buttonGo.GetComponent<Button>();
    }
}
