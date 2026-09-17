using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.API;

namespace DrakeModsLibs.Data;

/// <summary>Registry of known Drake-suite <c>m_customData</c> keys; other mods can register their keys at load.</summary>
public static class DrakeCustomDataCatalog
{
    public const string ModWorkshopLibs = "DrakeModsLibs";
    public const string ModRenameIt = "DrakesRenameIt";
    public const string ModQuestItems = "DrakesQuestItems";
    public const string ModItemShop = "DrakesItemShop";

    static readonly Dictionary<string, DrakeCustomDataField> ByKey = new(StringComparer.Ordinal);
    static readonly Dictionary<string, List<DrakeCustomDataField>> ByMod = new(StringComparer.Ordinal);

    static DrakeCustomDataCatalog() => RegisterBuiltInFields();

    public static void RegisterModFields(string modId, IEnumerable<DrakeCustomDataField> fields)
    {
        if (string.IsNullOrEmpty(modId) || fields == null) return;
        if (!ByMod.TryGetValue(modId, out var list))
        {
            list = new List<DrakeCustomDataField>();
            ByMod[modId] = list;
        }

        foreach (var field in fields)
        {
            if (field == null || string.IsNullOrEmpty(field.Key)) continue;
            ByKey[field.Key] = field;
            list.RemoveAll(f => string.Equals(f.Key, field.Key, StringComparison.Ordinal));
            list.Add(field);
        }
    }

    public static bool TryGetField(string key, out DrakeCustomDataField field) =>
        ByKey.TryGetValue(key, out field!);

    public static IReadOnlyList<DrakeCustomDataField> GetFieldsForMod(string modId)
    {
        if (string.IsNullOrEmpty(modId) || !ByMod.TryGetValue(modId, out var list))
            return Array.Empty<DrakeCustomDataField>();
        return list.ToArray();
    }

    public static IReadOnlyList<DrakeCustomDataField> GetAllFields() => ByKey.Values.ToArray();

    public static bool IsDrakeKey(string key) =>
        !string.IsNullOrEmpty(key) &&
        (key.StartsWith(DrakeCustomDataKeys.Prefix, StringComparison.Ordinal) || ByKey.ContainsKey(key));

    static void RegisterBuiltInFields()
    {
        RegisterModFields(ModRenameIt, new[]
        {
            new DrakeCustomDataField(DrakeCustomDataKeys.Rename, ModRenameIt, DrakeCustomDataKind.Text, "Custom name"),
            new DrakeCustomDataField(DrakeCustomDataKeys.RenameDescription, ModRenameIt, DrakeCustomDataKind.Text, "Custom description"),
            new DrakeCustomDataField(DrakeCustomDataKeys.CraftedByDisplay, ModRenameIt, DrakeCustomDataKind.Text, "Crafted-by display"),
            new DrakeCustomDataField(DrakeCustomDataKeys.CraftedByLineLabel, ModRenameIt, DrakeCustomDataKind.Text, "Crafted-by line label"),
            new DrakeCustomDataField(DrakeCustomDataKeys.RenameUnlocked, ModRenameIt, DrakeCustomDataKind.Tag, "Rename unlocked"),
            new DrakeCustomDataField(DrakeCustomDataKeys.PublicRewrite, ModRenameIt, DrakeCustomDataKind.Tag, "Anyone can rewrite name/desc"),
            new DrakeCustomDataField(DrakeCustomDataKeys.ItemStandHoverName, ModRenameIt, DrakeCustomDataKind.Text, "Item stand hover name (ZDO)"),
        });

        RegisterModFields(ModWorkshopLibs, new[]
        {
            new DrakeCustomDataField(DrakeCustomDataKeys.NoRename, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Soft block rename; suppresses RenameIt inventory UI"),
            new DrakeCustomDataField(DrakeCustomDataKeys.NoDescription, ModWorkshopLibs, DrakeCustomDataKind.Tag, "Soft block description edit"),
            new DrakeCustomDataField(DrakeCustomDataKeys.NoCraftedByEdit, ModWorkshopLibs, DrakeCustomDataKind.Tag, "Soft block crafted-by edit"),
            new DrakeCustomDataField(DrakeCustomDataKeys.HardNoRename, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Hard block rename (no admin bypass); suppresses inventory UI"),
            new DrakeCustomDataField(DrakeCustomDataKeys.HardNoDescription, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Hard block description (no admin bypass); suppresses inventory UI"),
            new DrakeCustomDataField(DrakeCustomDataKeys.HardNoCraftedByEdit, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Hard block crafted-by (no admin bypass); suppresses inventory UI"),
            new DrakeCustomDataField(DrakeCustomDataKeys.Immutable, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Hard block all edits (no admin bypass); suppresses inventory UI"),
            new DrakeCustomDataField(DrakeCustomDataKeys.DeferRename, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Defer rename-name to EditAuthority"),
            new DrakeCustomDataField(DrakeCustomDataKeys.DeferDescription, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Defer description edit to EditAuthority"),
            new DrakeCustomDataField(DrakeCustomDataKeys.DeferCraftedBy, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Defer crafted-by edit to EditAuthority"),
            new DrakeCustomDataField(DrakeCustomDataKeys.DeferEdits, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "Defer all edits to EditAuthority"),
            new DrakeCustomDataField(DrakeCustomDataKeys.EditAuthority, ModWorkshopLibs, DrakeCustomDataKind.Text,
                "Deferred edit authority id (e.g. LockSmith)"),
            new DrakeCustomDataField(DrakeCustomDataKeys.DeferSuppressUi, ModWorkshopLibs, DrakeCustomDataKind.Tag,
                "With deferral: suppress RenameIt inventory UI"),
        });

        RegisterModFields(ModQuestItems, new[]
        {
            new DrakeCustomDataField(DrakeCustomDataKeys.QuestItem, ModQuestItems, DrakeCustomDataKind.Tag,
                "Soft block all edits; suppresses RenameIt inventory UI"),
        });
        RegisterModFields(ModItemShop, new[]
        {
            new DrakeCustomDataField(DrakeCustomDataKeys.MarketPrice, ModItemShop, DrakeCustomDataKind.Text, "Market price"),
        });
    }
}
