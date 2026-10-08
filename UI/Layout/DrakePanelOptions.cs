namespace DrakeModsLibs.UI;

/// <summary>
/// Sizing for the shared wood panels. Panels size to their content between
/// <see cref="MinHeight"/> and <see cref="MaxHeight"/>; anything past the max scrolls.
/// Defaults are the confirm dialog's sizes, so callers that pass nothing see no change.
/// </summary>
public sealed class DrakePanelOptions
{
    /// <summary>Panel width in pixels.</summary>
    public float Width { get; set; } = 300f;

    /// <summary>Smallest panel height. Short content still gets this height.</summary>
    public float MinHeight { get; set; } = 178f;

    /// <summary>Largest panel height. Content past it scrolls instead of running off the panel.</summary>
    public float MaxHeight { get; set; } = 420f;

    /// <summary>Width of the panel's buttons.</summary>
    public float ButtonWidth { get; set; } = 110f;

    /// <summary>Height of the panel's buttons.</summary>
    public float ButtonHeight { get; set; } = 30f;

    /// <summary>Vertical step between stacked action rows (action menu only).</summary>
    public float RowGap { get; set; } = 40f;
}
