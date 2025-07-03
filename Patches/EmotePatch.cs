using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using Unity.Entities;
using ScarletCore;
using Unity.Collections;
using ScarletCore.Utils;
using ScarletCarrier.Services;
using System.Collections.Generic;
using Stunlock.Core;
using System;

namespace ScarletCarrier.Patches;

[HarmonyPatch(typeof(EmoteSystem), nameof(EmoteSystem.OnUpdate))]
public static class EmoteSystemPatch {
  private static readonly Dictionary<PrefabGUID, Action<ulong>> EmoteActions = new() {
    { new(-1525577000), CarrierService.Spawn },
    { new(-53273186), CarrierService.Dismiss },
    { new(-452406649), CarrierService.ToggleFollow }
  };

  [HarmonyPrefix]
  static void OnUpdatePrefix(EmoteSystem __instance) {
    var entities = __instance._Query.ToEntityArray(Allocator.Temp);

    try {
      foreach (var entity in entities) {
        if (entity.IsNull() || !entity.Exists()) continue;
        var useEmoteEvent = entity.Read<UseEmoteEvent>();
        var fromCharacter = entity.Read<FromCharacter>();
        var emoteGuid = useEmoteEvent.Action;
        User user = fromCharacter.User.Read<User>();
        ulong steamId = user.PlatformId;

        if (!EmoteActions.TryGetValue(emoteGuid, out var action)) {
          continue;
        }

        action(steamId);
      }
    } catch (Exception ex) {
      Log.Error($"Error in EmoteSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
    }
  }
}