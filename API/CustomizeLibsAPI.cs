using System.Collections.Generic;
using DrakesWorkshopLibs.Data;
using DrakesWorkshopLibs.Display;
using DrakesWorkshopLibs.Runtime;
using DrakesWorkshopLibs.Sync;
using DrakesWorkshopLibs.Tags;

namespace DrakesWorkshopLibs.API;

public static class CustomizeLibsAPI
{
    public static DrakeConfigSync CreateConfigSync(string modId, string displayName, string currentVersion, string minimumRequiredVersion = null) =>
        DrakeConfigSync.Create(modId, displayName, currentVersion, minimumRequiredVersion);

    public static string GetDisplayNameForUi(ItemDrop.ItemData? item, bool localize = false) =>
        ItemDisplayService.GetDisplayNameForUi(item, localize);

    public static string GetProperName(ItemDrop.ItemData? item) => ItemDisplayService.GetProperName(item);
    public static string GetProperDescription(ItemDrop.ItemData? item) => ItemDisplayService.GetProperDescription(item);
    public static bool HasCustomName(ItemDrop.ItemData? item) => ItemDisplayService.HasCustomName(item);
    public static bool HasCustomDescription(ItemDrop.ItemData? item) => ItemDisplayService.HasCustomDescription(item);
    public static string GetProperName(ItemDrop.ItemData? item, string defaultName) =>
        ItemDisplayService.GetProperName(item, defaultName);
    public static string GetProperDescription(ItemDrop.ItemData? item, string defaultDesc) =>
        ItemDisplayService.GetProperDescription(item, defaultDesc);
    public static bool HasCraftedByDisplayOverride(ItemDrop.ItemData? item) =>
        ItemDisplayService.HasCraftedByDisplayOverride(item);
    public static bool HasCraftedByLineLabelOverride(ItemDrop.ItemData? item) =>
        ItemDisplayService.HasCraftedByLineLabelOverride(item);
    public static bool HasAnyCustomization(ItemDrop.ItemData? item) => ItemDisplayService.HasAnyCustomization(item);
    public static string GetCraftedByDisplay(ItemDrop.ItemData? item) => ItemDisplayService.GetCraftedByDisplay(item);
    public static void SetCustomName(ItemDrop.ItemData item, string? name) => ItemDisplayService.SetCustomName(item, name);
    public static void SetCustomDescription(ItemDrop.ItemData item, string? desc) => ItemDisplayService.SetCustomDescription(item, desc);
    public static void SetCraftedByDisplay(ItemDrop.ItemData item, string? display) =>
        ItemDisplayService.SetCraftedByDisplay(item, display);
    public static void SetCraftedByLineLabel(ItemDrop.ItemData item, string? lineLabel) =>
        ItemDisplayService.SetCraftedByLineLabel(item, lineLabel);
    public static void ClearCraftedByOverrides(ItemDrop.ItemData item) => ItemDisplayService.ClearCraftedByOverrides(item);
    public static bool IsBlockedByTag(CustomizeOperation operation, ItemDrop.ItemData? item) =>
        CustomizationGatekeeper.IsBlockedByTag(operation, item);

    public static void RegisterDisplayNameModifier(IDisplayNameModifier modifier)
    {
        if (modifier != null && !CustomizeLibsRuntime.DisplayNameModifiers.Contains(modifier))
            CustomizeLibsRuntime.DisplayNameModifiers.Add(modifier);
    }

    public static void RegisterStackMergePolicy(IStackMergePolicy policy) =>
        CustomizeLibsRuntime.StackMergePolicy = policy;

    public static void SetShowItemStandItemNameWhenNoAccess(bool value) =>
        CustomizeLibsRuntime.ShowItemStandItemNameWhenNoAccess = value;

    public static bool HasTag(ItemDrop.ItemData? item, string tagKey) => DrakeTagManager.HasTag(item, tagKey);
    public static void SetTag(ItemDrop.ItemData item, string tagKey) => DrakeTagManager.SetTag(item, tagKey);
    public static void ClearTag(ItemDrop.ItemData item, string tagKey) => DrakeTagManager.ClearTag(item, tagKey);

