using System.Collections.Generic;
using DrakeModsLibs.Data;
using DrakeModsLibs.Display;
using DrakeModsLibs.Runtime;
using DrakeModsLibs.Sync;
using DrakeModsLibs.Tags;

namespace DrakeModsLibs.API;

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

    /// <summary>
    /// Recomputes item-stand hover labels from live display rules (rename + durability modifiers).
    /// Call after toggling durability (or other name-modifier) config so stands update immediately.
    /// </summary>
    public static void RefreshItemStandDisplayNames() =>
        Patches.ItemStandPatch.RefreshAllItemStandDisplayNames();

    public static bool HasTag(ItemDrop.ItemData? item, string tagKey) => DrakeTagManager.HasTag(item, tagKey);
    public static void SetTag(ItemDrop.ItemData item, string tagKey) => DrakeTagManager.SetTag(item, tagKey);
    public static void ClearTag(ItemDrop.ItemData item, string tagKey) => DrakeTagManager.ClearTag(item, tagKey);

    public static void RegisterEditValidator(CustomizeOperation operation, CustomizeEditValidator validator) =>
        CustomizationGatekeeper.RegisterValidator(operation, validator);

    public static bool CanPerform(CustomizeOperation operation, ItemDrop.ItemData? item, Player? player) =>
        CustomizationGatekeeper.CanPerform(operation, item, player);

    public static void RegisterTagBlockRule(string tagKey, CustomizeOperation blockedOperations) =>
        CustomizationGatekeeper.RegisterTagBlockRule(tagKey, blockedOperations);

    /// <summary>
    /// Register a tag that blocks <paramref name="blockedOperations"/>. When
    /// <paramref name="suppressRenameInventoryUi"/> is true, RenameIt inventory menu/tooltips
    /// stand down for tagged items (no RenameIt code change needed for new tags).
    /// </summary>
    public static void RegisterTagBlockRule(
        string tagKey,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi) =>
        CustomizationGatekeeper.RegisterTagBlockRule(tagKey, blockedOperations, suppressRenameInventoryUi);

    /// <summary>
    /// Register a tag block. When <paramref name="hardLock"/> is true, <see cref="CanPerform"/>
    /// always denies — admin/VIP <c>TagBypass</c> cannot override.
    /// </summary>
    public static void RegisterTagBlockRule(
        string tagKey,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi,
        bool hardLock) =>
        CustomizationGatekeeper.RegisterTagBlockRule(
            tagKey, blockedOperations, suppressRenameInventoryUi, hardLock);

    public static void RegisterDisplayNameLayer(IItemDisplayNameLayer layer) =>
        DisplayNameResolutionPipeline.RegisterLayer(layer);

    public static bool CanRenameItem(ItemDrop.ItemData? item, Player? player) =>
        CanPerform(CustomizeOperation.RenameName, item, player);

    public static bool CanEditDescription(ItemDrop.ItemData? item, Player? player) =>
        CanPerform(CustomizeOperation.RenameDescription, item, player);

    public static bool CanEditCraftedBy(ItemDrop.ItemData? item, Player? player) =>
        CanPerform(CustomizeOperation.EditCraftedBy, item, player);

    /// <summary>True when a hard-lock tag blocks this operation (TagBypass cannot override).</summary>
    public static bool IsHardBlocked(CustomizeOperation operation, ItemDrop.ItemData? item) =>
        CustomizationGatekeeper.IsHardBlockedByTag(operation, item);

    /// <summary>
    /// When true, inventory rename UIs (RenameIt menu + tooltip hints) should leave this item alone.
    /// Soft suppress tags (e.g. <see cref="DrakeCustomDataKeys.NoRename"/>) respect admin/VIP
    /// <c>TagBypass</c>. Hard suppress (<see cref="DrakeCustomDataKeys.HardNoRename"/> /
    /// <see cref="DrakeCustomDataKeys.Immutable"/>) always stands down Rename inventory UI.
    /// Direct <see cref="SetCustomName"/> still works for mods that own their own relabel UI.
    /// </summary>
    public static bool IsRenameInventorySuppressed(ItemDrop.ItemData? item) =>
        CustomizationGatekeeper.IsRenameInventorySuppressed(item);

    /// <inheritdoc cref="IsRenameInventorySuppressed(ItemDrop.ItemData?)"/>
    public static bool IsRenameInventorySuppressed(ItemDrop.ItemData? item, Player? player) =>
        CustomizationGatekeeper.IsRenameInventorySuppressed(item, player);

    // --- Soft stamps (TagBypass / admin may still allow CanPerform) ---

    public static void BlockRename(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.NoRename);
    public static void BlockDescription(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.NoDescription);
    public static void BlockCraftedByEdit(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.NoCraftedByEdit);
    public static void BlockAllEdits(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.QuestItem);

    public static void ClearBlockRename(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.NoRename);
    public static void ClearBlockDescription(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.NoDescription);
    public static void ClearBlockCraftedByEdit(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.NoCraftedByEdit);
    public static void ClearBlockAllEdits(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.QuestItem);

    // --- Prefab / family exclusions (startup; no per-item stamp required) ---

    /// <summary>
    /// Soft or hard family exclusion. <paramref name="match"/> is token / prefab / localized name
    /// (same as RenameIt ExcludedNames). Soft exclusions respect TagBypass; hard do not.
    /// </summary>
    public static void RegisterItemExclusion(
        string match,
        CustomizeOperation blockedOperations,
        bool suppressRenameInventoryUi = false,
        bool hardLock = false) =>
        CustomizationGatekeeper.RegisterItemExclusion(
            new ItemExclusionRule(match, blockedOperations, suppressRenameInventoryUi, hardLock));

    /// <summary>
    /// Defer all matching items to <paramref name="authorityId"/> (no per-drop stamp).
    /// Requires <see cref="RegisterDeferredEditAuthority"/>.
    /// </summary>
    public static void RegisterItemDeferral(
        string match,
        string authorityId,
        CustomizeOperation operations,
        bool suppressRenameInventoryUi = false) =>
        CustomizationGatekeeper.RegisterItemExclusion(
            new ItemExclusionRule(
                match,
                operations,
                suppressRenameInventoryUi,
                hardLock: false,
                deferredAuthorityId: authorityId));

    // --- Hard lock stamps (RenameIt-facing; owning mods may still SetCustomName) ---

    public static void HardBlockRename(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.HardNoRename);
    public static void HardBlockDescription(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.HardNoDescription);
    public static void HardBlockCraftedByEdit(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.HardNoCraftedByEdit);
    public static void MarkImmutable(ItemDrop.ItemData item) => SetTag(item, DrakeCustomDataKeys.Immutable);

    public static void ClearHardBlockRename(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.HardNoRename);
    public static void ClearHardBlockDescription(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.HardNoDescription);
    public static void ClearHardBlockCraftedByEdit(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.HardNoCraftedByEdit);
    public static void ClearImmutable(ItemDrop.ItemData item) => ClearTag(item, DrakeCustomDataKeys.Immutable);

    // --- Deferred authority ("I am in charge of whether rename is OK") ---

    /// <summary>
    /// Register at startup. When an item is deferred to <paramref name="authorityId"/>,
    /// <see cref="CanPerform"/> asks <paramref name="handler"/> (TagBypass does not auto-win).
    /// </summary>
    public static void RegisterDeferredEditAuthority(
        string authorityId,
        CustomizeOperation operations,
        DeferredEditHandler handler) =>
        CustomizationGatekeeper.RegisterDeferredEditAuthority(authorityId, operations, handler);

    /// <summary>
    /// Stamp an item so RenameIt defers the given ops to <paramref name="authorityId"/>.
    /// Pair with <see cref="RegisterDeferredEditAuthority"/> in the owning mod.
    /// </summary>
    public static void DeferEditsTo(
        ItemDrop.ItemData item,
        string authorityId,
        CustomizeOperation operations,
        bool suppressRenameInventoryUi = false)
    {
        if (item == null || string.IsNullOrWhiteSpace(authorityId) || operations == CustomizeOperation.None)
            return;

        item.m_customData ??= new System.Collections.Generic.Dictionary<string, string>();
        item.m_customData[DrakeCustomDataKeys.EditAuthority] = authorityId.Trim();

        ClearDeferOperationTags(item);

        if ((operations & CustomizeOperation.AllEdits) == CustomizeOperation.AllEdits)
        {
            SetTag(item, DrakeCustomDataKeys.DeferEdits);
        }
        else
        {
            if ((operations & CustomizeOperation.RenameName) != 0)
                SetTag(item, DrakeCustomDataKeys.DeferRename);
            if ((operations & CustomizeOperation.RenameDescription) != 0)
                SetTag(item, DrakeCustomDataKeys.DeferDescription);
            if ((operations & CustomizeOperation.EditCraftedBy) != 0)
                SetTag(item, DrakeCustomDataKeys.DeferCraftedBy);
        }

        if (suppressRenameInventoryUi)
            SetTag(item, DrakeCustomDataKeys.DeferSuppressUi);
        else
            ClearTag(item, DrakeCustomDataKeys.DeferSuppressUi);
    }

    /// <summary>Remove deferral stamps (authority id + defer tags + suppress-UI tag).</summary>
    public static void ClearEditDeferral(ItemDrop.ItemData item)
    {
        if (item == null)
            return;
        ClearDeferOperationTags(item);
        ClearTag(item, DrakeCustomDataKeys.DeferSuppressUi);
        item.m_customData?.Remove(DrakeCustomDataKeys.EditAuthority);
    }

    public static bool IsEditDeferred(CustomizeOperation operation, ItemDrop.ItemData? item) =>
        CustomizationGatekeeper.IsDeferredFor(operation, item);

    public static string? GetEditAuthorityId(ItemDrop.ItemData? item) =>
        CustomizationGatekeeper.GetEditAuthorityId(item);

    static void ClearDeferOperationTags(ItemDrop.ItemData item)
    {
        ClearTag(item, DrakeCustomDataKeys.DeferRename);
        ClearTag(item, DrakeCustomDataKeys.DeferDescription);
        ClearTag(item, DrakeCustomDataKeys.DeferCraftedBy);
        ClearTag(item, DrakeCustomDataKeys.DeferEdits);
    }


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
