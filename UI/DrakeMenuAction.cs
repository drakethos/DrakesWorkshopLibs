using System;

namespace DrakeModsLibs.UI;

/// <summary>One row in a <see cref="DrakeWoodActionMenu"/>.</summary>
public sealed class DrakeMenuAction
{
    public DrakeMenuAction(string label, Action onClick, bool enabled = true)
    {
        Label = label ?? "";
        OnClick = onClick ?? (() => { });
        Enabled = enabled;
    }

    public string Label { get; }
    public Action OnClick { get; }
    public bool Enabled { get; set; }
}
