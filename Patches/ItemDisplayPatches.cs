using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using DrakesWorkshopLibs.Data;
using DrakesWorkshopLibs.Display;
using DrakesWorkshopLibs.Runtime;


using DrakesWorkshopLibs.Display;

using HarmonyLib;



namespace DrakesWorkshopLibs.Patches;

/// <summary>Shared hover text rename logic (ItemDrop + ItemStand / container stands).</summary>
internal static class HoverRenameHelper
{
    internal static void ApplyRenameToHoverResult(ref string __result, ItemDrop.ItemData item)
    {
        if (item?.m_shared == null || string.IsNullOrEmpty(__result))
            return;

        if (ItemDisplayService.HasCustomName(item))
        {
            string customName = ItemDisplayService.GetDisplayNameForUi(item, localize: false);
            if (customName == null || item.m_shared.m_name == null)
                return;

            // Replace the default name in the hover text with our rename
            if (Localization.instance == null)
            {
                __result = __result.Replace(item.m_shared.m_name, customName);
                return;
            }

            string localizedOriginalName = Localization.instance.Localize(item.m_shared.m_name);
            string localizedCustomName = Localization.instance.Localize(customName);

            if (__result.Contains(localizedOriginalName))
                __result = __result.Replace(localizedOriginalName, localizedCustomName);
            return;
        }

        if (!DisplayNameModifierHub.AffectsDisplay(item))
            return;

        string locBase = Localization.instance != null
            ? Localization.instance.Localize(item.m_shared.m_name)
            : item.m_shared.m_name;
        if (string.IsNullOrEmpty(locBase))
            return;

        string prefixRaw = DisplayNameModifierHub.GetPrefixRaw(item);
        string locPrefix = Localization.instance != null
            ? Localization.instance.Localize(prefixRaw)
            : prefixRaw;
        string replacement =
            TooltipRichText.EnsureRichTextTagsClosedForTooltip(locPrefix + " " + locBase);

        if (__result.Contains(locBase))
            __result = __result.Replace(locBase, replacement);
        else if (!string.IsNullOrEmpty(item.m_shared.m_name) && __result.Contains(item.m_shared.m_name))
        {
            string repTok = TooltipRichText.EnsureRichTextTagsClosedForTooltip(
                locPrefix + " " + item.m_shared.m_name);
            __result = __result.Replace(item.m_shared.m_name, repTok);
        }
    }
}

/// <summary>Top-left pickup / removed messages use <see cref="ItemDrop.ItemData.m_shared"/>.<see cref="ItemDrop.SharedData.m_name"/>; swap in our custom name when set.</summary>
internal static class PickupHudMessageHelper
{
    internal static bool TryGetLocalizedCustomNameForHud(ItemDrop.ItemData? item, out string localizedName)
    {
        localizedName = "";
        if (item?.m_shared == null)
            return false;
        if (!ItemDisplayService.HasCustomName(item) && !DisplayNameModifierHub.AffectsDisplay(item))
            return false;

        localizedName = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
        if (string.IsNullOrEmpty(localizedName))
            return false;
        return true;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.ShowPickupMessage))]
internal static class CharacterShowPickupMessagePatch
{
    [HarmonyPrefix]
    static bool Prefix(Character __instance, ItemDrop.ItemData item, int amount)
    {
        if (!PickupHudMessageHelper.TryGetLocalizedCustomNameForHud(item, out var nameFragment))
            return true;

        __instance.Message(MessageHud.MessageType.TopLeft, "$msg_added " + nameFragment, amount, item.GetIcon());
        return false;
    }
}

[HarmonyPatch(typeof(Character), nameof(Character.ShowRemovedMessage))]
internal static class CharacterShowRemovedMessagePatch
{
    [HarmonyPrefix]
    static bool Prefix(Character __instance, ItemDrop.ItemData item, int amount)
    {
        if (!PickupHudMessageHelper.TryGetLocalizedCustomNameForHud(item, out var nameFragment))
            return true;

        __instance.Message(MessageHud.MessageType.TopLeft, "$msg_removed " + nameFragment, amount, item.GetIcon());
        return false;
    }
}

