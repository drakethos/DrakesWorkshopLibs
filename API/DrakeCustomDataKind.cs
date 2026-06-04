namespace DrakesWorkshopLibs.API;

/// <summary>How a <c>m_customData</c> key is interpreted when read or dumped.</summary>
public enum DrakeCustomDataKind
{
    /// <summary>Flag stored as <c>1</c> / <c>true</c> (see <see cref="CustomizeLibsAPI.HasTag"/>).</summary>
    Tag,
    /// <summary>Free-form string (rename text, description, price, etc.).</summary>
    Text,
}
