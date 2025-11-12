using HarmonyLib;
using ProjectM;
using ProjectM.Gameplay.Systems;
using ProjectM.Shared;
using ScarletCarrier.Models;
using ScarletCore;
using ScarletCore.Services;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;

namespace ScarletCarrier.Patches;

[HarmonyPatch(typeof(InteractValidateAndStopSystemServer), nameof(InteractValidateAndStopSystemServer.OnUpdate))]
public static class InteractPatch {
  private static readonly PrefabGUID CarrierInteractAbility = new(-127008514);
  [HarmonyPrefix]
  public static void Prefix(InteractValidateAndStopSystemServer __instance) {
    var query = __instance.__query_195794971_3.ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {

      if (entity.GetPrefabGuid() != CarrierInteractAbility) continue;

      var abilityOwner = entity.Read<EntityOwner>().Owner;

      if (abilityOwner == Entity.Null || !abilityOwner.Has<PlayerCharacter>()) continue;

      var servant = abilityOwner.Read<Interactor>().Target;

      if (!servant.Has<NameableInteractable>()) continue;

      var id = servant.Read<NameableInteractable>().Name.Value;

      if (id != Carrier.Id) continue;

      if (!servant.Has<Follower>()) continue;

      var player = abilityOwner.GetPlayerData();
      var owner = servant.Read<Follower>().Followed._Value;

      if (player.IsAdmin) continue;

      if (owner != player.UserEntity && owner != player.CharacterEntity) {
        BuffService.TryRemoveBuff(abilityOwner, CarrierInteractAbility);
      }
    }
  }
}