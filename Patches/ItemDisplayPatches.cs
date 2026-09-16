using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using DrakeModsLibs.Data;
using DrakeModsLibs.Display;
using DrakeModsLibs.Runtime;
using HarmonyLib;



namespace DrakeModsLibs.Patches;

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

/// <summary>
/// Valheim 1.0+: <c>Character.Message(type, msg, amount, icon, log)</c>.
/// Older publicized refs (CI) only expose the 4-arg overload — bind at runtime.
/// </summary>
internal static class CharacterMessageInvoker
{
    private static readonly MethodInfo? Message5 = AccessTools.Method(
        typeof(Character),
        nameof(Character.Message),
        new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite), typeof(bool) });

    private static readonly MethodInfo? Message4 = AccessTools.Method(
        typeof(Character),
        nameof(Character.Message),
        new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite) });

    internal static void Show(
        Character character,
        MessageHud.MessageType type,
        string msg,
        int amount,
        UnityEngine.Sprite? icon)
    {
        if (!character)
            return;

        try
        {
            if (Message5 != null)
            {
                Message5.Invoke(character, new object?[] { type, msg, amount, icon, false });
                return;
            }

            Message4?.Invoke(character, new object?[] { type, msg, amount, icon });
        }
        catch (Exception)
        {
            /* never break pickup/remove HUD */
        }
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

        CharacterMessageInvoker.Show(
            __instance,
            MessageHud.MessageType.TopLeft,
            "$msg_added " + nameFragment,
            amount,
            item.GetIcon());
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

        CharacterMessageInvoker.Show(
            __instance,
            MessageHud.MessageType.TopLeft,
            "$msg_removed " + nameFragment,
            amount,
            item.GetIcon());
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
    /// <summary>
    /// Recomputes <c>m_currentItemName</c> on all loaded stands from each stand's rename ZDO cache + live
    /// durability prefix. Does not mutate <see cref="ItemDrop.ItemData"/> / ObjectDB prefabs.
    /// </summary>
    internal static void RefreshAllItemStandDisplayNames()
    {
        ItemStand[] stands;
        try
        {
            stands = UnityEngine.Object.FindObjectsByType<ItemStand>(UnityEngine.FindObjectsSortMode.None);
        }
        catch
        {
            try
            {
#pragma warning disable CS0618
                stands = UnityEngine.Object.FindObjectsOfType<ItemStand>();
#pragma warning restore CS0618
            }
            catch
            {
                return;
            }
        }

        if (stands == null || stands.Length == 0)
            return;

        foreach (var stand in stands)
        {
            if (stand == null)
                continue;
            try
            {
                ApplyLiveDisplayNameToStand(stand);
            }
            catch
            {
                /* ignore per-stand failures */
            }
        }
    }

    private static ZDO? TryGetStandZdo(ItemStand stand)
    {
        var nview = AccessTools.Field(typeof(ItemStand), "m_nview")?.GetValue(stand) as ZNetView;
        return nview?.GetZDO();
    }

    /// <summary>
    /// Valheim 1.0 returns a prefab hash. Older publicized refs (CI) return a prefab name string.
    /// Bind at runtime so both compile against the older reference assemblies.
    /// </summary>
    private static int TryGetAttachedPrefabHash(ItemStand stand)
    {
        if (stand == null)
            return 0;

        var method = AccessTools.Method(typeof(ItemStand), nameof(ItemStand.GetAttachedItem));
        if (method == null)
            return 0;

        object? value;
        try
        {
            value = method.Invoke(stand, null);
        }
        catch
        {
            return 0;
        }

        if (value is int hash)
            return hash;
        if (value is string name && int.TryParse(name, out var parsed))
            return parsed;
        return 0;
    }

    /// <summary>
    /// Valheim 1.0 stores stand item bytes under ZDOVars.s_itemData (hashed int key).
    /// That field is missing from older reference assemblies used by CI.
    /// </summary>
    private static byte[]? TryGetStandItemBytes(ZDO zdo)
    {
        var field = AccessTools.Field(typeof(ZDOVars), "s_itemData");
        if (field != null)
        {
            var key = field.GetValue(null);
            if (key is int hashed)
                return zdo.GetByteArray(hashed, (byte[]?)null);
            if (key is string name && !string.IsNullOrEmpty(name))
                return zdo.GetByteArray(name, (byte[]?)null);
        }

        return zdo.GetByteArray("-1_itemData", (byte[]?)null);
    }

    /// <summary>
    /// 1.0 signature is LoadFromZDO(item, zdo, index). Older refs use (index, item, zdo) or (item, zdo).
    /// </summary>
    private static bool TryLoadItemFromZdo(ItemDrop.ItemData item, ZDO zdo, int index)
    {
        const string methodName = "LoadFromZDO";
        var methods = typeof(ItemDrop).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        var modern = methods.FirstOrDefault(m =>
        {
            if (m.Name != methodName)
                return false;
            var p = m.GetParameters();
            return p.Length == 3
                && p[0].ParameterType == typeof(ItemDrop.ItemData)
                && p[1].ParameterType == typeof(ZDO)
                && p[2].ParameterType == typeof(int);
        });
        if (modern != null)
        {
            modern.Invoke(null, new object[] { item, zdo, index });
            return true;
        }

        var legacyIndexed = methods.FirstOrDefault(m =>
        {
            if (m.Name != methodName)
                return false;
            var p = m.GetParameters();
            return p.Length == 3
                && p[0].ParameterType == typeof(int)
                && p[1].ParameterType == typeof(ItemDrop.ItemData)
                && p[2].ParameterType == typeof(ZDO);
        });
        if (legacyIndexed != null)
        {
            legacyIndexed.Invoke(null, new object[] { index, item, zdo });
            return true;
        }

        var legacy = methods.FirstOrDefault(m =>
        {
            if (m.Name != methodName)
                return false;
            var p = m.GetParameters();
            return p.Length == 2
                && p[0].ParameterType == typeof(ItemDrop.ItemData)
                && p[1].ParameterType == typeof(ZDO);
        });
        if (legacy == null)
            return false;

        legacy.Invoke(null, new object[] { item, zdo });
        return true;
    }

    /// <summary>
    /// Vanilla attach path calls SaveToZDO with index -1. 1.0 stores those bytes under ZDOVars.s_itemData.
    /// </summary>
    private const int StandItemDataZdoIndex = -1;

    /// <summary>
    /// Real attached item (durability + custom data) from the stand ZDO into a deep <see cref="ItemDrop.ItemData.Clone"/>.
    /// Never mutates ObjectDB prefabs. Falls back to container occupant when there is no vanilla attachment.
    /// </summary>
    private static ItemDrop.ItemData? TryGetStandItemForDisplay(ItemStand stand, out bool loadedInstance)
    {
        loadedInstance = false;
        if (stand == null)
            return null;

        int hash = 0;
        try
        {
            hash = TryGetAttachedPrefabHash(stand);
        }
        catch
        {
            /* ignore */
        }

        // Prefer vanilla attachment over any Container on the same piece.
        if (hash != 0 && ObjectDB.instance != null)
        {
            var prefab = ObjectDB.instance.GetItemPrefab(hash);
            var prefabData = prefab != null ? prefab.GetComponent<ItemDrop>()?.m_itemData : null;
            if (prefabData == null)
                return null;

            var clone = prefabData.Clone();
            var zdo = TryGetStandZdo(stand);
            if (zdo == null)
                return clone;

            try
            {
                // Index -1 => ZDOVars.s_itemData on 1.0 (see ItemDrop.LoadFromZDO / SaveToZDO).
                var bytes = TryGetStandItemBytes(zdo);
                if (bytes != null && bytes.Length > 2 && TryLoadItemFromZdo(clone, zdo, StandItemDataZdoIndex))
                    loadedInstance = true;
            }
            catch
            {
                /* keep prefab clone defaults */
            }

            return clone;
        }

        var container = TryGetFirstContainerItem(stand);
        if (container?.m_shared != null)
        {
            loadedInstance = true;
            return container;
        }

        return null;
    }

    /// <summary>Sets <c>m_currentItemName</c> from a known item instance (place path — ZDO may not be saved yet).</summary>
    private static void ApplyDisplayNameFromItemInstance(ItemStand stand, ItemDrop.ItemData item)
    {
        if (stand == null || item?.m_shared == null)
            return;

        var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
        if (currentItemField == null)
            return;

        if (ItemDisplayService.HasCustomName(item) || DisplayNameModifierHub.AffectsDisplay(item))
        {
            string display = ItemDisplayService.GetDisplayNameForUi(item, localize: false);
            currentItemField.SetValue(stand, TooltipRichText.EnsureRichTextTagsClosedForTooltip(display));
        }
        else
        {
            currentItemField.SetValue(stand, item.m_shared.m_name);
        }
    }

    /// <summary>
    /// Base label for the stand: rename from loaded item / rename-only ZDO cache / vanilla shared name.
    /// </summary>
    private static string ResolveStandBaseLabel(ItemStand stand, ZDO? zdo, ItemDrop.ItemData? item)
    {
        if (item != null && ItemDisplayService.HasCustomName(item))
            return ItemDisplayService.GetProperName(item);

        string cached = zdo != null ? zdo.GetString(DrakeCustomDataKeys.ItemStandHoverName, "") : "";
        if (!string.IsNullOrWhiteSpace(cached))
            return StripLegacyDurabilityPrefix(item, cached.Trim());

        if (item?.m_shared != null && !string.IsNullOrEmpty(item.m_shared.m_name))
            return item.m_shared.m_name;

        var current = AccessTools.Field(typeof(ItemStand), "m_currentItemName")?.GetValue(stand) as string;
        return string.IsNullOrWhiteSpace(current) ? "" : current.Trim();
    }

    /// <summary>
    /// Live display for <c>m_currentItemName</c>: real stand-item durability + rename. Clone-only loads — no prefab writes.
    /// </summary>
    private static void ApplyLiveDisplayNameToStand(ItemStand stand)
    {
        if (stand == null)
            return;

        var item = TryGetStandItemForDisplay(stand, out var loadedInstance);
        if (item?.m_shared == null)
            return;

        var zdo = TryGetStandZdo(stand);
        string baseLabel = ResolveStandBaseLabel(stand, zdo, item);
        if (string.IsNullOrEmpty(baseLabel))
            return;

        // Keep ZDO as rename-only (migrate off baked "Pristine/Worn ..." from older builds).
        if (zdo != null)
        {
            if (ItemDisplayService.HasCustomName(item))
            {
                string proper = ItemDisplayService.GetProperName(item);
                zdo.Set(DrakeCustomDataKeys.ItemStandHoverName,
                    TooltipRichText.EnsureRichTextTagsClosedForTooltip(proper));
            }
            else
            {
                string rawCache = zdo.GetString(DrakeCustomDataKeys.ItemStandHoverName, "");
                if (!string.IsNullOrWhiteSpace(rawCache))
                {
                    string stripped = StripLegacyDurabilityPrefix(item, rawCache.Trim());
                    if (!string.Equals(stripped, rawCache.Trim(), StringComparison.Ordinal))
                        zdo.Set(DrakeCustomDataKeys.ItemStandHoverName,
                            TooltipRichText.EnsureRichTextTagsClosedForTooltip(stripped));
                }
                else if (loadedInstance)
                {
                    zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, string.Empty);
                }
            }
        }

        // Prefer full live pipeline when we have a real instance (correct durability + rename).
        string display;
        if (loadedInstance &&
            (ItemDisplayService.HasCustomName(item) || DisplayNameModifierHub.AffectsDisplay(item)))
        {
            display = ItemDisplayService.GetDisplayNameForUi(item, localize: false);
        }
        else
        {
            display = baseLabel;
            if (DisplayNameModifierHub.AffectsDisplay(item))
            {
                string prefix = DisplayNameModifierHub.GetPrefixRaw(item);
                if (!string.IsNullOrEmpty(prefix))
                {
                    string strippedBase = StripLegacyDurabilityPrefix(item, baseLabel);
                    display = prefix.TrimEnd() + " " + strippedBase;
                }
            }
        }

        var currentItemField = AccessTools.Field(typeof(ItemStand), "m_currentItemName");
        currentItemField?.SetValue(stand, TooltipRichText.EnsureRichTextTagsClosedForTooltip(display));
    }

    /// <summary>
    /// Older builds baked durability into the stand ZDO (e.g. "Worn MySword"). Strip known prefixes so live
    /// re-apply does not become "Worn Worn ...".
    /// </summary>
    private static string StripLegacyDurabilityPrefix(ItemDrop.ItemData? item, string cached)
    {
        if (string.IsNullOrEmpty(cached))
            return cached;

        // Strip whatever the live modifier would prepend right now.
        if (item != null)
        {
            string prefix = DisplayNameModifierHub.GetPrefixRaw(item);
            cached = StripOnePrefix(cached, prefix);
        }

        // Also strip common configured labels when the modifier is currently off (disable path).
        cached = StripOnePrefix(cached, "Pristine");
        cached = StripOnePrefix(cached, "Worn");
        cached = StripOnePrefix(cached, "Rusty");
        cached = StripOnePrefix(cached, "Tarnished");
        cached = StripOnePrefix(cached, "Broken");
        return cached;
    }

    private static string StripOnePrefix(string cached, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix) || string.IsNullOrEmpty(cached))
            return cached;

        foreach (var candidate in UniquePrefixCandidates(prefix))
        {
            string withSpace = candidate.TrimEnd() + " ";
            if (cached.StartsWith(withSpace, StringComparison.OrdinalIgnoreCase))
                return cached.Substring(withSpace.Length).TrimStart();
        }

        return cached;
    }

    private static List<string> UniquePrefixCandidates(string prefix)
    {
        var list = new List<string>();
        void consider(string? s)
        {
            if (string.IsNullOrWhiteSpace(s))
                return;
            s = s.Trim();
            if (list.Exists(x => string.Equals(x, s, StringComparison.Ordinal)))
                return;
            list.Add(s);
            if (s.IndexOf('<') >= 0)
            {
                string plain = Regex.Replace(s, "<[^>]+>", "").Trim();
                if (plain.Length > 0 && !list.Exists(x => string.Equals(x, plain, StringComparison.Ordinal)))
                    list.Add(plain);
            }
        }

        consider(prefix);
        if (Localization.instance != null)
            consider(Localization.instance.Localize(prefix));
        return list;
    }

    private static bool ItemStandHadVisualBeforeUse(ItemStand stand)
    {
        if (stand == null)
            return false;

        // Valheim 1.0+: prefer HaveAttachment / hash. Fall back to m_visualName for older builds.
        try
        {
            if (stand.HaveAttachment())
                return true;
        }
        catch
        {
            /* ignore */
        }

        var visual = AccessTools.Field(typeof(ItemStand), "m_visualName")?.GetValue(stand) as string;
        return !string.IsNullOrEmpty(visual);
    }

    private static string TryGetStandCustomNameFromZdo(ItemStand stand)
    {
        if (stand == null)
            return "";

        var zdo = TryGetStandZdo(stand);
        if (zdo == null)
            return "";

        var item = TryGetStandItemForDisplay(stand, out var loadedInstance);
        if (item != null && loadedInstance &&
            (ItemDisplayService.HasCustomName(item) || DisplayNameModifierHub.AffectsDisplay(item)))
        {
            string live = ItemDisplayService.GetDisplayNameForUi(item, localize: true);
            if (!string.IsNullOrEmpty(live))
                return live;
        }

        // Cached rename (or legacy display) for ward / shop-style labels.
        string raw = zdo.GetString(DrakeCustomDataKeys.ItemStandHoverName, "");
        if (string.IsNullOrWhiteSpace(raw))
            return "";

        string baseLabel = StripLegacyDurabilityPrefix(item, raw.Trim());
        string display = baseLabel;
        if (item != null && DisplayNameModifierHub.AffectsDisplay(item))
        {
            string prefix = DisplayNameModifierHub.GetPrefixRaw(item);
            if (!string.IsNullOrEmpty(prefix))
                display = prefix.TrimEnd() + " " + baseLabel;
        }

        string safe = TooltipRichText.EnsureRichTextTagsClosedForTooltip(display);
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

        // Container-only stands (no vanilla attachment hash): rewrite hover from the real inventory item.
        // Vanilla attachment labels come from m_currentItemName (updated live) â€” do not re-apply durability here
        // or names become "Worn Worn â€¦".
        try
        {
            if (TryGetAttachedPrefabHash(__instance) != 0)
                return;
        }
        catch
        {
            /* fall through to container path */
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

        var zdo = TryGetStandZdo(__instance);
        if (zdo == null)
            return;

        Inventory? inv = user?.GetInventory();
        if (__state && inv != null && inv.ContainsItem(item))
        {
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, string.Empty);
            return;
        }

        // Cache rename text only — never bake durability into the ZDO.
        if (ItemDisplayService.HasCustomName(item))
        {
            string proper = ItemDisplayService.GetProperName(item);
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName,
                TooltipRichText.EnsureRichTextTagsClosedForTooltip(proper));
        }
        else
            zdo.Set(DrakeCustomDataKeys.ItemStandHoverName, string.Empty);

        // UseItem queues attach; ZDO payload may not exist yet. Label from the real item instance now;
        // SetVisualItem postfix reloads from ZDOVars.s_itemData once UpdateAttach has saved.
        ApplyDisplayNameFromItemInstance(__instance, item);
    }

    // Valheim 1.0+: SetVisualItem(int itemHash, int variant, int quality, int orientation)
    [HarmonyPatch(nameof(ItemStand.SetVisualItem))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void FixStandText(ItemStand __instance, int itemHash, int variant, int quality, int orientation)
    {
        if (__instance == null)
            return;

        ApplyLiveDisplayNameToStand(__instance);
    }
}

