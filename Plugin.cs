using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ScarletCore.Data;
using VampireCommandFramework;
using ScarletCore.Events;
using ScarletCarrier.Services;
using ScarletCore.Systems;
using Unity.Mathematics;
using System.Reflection;

namespace ScarletCarrier;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("markvaaz.ScarletCore")]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin {
  static Harmony _harmony;
  public static Harmony Harmony => _harmony;
  public static Plugin Instance { get; private set; }
  public static ManualLogSource LogInstance { get; private set; }
  public static Settings Settings { get; private set; }
  public static Database Database { get; private set; }

  public override void Load() {
    Instance = this;
    LogInstance = Log;

    Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

    _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
    _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

    Database = new Database(MyPluginInfo.PLUGIN_GUID);
    Settings = new Settings(MyPluginInfo.PLUGIN_GUID, Instance);

    GameSystems.OnInitialize(OnInitialize);

    EventManager.On(PlayerEvents.PlayerLeft, (player) => {
      if (CarrierService.HasServant(player.PlatformId)) {
        CarrierService.Dismiss(player.PlatformId);
      }
    });

    LoadSettings();
    CommandRegistry.RegisterAll();
  }

  public override bool Unload() {
    _harmony?.UnpatchSelf();
    EventManager.UnregisterAssembly(Assembly.GetExecutingAssembly());
    ActionScheduler.UnregisterAssembly(Assembly.GetExecutingAssembly());
    CommandRegistry.UnregisterAssembly();
    return true;
  }

  public static void OnInitialize() {
    LogInstance.LogInfo("Removing carrier entities...");
    CarrierService.Initialize();
    CleanupService.ClearEntitiesInRadius(new float2(0, 0), 15);
    LogInstance.LogInfo("Carrier entities removed.");
  }

  public static void LoadSettings() {
    Settings.Section("General")
      .Add("ExpireDays", 15, "Carrier lifetime in days before automatic cleanup (server uptime only).\nTimer resets each time a player interacts with their carrier.\nWarning: All items stored in expired carriers will be permanently lost.");
  }
}
