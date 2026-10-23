using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using DrakeModsLibs.Input;

namespace DrakeModsLibs.Input;

public sealed class MenuBindingRegistration
{
    internal MenuBindingRegistration(string id, string scope, int priority, Func<string?> getBindingString, string modLabel)
    {
        Id = id;
        Scope = scope;
        Priority = priority;
        GetBindingString = getBindingString;
        ModLabel = modLabel;
    }

    public string Id { get; }
    public string Scope { get; }
    public int Priority { get; }
    public Func<string?> GetBindingString { get; }
    public string ModLabel { get; }
}

public static class MenuBindingRegistry
{
    public const string InventoryContextScope = "inventory.context";

    static readonly List<MenuBindingRegistration> Registrations = new();
    static readonly HashSet<string> LoggedConflicts = new();
    static ManualLogSource? Log;

    internal static void SetLogger(ManualLogSource log) => Log = log;

    public static void Register(string id, string scope, int priority, Func<string?> getBindingString, string modLabel)
    {
        var reg = new MenuBindingRegistration(id, scope, priority, getBindingString, modLabel);
        Registrations.Add(reg);
        WarnOnBindingConflict(reg);
    }

    static readonly Dictionary<string, HashSet<string>> LinkedTabs = new(StringComparer.Ordinal);

    /// <summary>
    /// Tie a mod's own menu binding to its tab(s). When the shared Libs modifier opens the tab host, a linked tab is left out
    /// unless its own binding is held (or uses the same keys as the shared one), so changing that mod's key really changes it.
    /// </summary>
    public static void LinkTab(string bindingId, string tabId)
    {
        if (string.IsNullOrEmpty(bindingId) || string.IsNullOrEmpty(tabId))
            return;
        if (!LinkedTabs.TryGetValue(bindingId, out var set))
            LinkedTabs[bindingId] = set = new HashSet<string>(StringComparer.Ordinal);
        set.Add(tabId);
    }

    /// <summary>Tabs whose own binding in <paramref name="scope"/> differs from <paramref name="sharedBindingId"/>'s keys and is not held now.</summary>
    public static HashSet<string> TabsWaitingForOwnKey(string scope, string sharedBindingId)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var shared = Registrations.FirstOrDefault(r => r.Scope == scope && r.Id == sharedBindingId);
        var sharedKeys = Normalize(shared?.GetBindingString());
        foreach (var reg in Registrations)
        {
            if (reg.Scope != scope || reg.Id == sharedBindingId || !LinkedTabs.TryGetValue(reg.Id, out var tabs))
                continue;
            if (Normalize(reg.GetBindingString()) == sharedKeys || MenuKeyBinding.IsHeld(reg.GetBindingString()))
                continue;
            result.UnionWith(tabs);
        }

        return result;
    }

    public static bool IsHeld(string scope, string id)
    {
        var reg = Registrations.FirstOrDefault(r => r.Scope == scope && r.Id == id);
        return reg != null && MenuKeyBinding.IsHeld(reg.GetBindingString());
    }

    public static string? GetActiveBindingId(string scope)
    {
        foreach (var reg in Registrations.Where(r => r.Scope == scope).OrderByDescending(r => r.Priority))
        {
            if (MenuKeyBinding.IsHeld(reg.GetBindingString()))
                return reg.Id;
        }
        return null;
    }

    static void WarnOnBindingConflict(MenuBindingRegistration added)
    {
        var newBinding = Normalize(added.GetBindingString());
        if (string.IsNullOrEmpty(newBinding))
            return;

        foreach (var existing in Registrations)
        {
            if (existing.Id == added.Id)
                continue;
            if (existing.Scope != added.Scope)
                continue;
            if (Normalize(existing.GetBindingString()) != newBinding)
                continue;

            var key = existing.Id + "|" + added.Id + "|" + newBinding;
            if (!LoggedConflicts.Add(key))
                continue;
            Log?.LogWarning(
                $"[DrakeModsLibs] Menu binding conflict in scope '{added.Scope}': " +
                $"{existing.ModLabel} ({existing.Id}) and {added.ModLabel} ({added.Id}) both use '{newBinding}'.");
        }
    }

    static string Normalize(string? binding)
    {
        if (string.IsNullOrWhiteSpace(binding))
            return "";
        return binding!.Trim().ToLowerInvariant();
    }
}
