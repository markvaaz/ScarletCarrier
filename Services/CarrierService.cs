using System.Collections.Generic;
using ProjectM;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletCarrier.Models;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ScarletCarrier.Services;

internal static class CarrierService {
  private static readonly Dictionary<ulong, Carrier> _activeCarriers = [];
  public static readonly PrefabGUID[] AppearancePrefabs = [
    new(-450600397),  // Bomber
    new(2142021685),  // Alchemist
    new(40217214),    // Lurker
    new(-1099047820), // Night Maiden
    new(-1108748448), // Viper
    new(-274383877),  // Striker
    new(-383158562),  // Lightweaver
    new(1649578802),  // Paladin
    new(-1213645419), // Sentry Officer
    new(565869317),   // Tractor Beamer
    new(-1773935659), // Militia Veteran
    new(1502148822),  // Exsanguinator
    new(-924080115),  // Tazer
    new(-1788957652), // Nun
    new(-444945115),  // Sister
    new(1218339832),  // Cleric
    new(-823557242),  // Devoted
    new(-442412464),  // Slave Master
    new(-1416355128), // Ruffian
    new(-1192403515), // Villager Female
    new(-2085282780), // Villager Male
    new(-1897484769), // Ace Incinerator
  ];

  public static readonly string[] AppearanceNames = [
    "Bomber",
    "Alchemist",
    "Lurker",
    "Night Maiden",
    "Viper",
    "Striker",
    "Lightweaver",
    "Paladin",
    "Sentry Officer",
    "Tractor Beamer",
    "Militia Veteran",
    "Exsanguinator",
    "Tazer",
    "Nun",
    "Sister",
    "Cleric",
    "Devoted",
    "Slave Master",
    "Ruffian",
    "Villager (Female)",
    "Villager (Male)",
    "~*~~Ace Incinerator~".Format(["green", RichTextFormatter.HighlightColor]),
  ];

  private static readonly PrefabGUID SpawnAbility = new(2072201164);
  public const string CustomAppearances = "CustomAppearances";

  public static void Spawn(ulong platformId) {
    var playerData = platformId.GetPlayerData();

    if (_activeCarriers.TryGetValue(playerData.PlatformId, out var existingCarrier)) {
      if (existingCarrier.IsDismissInProgress) {
        SendSCT(playerData);
        return;
      }
      TeleportToPlayer(platformId);
      return;
    }

    AbilityService.CastAbility(playerData.CharacterEntity, SpawnAbility);

    var carrier = new Carrier(playerData);

    carrier.Create();
    carrier.CreateSpawnSequence();

    _activeCarriers[playerData.PlatformId] = carrier;
  }

  public static void Dismiss(ulong platformId) {
    var playerData = platformId.GetPlayerData();

    if (!_activeCarriers.TryGetValue(playerData.PlatformId, out var carrier)) {
      SendSCT(playerData);
      return;
    }

    if (carrier.IsDismissInProgress) {
      SendSCT(playerData);
      return;
    }

    carrier.CreateDismissSequence(() => _activeCarriers.Remove(playerData.PlatformId));
  }

  public static bool HasServant(ulong platformId) {
    return _activeCarriers.TryGetValue(platformId, out var carrier) && carrier.IsValid();
  }

  public static Entity GetServant(ulong platformId) {
    if (_activeCarriers.TryGetValue(platformId, out var carrier) && carrier.IsValid()) {
      return carrier.ServantEntity;
    }
    return Entity.Null;
  }

  public static bool IsFollowing(ulong platformId) {
    return _activeCarriers.TryGetValue(platformId, out var carrier) && carrier.IsFollowing;
  }

  public static void ToggleFollow(ulong platformId) {
    if (_activeCarriers.TryGetValue(platformId, out var carrier)) {
      carrier.ToggleFollow();
    }
  }

  public static void StartFollow(ulong platformId) {
    if (_activeCarriers.TryGetValue(platformId, out var carrier)) {
      carrier.StartFollow();
    }
  }

  public static void StopFollow(ulong platformId) {
    if (_activeCarriers.TryGetValue(platformId, out var carrier)) {
      carrier.StopFollow();
    }
  }

  public static void TeleportToPlayer(ulong platformId) {
    if (_activeCarriers.TryGetValue(platformId, out var carrier)) {
      carrier.TeleportToPlayer();
    }
  }

  public static void ClearAll() {
    foreach (var carrier in _activeCarriers.Values) {
      carrier.Destroy();
    }
    _activeCarriers.Clear();

    var coffins = GameSystems.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ServantCoffinstation>()).ToEntityArray(Allocator.Temp);

    foreach (var coffin in coffins) {
      ClearCoffinFromWorld(coffin);
    }

    coffins.Dispose();
  }

  private static void ClearCoffinFromWorld(Entity coffin) {
    if (Entity.Null.Equals(coffin) || !coffin.Has<ServantCoffinstation>()) return;

    if (!coffin.Has<NameableInteractable>() || !coffin.Has<LocalTransform>()) return;

    var position = coffin.Read<LocalTransform>().Position;
    var id = coffin.Read<NameableInteractable>().Name.Value;

    if (position.y != Carrier.Height && id != Carrier.Id) return;

    var servant = coffin.Read<ServantCoffinstation>().ConnectedServant._Entity;

    if (!Entity.Null.Equals(servant) && servant.Has<Follower>()) {
      ClearServantFromWorld(servant);
    }

    var coffinBuffBuffer = coffin.ReadBuffer<BuffBuffer>();

    foreach (var buff in coffinBuffBuffer) {
      BuffService.TryRemoveBuff(coffin, buff.PrefabGuid);
    }

    coffin.Destroy();
  }

  private static void ClearServantFromWorld(Entity servant) {
    if (Entity.Null.Equals(servant) || !servant.Has<Follower>()) return;

    servant.Remove<Follower>();

    InventoryService.ClearInventory(servant);

    var servantBuffBuffer = servant.ReadBuffer<BuffBuffer>();

    foreach (var buff in servantBuffBuffer) {
      BuffService.TryRemoveBuff(servant, buff.PrefabGuid);
    }

    servant.Destroy();
  }

  private static void SendSCT(PlayerData player) {
    ScrollingCombatTextMessage.Create(
      GameSystems.EntityManager,
      GameSystems.EndSimulationEntityCommandBufferSystem.CreateCommandBuffer(),
      AssetGuid.FromString("45e3238f-36c1-427c-b21c-7d50cfbd77bc"),
      player.CharacterEntity.Position(),
      new float3(1f, 0f, 0f),
      player.CharacterEntity,
      0,
      new PrefabGUID(-1404311249),
      player.UserEntity
    );
  }
}
