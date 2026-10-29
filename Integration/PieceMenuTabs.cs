using System;
using System.Collections.Generic;
using UnityEngine;
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

    /// <summary>
    /// When set, this tab can also show on a non-chest host (a door). Chest matching still uses <see cref="AppliesTo"/>.
    /// Used by RenameIt's Name tab. Null means chest-only, which is every tab registered before this existed.
    /// </summary>
    public Func<Component, bool>? AppliesToHost { get; set; }

    /// <summary>Smart-tab group id. Roots and children that share this expand together.</summary>
    public string Group { get; }

    /// <summary>True when this tab is always shown; false when it only appears while <see cref="Group"/> is active.</summary>
    public bool IsRoot { get; }

    /// <summary>
    /// A root that should become the tab the menu opens on (Shop, Mail). False for a root that only sits beside the others,
    /// such as RenameIt's Name tab: it is always visible and does not take the opening tab.
    /// </summary>
    public bool Leads { get; set; } = true;

    /// <summary>Whether this tab shows for the given chest.</summary>
    public Func<Container, bool> AppliesTo { get; }

    /// <summary>Fill <see cref="PieceMenuTabContext.Body"/>. Called each time the tab is drawn.</summary>
    public Action<PieceMenuTabContext> Build { get; }
}

/// <summary>What a tab gets to draw into.</summary>
public sealed class PieceMenuTabContext
{
    public PieceMenuTabContext(Container piece, VisualElement body, bool canManage, Action rebuild)
        : this((Component)piece, body, canManage, rebuild)
    {
    }

    /// <summary>
    /// A tab on a door or other host that is not a chest. <see cref="Piece"/> is set when <paramref name="host"/> is
    /// (or sits on) a container; door tabs should read <see cref="Host"/> and not <see cref="Piece"/>.
    /// </summary>
    public PieceMenuTabContext(Component host, VisualElement body, bool canManage, Action rebuild)
    {
        Host = host ? host : throw new ArgumentNullException(nameof(host));
        var chest = host as Container ?? host.GetComponentInParent<Container>();
        Piece = chest!;
        Body = body ?? throw new ArgumentNullException(nameof(body));
        CanManage = canManage;
        Rebuild = rebuild ?? (() => { });
    }

    /// <summary>The container when this tab was opened on a chest. Door tabs use <see cref="Host"/>; <see cref="Piece"/> is not set for them.</summary>
    public Container Piece { get; }

    /// <summary>The chest or door the tab was opened on.</summary>
    public Component Host { get; }

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

    /// <summary>
    /// Tabs for a door or other non-chest host (<see cref="PieceMenuTab.AppliesToHost"/>). A container uses <see cref="For(Container)"/>.
    /// </summary>
    public static List<PieceMenuTab> For(Component host)
    {
        if (host is Container container)
            return For(container);

        var result = new List<PieceMenuTab>();
        if (!host)
            return result;

        foreach (var tab in Tabs)
        {
            if (tab.AppliesToHost == null)
                continue;
            try
            {
                if (tab.AppliesToHost(host))
                    result.Add(tab);
            }
            catch
            {
                // A broken predicate hides its own tab only.
            }
        }

        return result;
    }

    /// <summary>Lower comes first. Ids missing from <see cref="DrakeIntegrationConfig.PieceTabOrder"/> sort after the listed ones.</summary>
    public static int Rank(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return int.MaxValue;
        var order = DrakeIntegrationConfig.PieceTabOrder.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < order.Length; i++)
        {
            if (string.Equals(order[i].Trim(), id, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 1000;
    }
}
