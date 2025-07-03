using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ScarletCarrier.Services;
using ScarletCore;
using ScarletCore.Utils;
using Stunlock.Core;
using Unity.Collections;

namespace ScarletCarrier.Patches;

[HarmonyPatch(typeof(AbilityCastStarted_SetupAbilityTargetSystem_Shared), nameof(AbilityCastStarted_SetupAbilityTargetSystem_Shared.OnUpdate))]
public static class AbilityPatch {
  private static readonly PrefabGUID FlightAbilityGuid = new(-104327922);
  [HarmonyPrefix]
  static void OnUpdatePrefix(AbilityCastStarted_SetupAbilityTargetSystem_Shared __instance) {
    var castStartedEvents = __instance.EntityQueries[0].ToComponentDataArray<AbilityCastStartedEvent>(Allocator.Temp);

    try {
      foreach (AbilityCastStartedEvent castStartedEvent in castStartedEvents) {
        var abilityGuid = castStartedEvent.AbilityGroup.Read<PrefabGUID>();

        if (abilityGuid.GuidHash != FlightAbilityGuid.GuidHash) {
          continue;
        }

        var characterEntity = castStartedEvent.Character;

        if (!characterEntity.Has<PlayerCharacter>() && !characterEntity.Has<User>()) {
          continue;
        }

        var playerData = characterEntity.GetPlayerData();

        CarrierService.Dismiss(playerData.PlatformId);
      }
    } finally {
      castStartedEvents.Dispose();
    }
  }
}