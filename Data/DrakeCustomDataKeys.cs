namespace DrakeModsLibs.Data;

public static class DrakeCustomDataKeys
{
    public const string Prefix = "Drake_";

    public const string Rename = "Drake_Rename";
    public const string RenameDescription = "Drake_Rename_Desc";
    public const string CraftedByDisplay = "Drake_CraftedByDisplay";
    public const string CraftedByLineLabel = "Drake_CraftedByLineLabel";
    public const string RenameUnlocked = "Drake_RenameUnlocked";
    /// <summary>When set, non-owners may rewrite name/description if PublicRewriteEnabled (not crafted-by).</summary>
    public const string PublicRewrite = "Drake_PublicRewrite";
    public const string ItemStandHoverName = "DrakeRenameIt_CustomName";
    public const string NoRename = "Drake_NoRename";
    public const string NoDescription = "Drake_NoDesc";
    public const string NoCraftedByEdit = "Drake_NoCraftedByEdit";
    public const string QuestItem = "Drake_QuestItem";

    /// <summary>Hard: rename blocked even for admin/VIP TagBypass.</summary>
    public const string HardNoRename = "Drake_HardNoRename";
    /// <summary>Hard: description edit blocked even for admin/VIP TagBypass.</summary>
    public const string HardNoDescription = "Drake_HardNoDesc";
    /// <summary>Hard: crafted-by edit blocked even for admin/VIP TagBypass.</summary>
    public const string HardNoCraftedByEdit = "Drake_HardNoCraftedBy";
    /// <summary>Hard: all RenameIt edits blocked even for admin/VIP TagBypass.</summary>
    public const string Immutable = "Drake_Immutable";

    /// <summary>Defer rename-name decisions to <see cref="EditAuthority"/>.</summary>
    public const string DeferRename = "Drake_DeferRename";
    /// <summary>Defer description-edit decisions to <see cref="EditAuthority"/>.</summary>
    public const string DeferDescription = "Drake_DeferDesc";
    /// <summary>Defer crafted-by-edit decisions to <see cref="EditAuthority"/>.</summary>
    public const string DeferCraftedBy = "Drake_DeferCraftedBy";
    /// <summary>Defer all edit ops to <see cref="EditAuthority"/>.</summary>
    public const string DeferEdits = "Drake_DeferEdits";
    /// <summary>Authority id string (e.g. <c>LockSmith</c>) for deferred edits.</summary>
    public const string EditAuthority = "Drake_EditAuthority";
    /// <summary>When set with a deferral, suppress RenameIt inventory UI (owning mod owns menu).</summary>
    public const string DeferSuppressUi = "Drake_DeferSuppressUi";

    public const string MarketPrice = "Drake_MarketPrice";
}
