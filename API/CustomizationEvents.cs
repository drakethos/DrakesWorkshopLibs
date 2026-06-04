using System;

namespace DrakesWorkshopLibs.API;

/// <summary>
/// Suite-wide item customization change notifications (raised by RenameIt and other Drake mods).
/// Subscribe here for logging, moderation, or cross-mod reactions.
/// </summary>
public static class CustomizationEvents
{
    /// <summary>Display name changed; args: player, item, oldName, newName.</summary>
    public static event Action<Player, ItemDrop.ItemData, string, string>? OnItemNameChanged;

    /// <summary>Custom description changed; args: player, item, oldDescription, newDescription.</summary>
    public static event Action<Player, ItemDrop.ItemData, string, string>? OnItemDescriptionChanged;

    /// <summary>Crafted-by display changed; args: player, item, itemPrefabName, oldDisplay, newDisplay.</summary>
    public static event Action<Player, ItemDrop.ItemData, string, string, string>? OnCraftedByDisplayChanged;

    public static void RaiseNameChanged(Player player, ItemDrop.ItemData item, string oldName, string newName) =>
        OnItemNameChanged?.Invoke(player, item, oldName, newName);

    public static void RaiseDescriptionChanged(Player player, ItemDrop.ItemData item, string oldDesc, string newDesc) =>
        OnItemDescriptionChanged?.Invoke(player, item, oldDesc, newDesc);

    public static void RaiseCraftedByDisplayChanged(Player player, ItemDrop.ItemData item, string itemPrefabName, string oldDisplay, string newDisplay) =>
        OnCraftedByDisplayChanged?.Invoke(player, item, itemPrefabName, oldDisplay, newDisplay);
}

