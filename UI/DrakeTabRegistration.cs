using System;

namespace DrakeModsLibs.UI;

/// <summary>
/// One feature-mod tab in <see cref="DrakeTabHost"/>. Only registered (installed) mods appear.
/// Hint phrases stay owned by the feature mod (localized getters) — Libs only composes the chord line.
/// </summary>
public sealed class DrakeTabRegistration
{
    public DrakeTabRegistration(
        string id,
        string title,
        int priority,
        Func<ItemDrop.ItemData, bool> isAvailable,
        Action<DrakeTabPageContext> show,
        Func<ItemDrop.ItemData, bool>? claimDefault = null,
        Action? hide = null,
        Func<string>? getHintPhrase = null,
        Func<string>? getTitle = null)
    {
        Id = string.IsNullOrEmpty(id) ? throw new ArgumentException("Tab id required.", nameof(id)) : id;
        _fallbackTitle = string.IsNullOrEmpty(title) ? id : title;
        Priority = priority;
        IsAvailable = isAvailable ?? (_ => false);
        Show = show ?? (_ => { });
        ClaimDefault = claimDefault ?? (_ => false);
        Hide = hide;
        GetHintPhrase = getHintPhrase ?? (() => "");
        GetTitle = getTitle ?? (() => _fallbackTitle);
    }

    readonly string _fallbackTitle;

    public string Id { get; }

    /// <summary>Resolved tab chrome title (prefer <see cref="GetTitle"/> at display time).</summary>
    public string Title => SafeTitle();

    public int Priority { get; internal set; }
    public Func<ItemDrop.ItemData, bool> IsAvailable { get; }
    public Func<ItemDrop.ItemData, bool> ClaimDefault { get; }
    public Action<DrakeTabPageContext> Show { get; }

    /// <summary>Optional: hide this tab’s feature UI when switching away or closing the host.</summary>
    public Action? Hide { get; }

    /// <summary>
    /// Feature-mod localized single-mode phrase after “to …” (e.g. “configure lock tool”).
    /// Empty means this tab cannot supply a single-mode hint.
    /// </summary>
    public Func<string> GetHintPhrase { get; }

    /// <summary>Feature-mod localized tab title getter.</summary>
    public Func<string> GetTitle { get; }

    /// <summary>Suggested baseline for RenameIt (feature tabs should register higher).</summary>
    public const int DefaultRenamePriority = 100;

    /// <summary>Suggested baseline for LockSmith key-pass (and similar feature tabs).</summary>
    public const int DefaultFeaturePriority = 200;

    public const string RenameItTabId = "renameit";
    public const string LockSmithKeyPassTabId = "locksmith.keypass";

    string SafeTitle()
    {
        try
        {
            var t = GetTitle?.Invoke();
            return string.IsNullOrEmpty(t) ? _fallbackTitle : t!;
        }
        catch
        {
            return _fallbackTitle;
        }
    }
}