public static class Patches
{
    [HarmonyPatch(typeof(ItemDrop))]
    public static class HoverTextPatch
    {
        [HarmonyPatch(nameof(ItemDrop.GetHoverText))]
        [HarmonyPostfix]
        static void FixHoverText(ItemDrop __instance, ref string __result)
        {
            var item = __instance.m_itemData;
            if (item == null) return;
            if (__instance?.m_itemData?.m_shared == null || string.IsNullOrEmpty(__result))
                return;

            HoverRenameHelper.ApplyRenameToHoverResult(ref __result, item);
        }

        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.GetHoverName))]
        [HarmonyPostfix]
        static void FixHoverName(ItemDrop __instance, ref string __result)
        {
            var item = __instance?.m_itemData;
            if (item == null)
                return;

            if (ItemDisplayService.HasCustomName(item))
            {
                var newName = ItemDisplayService.GetDisplayNameForUi(item, localize: false);
                if (!string.IsNullOrEmpty(newName))
                    __result = newName;
            }
            else if (DisplayNameModifierHub.AffectsDisplay(item))
            {
                string prefix = DisplayNameModifierHub.GetPrefixRaw(item);
                string locPrefix = Localization.instance != null
                    ? Localization.instance.Localize(prefix)
                    : prefix;
                __result = TooltipRichText.EnsureRichTextTagsClosedForTooltip(locPrefix + " " + __result);
            }
        }
    }
}

[HarmonyPatch(typeof(ItemStand))]
public static class ItemStandPatch
{
    

    /// <summary>Container-backed stands first, then vanilla attached <see cref="ItemDrop.ItemData"/> (publicized <c>GetAttachedItem</c>).</summary>
    private static ItemDrop.ItemData? TryGetStandOccupantItem(ItemStand stand)
    {
        if (stand == null)
            return null;

        var fromContainer = TryGetFirstContainerItem(stand);
        if (fromContainer?.m_shared != null)
            return fromContainer;

        var getAttached = AccessTools.Method(typeof(ItemStand), nameof(ItemStand.GetAttachedItem), Type.EmptyTypes);
        if (getAttached?.Invoke(stand, null) is ItemDrop.ItemData attached && attached.m_shared != null)
            return attached;

        return null;
    }

