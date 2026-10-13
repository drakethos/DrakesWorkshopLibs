using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DrakeModsLibs.Integration;

/// <summary>
/// One extra tab in LockSmith's Lock menu for a chest, added by a feature mod (a Mail tab on mailboxes, say). Nothing happens
/// until a mod registers one and LockSmith is installed to show it; Libs only keeps the list.
/// </summary>
/// <remarks>
/// Tabs with the same <see cref="Group"/> expand together: LockSmith always shows group roots, and only shows
/// children while that group is the active one (Buy/Layout under Shop, etc.).
/// </remarks>
public sealed class PieceMenuTab
{
    public PieceMenuTab(string id, string title, Func<Container, bool> appliesTo, Action<PieceMenuTabContext> build)
        : this(id, title, appliesTo, build, group: null, isRoot: true)
    {
    }

    /// <param name="group">Shared key for smart tabs. Null or empty uses <paramref name="id"/> (own root).</param>
    /// <param name="isRoot">True = always visible as a sibling root; false = only while this group is active.</param>
    public PieceMenuTab(
        string id,
        string title,
        Func<Container, bool> appliesTo,
        Action<PieceMenuTabContext> build,
        string? group,
        bool isRoot)
    {
        Id = string.IsNullOrEmpty(id) ? throw new ArgumentException("Tab id required.", nameof(id)) : id;
        Title = string.IsNullOrEmpty(title) ? id : title;
        AppliesTo = appliesTo ?? (_ => false);
        Build = build ?? (_ => { });
        Group = string.IsNullOrEmpty(group) ? Id : group!;
        IsRoot = isRoot;
    }

    public string Id { get; }
    public string Title { get; }

    /// <summary>Smart-tab group id. Roots and children that share this expand together.</summary>
    public string Group { get; }

    /// <summary>True when this tab is always shown; false when it only appears while <see cref="Group"/> is active.</summary>
    public bool IsRoot { get; }

    /// <summary>Whether this tab shows for the given chest.</summary>
    public Func<Container, bool> AppliesTo { get; }

    /// <summary>Fill <see cref="PieceMenuTabContext.Body"/>. Called each time the tab is drawn.</summary>
    public Action<PieceMenuTabContext> Build { get; }
}

/// <summary>What a tab gets to draw into.</summary>
public sealed class PieceMenuTabContext
{
    public PieceMenuTabContext(Container piece, VisualElement body, bool canManage, Action rebuild)
    {
        Piece = piece;
        Body = body;
        CanManage = canManage;
        Rebuild = rebuild;
    }

    public Container Piece { get; }

    /// <summary>An empty column to fill with Uk controls.</summary>
    public VisualElement Body { get; }

    /// <summary>True for people who manage the chest in LockSmith (owner and team); guests and others should see it read-only.</summary>
    public bool CanManage { get; }

    /// <summary>Redraw the tab (after changing something it shows).</summary>
    public Action Rebuild { get; }
}

public static class PieceMenuTabs
{
    /// <summary>LockSmith's own Lock / Ward / Groups group id (host-side; not a registered extra).</summary>
    public const string LockSmithGroup = "locksmith";

    static readonly List<PieceMenuTab> Tabs = new();

    /// <summary>Add a tab (a second tab with the same id replaces the first).</summary>
    public static void Register(PieceMenuTab tab)
    {
        Tabs.RemoveAll(t => t.Id == tab.Id);
        Tabs.Add(tab);
    }

    /// <summary>The tabs that apply to <paramref name="piece"/>, in the order they were registered.</summary>
    public static List<PieceMenuTab> For(Container piece)
    {
        var result = new List<PieceMenuTab>();
        foreach (var tab in Tabs)
        {
            try
            {
                if (tab.AppliesTo(piece))
                    result.Add(tab);
            }
            catch
            {
                // A broken predicate hides its own tab only.
            }
        }

        return result;
    }
}
