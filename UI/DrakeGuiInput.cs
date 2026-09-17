using Jotunn.Managers;

namespace DrakeModsLibs.UI;

/// <summary>
/// Shared GUIManager.BlockInput bookkeeping (copied from RenameIt UIPanels patterns).
/// Prevents double-block / double-unblock when stacking Drake wood panels.
/// </summary>
public static class DrakeGuiInput
{
    private static bool _inputBlocked;

    public static bool IsBlocked => _inputBlocked;

    public static void EnsureBlocked()
    {
        if (_inputBlocked)
            return;
        GUIManager.BlockInput(true);
        _inputBlocked = true;
    }

    public static void EnsureUnblocked()
    {
        if (!_inputBlocked)
            return;
        GUIManager.BlockInput(false);
        _inputBlocked = false;
    }
}