    /// <summary>
    /// Clears the cached label when the stand is visually empty. When occupied, only <b>sets</b> the ZDO from
    /// <see cref="ItemDrop.ItemData"/> if Drake rename keys are present on that instance — vanilla's attached
    /// visual item is often a copy without <c>m_customData</c>, and clearing from that would wipe a correct value
    /// written by <see cref="GrabItem"/>.
    /// </summary>
    private static void SyncItemStandRenameZdoFromOccupant(ItemStand stand, ZDO zdo, string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, string.Empty);
            return;
        }

        var occupant = TryGetStandOccupantItem(stand);
        if (occupant?.m_shared == null)
            return;

        if (!ItemDisplayService.HasCustomName(occupant) && !DisplayNameModifierHub.AffectsDisplay(occupant))
            return;

        string display = ItemDisplayService.GetDisplayNameForUi(occupant, localize: false);
        zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, TooltipRichText.EnsureRichTextTagsClosedForTooltip(display));
    }

    private static bool ItemStandHadVisualBeforeUse(ItemStand stand)
    {
        if (stand == null)
            return false;

        // Must use reflection: direct m_visualName access throws FieldAccessException at runtime when the
        // game loads non-publicized ItemStand (compile against publicized, run against vanilla IL).
        var visual = AccessTools.Field(typeof(ItemStand), "m_visualName")?.GetValue(stand) as string;
        return !string.IsNullOrEmpty(visual);
    }

    private static string TryGetStandCustomNameFromZdo(ItemStand stand)
    {
        if (stand == null)
            return "";

        var mNviewField = AccessTools.Field(typeof(ItemStand), "m_nview");
        var nview = mNviewField?.GetValue(stand) as ZNetView;
        var zdo = nview?.GetZDO();
        if (zdo == null)
            return "";

        // Cached display label for item-stand hover / shop-style labels (cleared when the stand is empty or holds a vanilla-named stack).
        string raw = zdo.GetString(DrakeCustomDataKeys.ItemStandHoverName, "");
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string safe = TooltipRichText.EnsureRichTextTagsClosedForTooltip(raw);
        return Localization.instance != null ? Localization.instance.Localize(safe) : safe;
    }

    private static string TryGetStandCurrentItemName(ItemStand stand)
    {
        if (stand == null)
            return "";

        var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
        if (currentItemField == null)
            return "";

        var raw = currentItemField.GetValue(stand) as string;
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string safe = TooltipRichText.EnsureRichTextTagsClosedForTooltip(raw);
        return Localization.instance != null ? Localization.instance.Localize(safe) : safe;
    }

    private static bool HoverTextContainsNoAccess(string hoverText)
    {
        if (string.IsNullOrEmpty(hoverText))
            return false;

        const string token = "$piece_noaccess";
        if (hoverText.Contains(token))
            return true;

        if (Localization.instance == null)
            return false;

        string localized = Localization.instance.Localize(token);
        if (string.IsNullOrEmpty(localized))
            return false;

        return hoverText.IndexOf(localized, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>ZenDragon-style and other container stands: first item in attached <see cref="Container"/>.</summary>
    private static ItemDrop.ItemData? TryGetFirstContainerItem(ItemStand stand)
    {
        var c = stand.GetComponent<Container>();
        if (c == null)
            return null;
        var inv = c.GetInventory();
        if (inv == null)
            return null;
        var items = inv.GetAllItems();
        if (items == null || items.Count == 0)
            return null;
        return items[0];
    }

    private static string TryGetBestStandLabel(ItemStand stand)
    {
        // Preferred: the stand's own current-item name (often what other mods tweak for shop labels).
        string label = TryGetStandCurrentItemName(stand);
        if (!string.IsNullOrEmpty(label))
            return label;

        // Fallback: our own cached name on the ZDO (set when interacting with stands).
        label = TryGetStandCustomNameFromZdo(stand);
        if (!string.IsNullOrEmpty(label))
            return label;

        // Fallback: if ZenItemStands (or similar) turns the stand into a container, use its first item name.
        var item = TryGetFirstContainerItem(stand);
        if (item?.m_shared == null)
            return "";

        return ItemDisplayService.GetDisplayNameForUi(item, localize: true);
    }

    [HarmonyPatch(nameof(ItemStand.GetHoverText))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixItemStandHoverText(ItemStand __instance, ref string __result)
    {
        if (__instance == null || string.IsNullOrEmpty(__result))
            return;

        // If the stand is in a warded/private area, vanilla replaces the interact text with "no access".
        // When enabled, keep "no access" but also show the stand label (item name / shop label) for shop-sign style use.
        if (HoverTextContainsNoAccess(__result))
        {
            if (CustomizeLibsRuntime.ShowItemStandItemNameWhenNoAccess)
            {
                string label = TryGetBestStandLabel(__instance);
                if (!string.IsNullOrEmpty(label) &&
                    __result.IndexOf(label, System.StringComparison.Ordinal) < 0)
                    __result = $"{label}\n{__result}";
            }
            return;
        }

        var item = TryGetFirstContainerItem(__instance);
        if (item?.m_shared == null)
            return;

        if (!ItemDisplayService.HasCustomName(item) && !DisplayNameModifierHub.AffectsDisplay(item))
            return;

        HoverRenameHelper.ApplyRenameToHoverResult(ref __result, item!);
    }

    [HarmonyPatch(nameof(ItemStand.UseItem))]
    [HarmonyPrefix]
    static void UseItem_Prefix(ItemStand __instance, out bool __state)
    {
        // Used with postfix: pickup is "had something mounted" + stack is back in the humanoid's inventory.
        __state = ItemStandHadVisualBeforeUse(__instance);
    }

    [HarmonyPatch(nameof(ItemStand.UseItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void GrabItem(ItemStand __instance, Humanoid user, ItemDrop.ItemData? item, bool __state)
    {
        if (item?.m_shared == null)
            return;

        var nview = AccessTools.Field(typeof(ItemStand), "m_nview")?.GetValue(__instance) as ZNetView;
        var zdo = nview?.GetZDO();
        if (zdo == null)
            return;

        Inventory? inv = user?.GetInventory();
        if (__state && inv != null && inv.ContainsItem(item))
        {
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, string.Empty);
            return;
        }

        string customName = ItemDisplayService.GetDisplayNameForUi(item, localize: false);
        if (ItemDisplayService.HasCustomName(item) || customName != item.m_shared.m_name)
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, TooltipRichText.EnsureRichTextTagsClosedForTooltip(customName));
        else
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, string.Empty);
    }

    [HarmonyPatch(nameof(ItemStand.SetVisualItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixStandText(ItemStand __instance, string itemName, int variant, int quality)
    {
        if (__instance == null)
            return;

        var mNviewField = AccessTools.Field(typeof(ItemStand), "m_nview");
        object? nviewObj = mNviewField?.GetValue(__instance);
        var nview = nviewObj as ZNetView;

        if (nview == null)
            return;

        var zdo = nview.GetZDO();

        if (zdo == null) return;

        SyncItemStandRenameZdoFromOccupant(__instance, zdo, itemName);

        string customName = TooltipRichText.EnsureRichTextTagsClosedForTooltip(zdo.GetString(DrakeCustomDataKeys.ItemStandHoverName, ""));
        if (!string.IsNullOrEmpty(customName))
        {
            var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
            currentItemField?.SetValue(__instance, customName);
        }
    }
}

/// <summary>Rewrites the "dropped" HUD message to use the renamed item display name.</summary>
internal static class DropHudMessagePatches
{
    private static ItemDrop.ItemData? PendingDroppedItem;
    private static float PendingDroppedItemSetAt;
    private const float PendingDroppedItemTtlSeconds = 2.5f;

    /// <summary>
    /// Typed prefix on vanilla <see cref="Humanoid.DropItem(Inventory, ItemDrop.ItemData, int)"/> — Harmony
    /// <c>object[] __args</c> multi-target patches often never run; runtime logs showed zero DropHud events until this.
    /// </summary>
    internal static void ApplyDropItemPendingCapture(Harmony harmony, ManualLogSource log)
    {
        try
        {
            var sig = new[] { typeof(Inventory), typeof(ItemDrop.ItemData), typeof(int) };
            var m = AccessTools.Method(typeof(Humanoid), nameof(Humanoid.DropItem), sig)
                    ?? AccessTools.DeclaredMethod(typeof(Humanoid), nameof(Humanoid.DropItem), sig);

            if (m == null)
            {
                foreach (var cand in typeof(Humanoid).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (cand.Name != nameof(Humanoid.DropItem))
                        continue;
                    var p = cand.GetParameters();
                    if (p.Length == 3 &&
                        p[0].ParameterType == typeof(Inventory) &&
                        p[1].ParameterType == typeof(ItemDrop.ItemData) &&
                        p[2].ParameterType == typeof(int))
                    {
                        m = cand;
                        break;
                    }
                }
            }

            if (m == null)
            {
                log.LogWarning("[DrakesWorkshopLibs] Drop HUD: Humanoid.DropItem(Inventory,ItemData,int) not found.");
                return;
            }

            harmony.Patch(m, prefix: new HarmonyMethod(typeof(DropHudMessagePatches), nameof(HumanoidDropItemTypedPrefix)));
            log.LogInfo("[DrakesWorkshopLibs] Drop HUD: patched typed " + m.DeclaringType?.Name + "." + m.Name);
        }
        catch (Exception ex)
        {
            log.LogError("[DrakesWorkshopLibs] Drop HUD: typed DropItem patch failed: " + ex);
        }
    }

    // Manual-only (applied in <see cref="ApplyDropItemPendingCapture"/>).
    private static void HumanoidDropItemTypedPrefix(Inventory inventory, ItemDrop.ItemData item, int amount)
    {
        PendingDroppedItem = item;
        PendingDroppedItemSetAt = UnityEngine.Time.time;
    }

    internal static void TryRewriteDroppedMessage(ref string msg)
    {
        var item = PendingDroppedItem;
        if (item?.m_shared == null || string.IsNullOrEmpty(msg))
            return;

        // Some builds emit the dropped message after DropItem returns; keep the pending item briefly.
        // If it's too old, drop it so we don't rewrite unrelated messages.
        var age = UnityEngine.Time.time - PendingDroppedItemSetAt;
        if (age > PendingDroppedItemTtlSeconds)
        {
            PendingDroppedItem = null;
            return;
        }

        string droppedToken = "$msg_dropped";
        string droppedLocalized = Localization.instance != null ? Localization.instance.Localize(droppedToken) : droppedToken;
        var hasTok = msg.IndexOf(droppedToken, System.StringComparison.Ordinal) >= 0;
        var hasLoc = msg.IndexOf(droppedLocalized, System.StringComparison.OrdinalIgnoreCase) >= 0;
        bool careAboutDisplay = ItemDisplayService.HasCustomName(item) || DisplayNameModifierHub.AffectsDisplay(item);
        // Some locales / builds show "Dropped …" without leaving $msg_dropped in the final string, or localize differently.
        var looksLikeDroppedLine = careAboutDisplay &&
                                   msg.IndexOf("drop", System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                                   msg.Length < 280;
        if (!hasTok && !hasLoc && !looksLikeDroppedLine)
            return;

        string originalName = Localization.instance != null
            ? Localization.instance.Localize(item.m_shared.m_name)
            : item.m_shared.m_name;

        string displayNameLocalized = ItemDisplayService.GetDisplayNameForUi(item, localize: true);

        if (string.IsNullOrEmpty(displayNameLocalized))
            return;

        // Vanilla Humanoid.DropItem passes "$msg_dropped " + m_shared.m_name (token) — replace whole tail in one shot.
        const string dropPrefix = "$msg_dropped ";
        if (careAboutDisplay &&
            msg.StartsWith(dropPrefix, StringComparison.Ordinal) &&
            !string.IsNullOrEmpty(item.m_shared.m_name))
        {
            var tail = msg.Substring(dropPrefix.Length).TrimStart();
            if (string.Equals(tail, item.m_shared.m_name, StringComparison.Ordinal))
            {
                msg = dropPrefix + displayNameLocalized;
                PendingDroppedItem = null;
                return;
            }
        }

        // Replace first occurrence only; use regex so matched span length can differ from needle (case / edge cases).
        if (!string.IsNullOrEmpty(originalName))
        {
            try
            {
                var rx = new Regex(Regex.Escape(originalName), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
                if (rx.IsMatch(msg))
                {
                    msg = rx.Replace(msg, _ => displayNameLocalized, 1);
                    PendingDroppedItem = null;
                    return;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                /* ignore */
            }
        }

        // Fallback: token id on the item (not always equal to localized segment in the HUD line).
        if (!string.IsNullOrEmpty(item.m_shared.m_name))
        {
            try
            {
                var rx2 = new Regex(Regex.Escape(item.m_shared.m_name), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                    TimeSpan.FromMilliseconds(250));
                if (rx2.IsMatch(msg))
                {
                    msg = rx2.Replace(msg, _ => displayNameLocalized, 1);
                    PendingDroppedItem = null;
                    return;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                /* ignore */
            }
        }

        // Append, when the dropped message has no inline name.
        if (string.Equals(msg, droppedToken, System.StringComparison.Ordinal) ||
            string.Equals(msg, droppedLocalized, System.StringComparison.OrdinalIgnoreCase))
        {
            msg = string.Equals(msg, droppedToken, System.StringComparison.Ordinal)
                ? $"{droppedToken} {displayNameLocalized}"
                : $"{droppedLocalized} {displayNameLocalized}";
            PendingDroppedItem = null;
        }
    }

    /// <summary>
    /// Patch only the <see cref="MessageHud.ShowMessage"/> overload that carries the HUD text as a <see cref="string"/>.
    /// A broad multi-target patch with <c>ref string text</c> breaks <see cref="Harmony.PatchAll"/> when overloads do not match.
    /// </summary>
    internal static void ApplyMessageHudShowMessage(Harmony harmony, ManualLogSource log)
    {
        try
        {
            var hudType = typeof(MessageHud);
            var msgEnum = AccessTools.Inner(hudType, "MessageType")
                ?? hudType.GetNestedType("MessageType", BindingFlags.Public | BindingFlags.NonPublic);
            if (msgEnum == null)
            {
                log.LogWarning("[DrakesWorkshopLibs] Drop HUD: MessageHud.MessageType nested type not found.");
                return;
            }

            MethodBase? best = null;
            foreach (var m in hudType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (m.Name != "ShowMessage")
                    continue;
                var p = m.GetParameters();
                if (p.Length < 2)
                    continue;
                if (p[0].ParameterType != msgEnum || p[1].ParameterType != typeof(string))
                    continue;

                // Prefer the same shape as Character.Message (type, text, amount, sprite).
                if (p.Length == 4 &&
                    p[2].ParameterType == typeof(int) &&
                    p[3].ParameterType == typeof(UnityEngine.Sprite))
                {
                    best = m;
                    break;
                }

                best ??= m;
            }

            if (best == null)
            {
                log.LogWarning("[DrakesWorkshopLibs] Drop HUD: no suitable MessageHud.ShowMessage overload found.");
                return;
            }

            int stringArgIndex = -1;
            var ps = best.GetParameters();
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].ParameterType == typeof(string))
                {
                    stringArgIndex = i;
                    break;
                }
            }

            if (stringArgIndex < 0)
            {
                log.LogWarning("[DrakesWorkshopLibs] Drop HUD: ShowMessage overload has no string parameter: " + best);
                return;
            }

            var prefix = stringArgIndex == 0
                ? new HarmonyMethod(typeof(DropHudMessagePatches), nameof(MessageHudShowMessageStringArg0))
                : new HarmonyMethod(typeof(DropHudMessagePatches), nameof(MessageHudShowMessageStringArg1));

            harmony.Patch(best, prefix: prefix);
            log.LogInfo("[DrakesWorkshopLibs] Drop HUD: patched MessageHud." + best.Name + " stringArg=" + stringArgIndex + " :: " + best);
        }
        catch (Exception ex)
        {
            log.LogError("[DrakesWorkshopLibs] Drop HUD: MessageHud patch failed: " + ex);
        }
    }

    // Manual-only MessageHud prefixes (applied in <see cref="ApplyMessageHudShowMessage"/>).
    private static void MessageHudShowMessageStringArg0(ref string __0)
    {
        TryRewriteDroppedMessage(ref __0);
    }

    private static void MessageHudShowMessageStringArg1(ref string __1)
    {
        TryRewriteDroppedMessage(ref __1);
    }

    // Character.Message — kept for non-Player characters / mods that call the base implementation.
    [HarmonyPatch(typeof(Character), nameof(Character.Message), new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite) })]
    [HarmonyPrefix]
    private static void CharacterMessagePrefix(MessageHud.MessageType type, ref string msg)
    {
        TryRewriteDroppedMessage(ref msg);
    }

    /// <summary>
    /// Local <see cref="Player"/> overrides <see cref="Character.Message"/> and forwards straight to <see cref="MessageHud.instance.ShowMessage"/>,
    /// so drop notifications never hit the <see cref="Character.Message"/> patch. Pickup works because we patch <see cref="Character.ShowPickupMessage"/> instead.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Message), new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite) })]
    [HarmonyPrefix]
    private static void PlayerMessagePrefix(MessageHud.MessageType type, ref string msg)
    {
        TryRewriteDroppedMessage(ref msg);
    }
}
