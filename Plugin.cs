﻿using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using ScarletCore.Data;
using VampireCommandFramework;
using ScarletCore.Events;
using ScarletCarrier.Services;

namespace ScarletCarrier;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
[BepInDependency("markvaaz.ScarletCore")]
[BepInDependency("gg.deca.VampireCommandFramework")]
public class Plugin : BasePlugin {
  static Harmony _harmony;
  public static Harmony Harmony => _harmony;
  public static Plugin Instance { get; private set; }
  public static ManualLogSource LogInstance { get; private set; }
  public static Database Database { get; private set; }

  public override void Load() {
    Instance = this;
    LogInstance = Log;

    Log.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} version {MyPluginInfo.PLUGIN_VERSION} is loaded!");

    _harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
    _harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());

    EventManager.OnInitialize += (_, _) => {
      Log.LogInfo("Removing carrier entities...");
      CarrierService.ClearAll();
      Log.LogInfo("Carrier entities removed.");
    };

    Database = new Database(MyPluginInfo.PLUGIN_GUID);
    CommandRegistry.RegisterAll();
  }

  public override bool Unload() {
    _harmony?.UnpatchSelf();
    CommandRegistry.UnregisterAssembly();
    return true;
  }
}
