using BepInEx;
using HarmonyLib;

namespace DrakeModsLibs;

[BepInPlugin(GUID, ModName, Version)]
public partial class CustomizeLibsPlugin : BaseUnityPlugin
{

    private readonly Harmony _harmony = new("drakemods.DrakeModsLibs");

    private void Awake()
    {
        HarmonyPatchHub.ApplyAll(_harmony, Logger);
        Logger.LogInfo($"{ModName} {Version} loaded (display patches, DrakeConfigSync API, shared API).");
    }
}
