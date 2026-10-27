using System;
using System.Collections.Generic;

namespace DrakeModsLibs.API;

/// <summary>
/// A mod that gives its own pieces a Shift+Use (alt-interact) menu registers a claim, so customization mods that also want Shift+Use on
/// pieces (RenameIt's piece rename, and others later) leave those pieces alone. Dormant: a static list, no patches. Any mod can register
/// without a hard dependency by calling <see cref="Register"/> through reflection when DrakeModsLibs is loaded (RenameIt needs Libs, so the
/// conflict can only happen when Libs is there).
/// </summary>
public static class PieceInteractionClaims
{
    static readonly List<(string owner, Func<Piece, bool> claims)> Claims = new();

    /// <summary>Claim pieces for the owner mod. <paramref name="claims"/> must be cheap (it runs while hovering and on Use). A second call with the same owner replaces the first.</summary>
    public static void Register(string owner, Func<Piece, bool> claims)
    {
        if (string.IsNullOrWhiteSpace(owner) || claims == null)
            return;
        Claims.RemoveAll(c => string.Equals(c.owner, owner, StringComparison.OrdinalIgnoreCase));
        Claims.Add((owner, claims));
    }

    /// <summary>True when some mod has claimed this piece's Shift+Use. Customization mods skip claimed pieces.</summary>
    public static bool IsClaimed(Piece? piece) => ClaimedBy(piece) != null;

    /// <summary>The owner that claimed this piece, or null.</summary>
    public static string? ClaimedBy(Piece? piece)
    {
        if (piece == null)
            return null;
        foreach (var (owner, claims) in Claims)
        {
            try
            {
                if (claims(piece))
                    return owner;
            }
            catch
            {
                // a broken claim must never break hovering or interacting
            }
        }
        return null;
    }
}