/// <summary>Rewrites the "dropped" HUD message to use the renamed item display name.</summary>
internal static class DropHudMessagePatches
{
    private static ItemDrop.ItemData? PendingDroppedItem;
    private static float PendingDroppedItemSetAt;
    private const float PendingDroppedItemTtlSeconds = 2.5f;

    /// <summary>
    /// Typed prefix on vanilla <see cref="Humanoid.DropItem(Inventory, ItemDrop.ItemData, int)"/> â€” Harmony
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
                log.LogWarning("[DrakeModsLibs] Drop HUD: Humanoid.DropItem(Inventory,ItemData,int) not found.");
                return;
            }

            harmony.Patch(m, prefix: new HarmonyMethod(typeof(DropHudMessagePatches), nameof(HumanoidDropItemTypedPrefix)));
            log.LogInfo("[DrakeModsLibs] Drop HUD: patched typed " + m.DeclaringType?.Name + "." + m.Name);
        }
        catch (Exception ex)
        {
            log.LogError("[DrakeModsLibs] Drop HUD: typed DropItem patch failed: " + ex);
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
        // Some locales / builds show "Dropped â€¦" without leaving $msg_dropped in the final string, or localize differently.
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

        // Vanilla Humanoid.DropItem passes "$msg_dropped " + m_shared.m_name (token) â€” replace whole tail in one shot.
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
                log.LogWarning("[DrakeModsLibs] Drop HUD: MessageHud.MessageType nested type not found.");
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
                log.LogWarning("[DrakeModsLibs] Drop HUD: no suitable MessageHud.ShowMessage overload found.");
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
                log.LogWarning("[DrakeModsLibs] Drop HUD: ShowMessage overload has no string parameter: " + best);
                return;
            }

            var prefix = stringArgIndex == 0
                ? new HarmonyMethod(typeof(DropHudMessagePatches), nameof(MessageHudShowMessageStringArg0))
                : new HarmonyMethod(typeof(DropHudMessagePatches), nameof(MessageHudShowMessageStringArg1));

            harmony.Patch(best, prefix: prefix);
            log.LogInfo("[DrakeModsLibs] Drop HUD: patched MessageHud." + best.Name + " stringArg=" + stringArgIndex + " :: " + best);
        }
        catch (Exception ex)
        {
            log.LogError("[DrakeModsLibs] Drop HUD: MessageHud patch failed: " + ex);
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
    // Valheim 1.0+: Message(type, msg, amount, icon, log).
    [HarmonyPatch(typeof(Character), nameof(Character.Message), new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite), typeof(bool) })]
    [HarmonyPrefix]
    private static void CharacterMessagePrefix(MessageHud.MessageType type, ref string msg)
    {
        TryRewriteDroppedMessage(ref msg);
    }

    /// <summary>
    /// Local <see cref="Player"/> overrides <see cref="Character.Message"/> and forwards straight to <see cref="MessageHud.instance.ShowMessage"/>,
    /// so drop notifications never hit the <see cref="Character.Message"/> patch. Pickup works because we patch <see cref="Character.ShowPickupMessage"/> instead.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.Message), new[] { typeof(MessageHud.MessageType), typeof(string), typeof(int), typeof(UnityEngine.Sprite), typeof(bool) })]
    [HarmonyPrefix]
    private static void PlayerMessagePrefix(MessageHud.MessageType type, ref string msg)
    {
        TryRewriteDroppedMessage(ref msg);
    }
}
