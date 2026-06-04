using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using DrakesWorkshopLibs.Input;

namespace DrakesWorkshopLibs.Input;

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
                $"[DrakesWorkshopLibs] Menu binding conflict in scope '{added.Scope}': " +
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
