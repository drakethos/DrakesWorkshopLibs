using System;

namespace DrakeModsLibs.Tags;

/// <summary>
/// Matches an item against a token the same way RenameIt exclusions do:
/// localization token, drop prefab name, or localized display name.
/// </summary>
internal static class ItemNameMatch
{
    public static bool Matches(ItemDrop.ItemData? item, string? token)
    {
        if (item?.m_shared == null || string.IsNullOrWhiteSpace(token))
            return false;

        token = token!.Trim();
        string internalName = item.m_shared.m_name;
        if (string.IsNullOrEmpty(internalName))
            return false;

        if (internalName.Equals(token, StringComparison.OrdinalIgnoreCase))
            return true;

        if (item.m_dropPrefab != null &&
            !string.IsNullOrEmpty(item.m_dropPrefab.name) &&
            item.m_dropPrefab.name.Equals(token, StringComparison.OrdinalIgnoreCase))
            return true;

        if (Localization.instance != null)
        {
            string localized = Localization.instance.Localize(internalName);
            if (!string.IsNullOrEmpty(localized) &&
                localized.Equals(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
