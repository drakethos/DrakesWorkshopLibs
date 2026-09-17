using System;
using UnityEngine;

namespace DrakeModsLibs.UI;

/// <summary>
/// Context passed to a tab's show callback when the libs tab host opens or switches tabs.
/// </summary>
public sealed class DrakeTabPageContext
{
    public DrakeTabPageContext(ItemDrop.ItemData item, string tabId, Transform contentRoot)
    {
        Item = item;
        TabId = tabId ?? "";
        ContentRoot = contentRoot;
    }

    public ItemDrop.ItemData Item { get; }
    public string TabId { get; }

    /// <summary>Parent for tab content under the host wood panel (may be the panel itself).</summary>
    public Transform ContentRoot { get; }
}
