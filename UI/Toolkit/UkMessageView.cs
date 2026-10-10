using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DrakeModsLibs.UI.Toolkit;

/// <summary>
/// The on-screen stack behind <see cref="DrakeModsLibs.UI.DrakeMessage"/>: small wood cards at the top of the screen that fade
/// out on their own. It has its own full-screen panel, drawn above every window and dialog and never taking clicks.
/// </summary>
internal sealed class UkMessageView
{
    /// <summary>Above every window and dialog (<see cref="UkScreen.TopSorting"/> is the cap for those).</summary>
    const int MessageSorting = UkScreen.MessageSorting;
    const int MaxVisible = 4;
    const float FadeSeconds = 0.25f;
    /// <summary>Space above the cards, in 1080p layout pixels: clears the Valheim HUD top bar and leaves the crosshair free.</summary>
    const float TopOffset = 150f;

    static UkMessageView? _instance;

    readonly UkScreen _screen;
    readonly VisualElement _stack;
    readonly List<Entry> _entries = new();

    sealed class Entry
    {
        public string Key = "";
        public VisualElement Card = null!;
        public float HideAt;
        public float RemoveAt;
        public bool Fading;
    }

    UkMessageView()
    {
        _screen = UkScreen.Create("drake_message", MessageSorting);
        _stack = new VisualElement { pickingMode = PickingMode.Ignore };
        _stack.style.position = Position.Absolute;
        _stack.style.left = _stack.style.right = _stack.style.top = _stack.style.bottom = 0;
        _stack.style.flexDirection = FlexDirection.Column;
        _stack.style.alignItems = Align.Center;
        _stack.style.justifyContent = Justify.FlexStart;
        _stack.style.paddingTop = TopOffset;
        _screen.Root.Add(_stack);
        _stack.schedule.Execute(Tick).Every(50);
    }

    /// <summary>Creates the stack on first use (needs Valheim's fonts, so not before GUIManager is up).</summary>
    public static UkMessageView Instance => _instance ??= new UkMessageView();

    /// <summary>Shows a card, or refreshes the one already showing the same text and kind.</summary>
    public void Show(string text, string? title, UkMessageTone tone, float seconds)
    {
        var key = tone + "|" + title + "|" + text;
        var now = Time.realtimeSinceStartup;
        foreach (var existing in _entries)
        {
            if (existing.Key == key && !existing.Fading)
            {
                existing.HideAt = now + seconds;
                return;
            }
        }

        var entry = new Entry { Key = key, HideAt = now + seconds, Card = BuildCard(text, title, tone) };
        _entries.Add(entry);
        _stack.Add(entry.Card);

        // Start transparent and a little high, then glide into place on the next frame so the transition plays.
        entry.Card.schedule.Execute(() =>
        {
            entry.Card.style.opacity = 1f;
            entry.Card.style.translate = new Translate(0f, 0f);
        }).ExecuteLater(30);

        while (CountVisible() > MaxVisible)
            Fade(FirstVisible());
    }

    public void Clear()
    {
        foreach (var entry in _entries)
            entry.Card.RemoveFromHierarchy();
        _entries.Clear();
    }

    static VisualElement BuildCard(string text, string? title, UkMessageTone tone)
    {
        var accent = tone switch
        {
            UkMessageTone.Success => UkTheme.Ok,
            UkMessageTone.Warning => UkTheme.Gold,
            UkMessageTone.Error => UkTheme.Danger,
            _ => UkTheme.Accent,
        };

        var card = new VisualElement { pickingMode = PickingMode.Ignore };
        card.Row().Fill(UkTheme.Panel).Round(UkTheme.RadiusLarge).Border(UkTheme.Line).Margin(0, 0, 8, 0);
        card.style.minWidth = 260;
        card.style.maxWidth = 560;
        card.style.overflow = Overflow.Hidden;
        card.style.opacity = 0f;
        card.style.translate = new Translate(0f, -10f);
        card.style.transitionProperty = new List<StylePropertyName> { new("opacity"), new("translate") };
        card.style.transitionDuration = new List<TimeValue> { new(FadeSeconds, TimeUnit.Second), new(FadeSeconds, TimeUnit.Second) };

        // The coloured edge says what kind of message this is; the text stays the same brass-and-parchment look everywhere.
        var edge = new VisualElement { pickingMode = PickingMode.Ignore }.Fill(accent).Fixed();
        edge.style.width = 4;
        edge.style.alignSelf = Align.Stretch;
        card.Add(edge);

        var column = new VisualElement { pickingMode = PickingMode.Ignore }.Column().Grow().Pad(12, 18);
        if (!string.IsNullOrEmpty(title))
        {
            var heading = UkControls.Text(title!, 18, UkTheme.Text, heading: true);
            heading.style.marginBottom = 2;
            column.Add(heading);
        }

        column.Add(UkControls.Text(text, 16, UkTheme.TextBody));
        card.Add(column);
        return card;
    }

    void Tick()
    {
        var now = Time.realtimeSinceStartup;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var entry = _entries[i];
            if (entry.Fading)
            {
                if (now >= entry.RemoveAt)
                {
                    entry.Card.RemoveFromHierarchy();
                    _entries.RemoveAt(i);
                }
            }
            else if (now >= entry.HideAt)
            {
                Fade(entry);
            }
        }
    }

    void Fade(Entry? entry)
    {
        if (entry == null || entry.Fading)
            return;
        entry.Fading = true;
        entry.Card.style.opacity = 0f;
        entry.RemoveAt = Time.realtimeSinceStartup + FadeSeconds;
    }

    int CountVisible()
    {
        var count = 0;
        foreach (var entry in _entries)
        {
            if (!entry.Fading)
                count++;
        }

        return count;
    }

    Entry? FirstVisible()
    {
        foreach (var entry in _entries)
        {
            if (!entry.Fading)
                return entry;
        }

        return null;
    }
}

/// <summary>Colour of a message's edge (see <see cref="UkMessageView"/>).</summary>
internal enum UkMessageTone
{
    Info,
    Success,
    Warning,
    Error,
}
