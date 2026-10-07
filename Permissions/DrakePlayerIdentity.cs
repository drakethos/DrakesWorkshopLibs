using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Jotunn.Managers;

namespace DrakeModsLibs.Permissions;

/// <summary>Valheim admin check and VIP identity keys (same rules as RenameIt's RenameitPermission).</summary>
public static class DrakePlayerIdentity
{
    // ZNet.m_adminList / ListContainsId are private in the real game: reflection only (direct access throws at runtime).
    static readonly FieldInfo? AdminListField = AccessTools.Field(typeof(ZNet), "m_adminList");
    static readonly MethodInfo? ListContainsIdMethod = AccessTools.Method(typeof(ZNet), "ListContainsId");

    /// <summary>
    /// Valheim server admin. Local player: Jotunn's synced <c>PlayerIsAdmin</c>. Remote player (server side):
    /// the peer's host/Steam ID against the server admin list.
    /// </summary>
    public static bool IsValheimAdmin(Player? player)
    {
        if (player == null || ZNet.instance == null)
            return false;
        if (player == Player.m_localPlayer)
            return SynchronizationManager.Instance != null && SynchronizationManager.Instance.PlayerIsAdmin;
        try
        {
            var hostId = PeerHostId(player);
            var adminList = AdminListField?.GetValue(ZNet.instance);
            return adminList != null
                   && !string.IsNullOrEmpty(hostId)
                   && ListContainsIdMethod?.Invoke(ZNet.instance, new[] { adminList, hostId }) is true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Character name, player ID, and (when known) platform host ID — any may appear on a VIP list.</summary>
    public static IEnumerable<string> Keys(Player player)
    {
        var name = player.GetPlayerName();
        if (!string.IsNullOrEmpty(name))
            yield return name;
        yield return player.GetPlayerID().ToString();
        var hostId = PeerHostId(player);
        if (!string.IsNullOrEmpty(hostId))
            yield return hostId!;
    }

    static string? PeerHostId(Player player)
    {
        if (ZNet.instance == null)
            return null;
        try
        {
            var zid = player.GetZDOID();
            foreach (var peer in ZNet.instance.GetPeers())
            {
                if (!peer.IsReady() || peer.m_characterID != zid)
                    continue;
                var hostId = peer.m_socket?.GetHostName();
                return string.IsNullOrEmpty(hostId) ? null : hostId;
            }
        }
        catch
        {
            // ignored
        }
        return null;
    }
}
