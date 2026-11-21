using BepInEx;
using DrakeModsLibs.Defense;
using DrakeModsLibs.Input;
using DrakeModsLibs.Integration;
using HarmonyLib;
using Jotunn.Managers;

namespace DrakeModsLibs;

[BepInPlugin(GUID, ModName, Version)]
public partial class CustomizeLibsPlugin : BaseUnityPlugin
{

    private readonly Harmony _harmony = new("drakemods.DrakeModsLibs");

    private void Awake()
    {
        var guard = ModGuard.Create(ModName, GUID, Version, typeof(CustomizeLibsPlugin).Assembly, _harmony, Logger);
        guard.ReportTo = "the mod's Hexium page (https://valheim.hexium.gg/mods/DrakeMods/DrakeModsLibs, Threads tab) or drakethos@gmail.com";
        DrakeIntegrationConfig.Bind(Config);
        MenuBindingRegistry.SetLogger(Logger);
        MenuBindingRegistry.Register(
            InventoryContextHints.BindingId,
            MenuBindingRegistry.InventoryContextScope,
            priority: 0,
            () => DrakeIntegrationConfig.InventoryOpenModifier,
            ModName);

        HarmonyPatchHub.ApplyAll(_harmony, Logger, guard);

        PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
        Logger.LogInfo($"{ModName} {Version} loaded (display patches, DrakeConfigSync API, shared wood UI + inventory context host).");
    }

    void OnVanillaPrefabs()
    {
        PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
        InventoryContextLocalization.Register();
    }
}