using System;
using DrakeModsLibs.UI.Toolkit;
using UnityEngine;

namespace DrakeModsLibs.UI;

/// <summary>Colour of a <see cref="DrakeMessage"/>'s edge. The card itself looks the same for every kind.</summary>
public enum DrakeMessageKind
{
    Info,
    Success,
    Warning,
    Error,
}

/// <summary>
/// The shared on-screen message: a small wood card near the top of the screen that fades out on its own. It sits above every
/// Drake window and dialog and never takes clicks, so it can't hide behind a menu or block one. Use it instead of Valheim's yellow
/// centre text. Showing the same message again while it is still up restarts its timer instead of stacking a copy.
/// With the new look turned off (<c>UseToolkitUi</c>), it falls back to Valheim's centre message.
/// </summary>
public static class DrakeMessage
{
    /// <summary>Show a card. Call from Unity's main thread; does nothing on a dedicated server.</summary>
    /// <param name="text">The message body.</param>
    /// <param name="kind">Edge colour: info (brass), success (green), warning (gold) or error (red).</param>
    /// <param name="title">Optional bold line above the text.</param>
    /// <param name="seconds">How long it stays before fading out.</param>
    public static void Show(string text, DrakeMessageKind kind = DrakeMessageKind.Info, string? title = null, float seconds = 3.5f)
    {
        if (string.IsNullOrEmpty(text))
            return;
        if (ZNet.instance != null && ZNet.instance.IsDedicated())
            return;

        if (!DrakeUiMode.Toolkit)
        {
            Fallback(text);
            return;
        }

        try
        {
            UkMessageView.Instance.Show(text, title, ToTone(kind), Mathf.Max(0.5f, seconds));
        }
        catch (Exception ex)
        {
            DrakeUiMode.ReportFailure(ex);
            Fallback(text);
        }
    }

    /// <summary>Removes every card right away (for example when leaving a world).</summary>
    public static void Clear()
    {
        try
        {
            UkMessageView.Instance.Clear();
        }
        catch (Exception)
        {
            // Nothing was ever shown, so there is nothing to clear.
        }
    }

    /// <summary>Valheim's own centre message, for when the new look is off. Used directly, not via <c>CustomizeLibsAPI</c>, which calls us.</summary>
    static void Fallback(string text) =>
        DrakeModsLibs.Patches.CharacterMessageInvoker.Show(Player.m_localPlayer, MessageHud.MessageType.Center, text, 0, null);

    static UkMessageTone ToTone(DrakeMessageKind kind) => kind switch
    {
        DrakeMessageKind.Success => UkMessageTone.Success,
        DrakeMessageKind.Warning => UkMessageTone.Warning,
        DrakeMessageKind.Error => UkMessageTone.Error,
        _ => UkMessageTone.Info,
    };
}