    public static void RegisterEditValidator(CustomizeOperation operation, CustomizeEditValidator validator) =>
        CustomizationGatekeeper.RegisterValidator(operation, validator);

    public static bool CanPerform(CustomizeOperation operation, ItemDrop.ItemData? item, Player? player) =>
        CustomizationGatekeeper.CanPerform(operation, item, player);

    public static void RegisterTagBlockRule(string tagKey, CustomizeOperation blockedOperations) =>
        CustomizationGatekeeper.RegisterTagBlockRule(tagKey, blockedOperations);

    public static void RegisterDisplayNameLayer(IItemDisplayNameLayer layer) =>
        DisplayNameResolutionPipeline.RegisterLayer(layer);

    public static bool CanRenameItem(ItemDrop.ItemData? item, Player? player) =>
        CanPerform(CustomizeOperation.RenameName, item, player);

    public static bool CanEditDescription(ItemDrop.ItemData? item, Player? player) =>
        CanPerform(CustomizeOperation.RenameDescription, item, player);

    public static bool CanEditCraftedBy(ItemDrop.ItemData? item, Player? player) =>
        CanPerform(CustomizeOperation.EditCraftedBy, item, player);


    // --- Custom data read / dump (for logging, moderation, other mods) ---

    /// <summary>Raw string from <c>m_customData</c> for <paramref name="key"/>, or null if missing.</summary>
    public static bool TryGetCustomDataRaw(ItemDrop.ItemData? item, string key, out string? value) =>
        ItemCustomDataReader.TryGetRawValue(item, key, out value);

    /// <summary>Same as <see cref="TryGetCustomDataRaw"/>; convenience when you only need the string.</summary>
    public static string? GetCustomDataRaw(ItemDrop.ItemData? item, string key) =>
        TryGetCustomDataRaw(item, key, out var v) ? v : null;

    /// <summary>Human-friendly value: tags → <c>true</c>/null, text → stored value. Use for logs and dumps.</summary>
    public static string? GetCustomDataValue(ItemDrop.ItemData? item, string key) =>
        ItemCustomDataReader.GetDisplayValue(item, key);

    /// <summary>All Drake custom-data on an item; JSON or neat text. Filter by <see cref="ItemCustomDataDumpOptions.ModId"/> or <see cref="ItemCustomDataDumpOptions.Key"/>.</summary>
    public static string DumpItemCustomData(ItemDrop.ItemData? item, ItemCustomDataDumpOptions? options = null) =>
        ItemCustomDataReader.Dump(item, options);

    /// <summary>Single key, neat one-liner (e.g. logging a rename).</summary>
    public static string DumpItemCustomData(ItemDrop.ItemData? item, string key) =>
        DumpItemCustomData(item, new ItemCustomDataDumpOptions { Key = key });

    /// <summary>All keys for one suite mod (e.g. <see cref="DrakeCustomDataCatalog.ModRenameIt"/>).</summary>
    public static string DumpItemCustomDataForMod(ItemDrop.ItemData? item, string modId, ItemCustomDataDumpFormat format = ItemCustomDataDumpFormat.Neat) =>
        DumpItemCustomData(item, new ItemCustomDataDumpOptions { ModId = modId, Format = format });

    /// <summary>All registered Drake keys on the item.</summary>
    public static string DumpAllDrakeCustomData(ItemDrop.ItemData? item, ItemCustomDataDumpFormat format = ItemCustomDataDumpFormat.Neat) =>
        DumpItemCustomData(item, new ItemCustomDataDumpOptions { DrakeKeysOnly = true, Format = format });

    public static void RegisterModCustomDataFields(string modId, IEnumerable<DrakeCustomDataField> fields) =>
        DrakeCustomDataCatalog.RegisterModFields(modId, fields);

    public static IReadOnlyList<DrakeCustomDataField> GetRegisteredCustomDataFields(string? modId = null) =>
        string.IsNullOrEmpty(modId)
            ? DrakeCustomDataCatalog.GetAllFields()
            : DrakeCustomDataCatalog.GetFieldsForMod(modId);
}
