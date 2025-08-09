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
using System;

namespace ScarletCarrier.Services;

internal static class CarrierService {
  private static readonly Dictionary<ulong, Carrier> Carriers = [];
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

  public const string CustomAppearances = "CustomAppearances";

  public static void Initialize() {
    ClearAllLegacy();

    var query = GameSystems.EntityManager.CreateEntityQuery(new EntityQueryDesc {
      All = new[] { ComponentType.ReadOnly<ServantCoffinstation>() },
      Options = EntityQueryOptions.IncludeDisabledEntities
    }).ToEntityArray(Allocator.Temp);

    foreach (var entity in query) {
      if (entity.IsNull() || !entity.Exists()) continue;
      if (!entity.Has<ServantCoffinstation>() || !entity.Has<NameableInteractable>() || !entity.Has<EntityOwner>()) continue;
      var owner = entity.Read<EntityOwner>().Owner;
      var player = owner.GetPlayerData();

      if (player == null) continue;

      var servant = entity.Read<ServantCoffinstation>().ConnectedServant._Entity;

      if (!servant.Exists()) {
        entity.Destroy();
        continue;
      }

      var carrier = new Carrier(entity, servant, player);

      Carriers[player.PlatformId] = carrier;

      carrier.Hide();
    }
  }

  public static void Spawn(ulong platformId) {
    var playerData = platformId.GetPlayerData();
    Carrier carrier;

    if (!Carriers.ContainsKey(playerData.PlatformId)) {
      carrier = new(playerData);
      carrier.Create();
    } else carrier = Carriers[playerData.PlatformId];

    if (carrier == null) {
      Log.Error($"Failed to create carrier for player {playerData.Name} ({playerData.PlatformId}).");
      return;
    }

    // Just in case the lifetime has expired or the entities are missing (extremely unlikely)
    if (!carrier.ServantEntity.Exists() || !carrier.CoffinEntity.Exists()) {
      Carriers.Remove(playerData.PlatformId);
      carrier = new(playerData);
      carrier.Create();
      return;
    }

    carrier.Call();

    Carriers[playerData.PlatformId] = carrier;
  }

  public static void Dismiss(ulong platformId) {
    var playerData = platformId.GetPlayerData();

    if (!Carriers.TryGetValue(playerData.PlatformId, out var carrier)) {
      return;
    }

    // Just in case the lifetime has expired or the entities are missing (extremely unlikely)
    if (!carrier.ServantEntity.Exists() || !carrier.CoffinEntity.Exists()) {
      Carriers.Remove(playerData.PlatformId);
      carrier = new(playerData);
      carrier.Create();
      return;
    }

    carrier.Dismiss();
  }
  public static Carrier GetCarrier(ulong platformId) {
    if (Carriers.TryGetValue(platformId, out var carrier) && carrier.IsValid()) {
      return carrier;
    }
    return null;
  }

  public static bool HasServant(ulong platformId) {
    return Carriers.TryGetValue(platformId, out var carrier) && carrier.IsValid();
  }

  public static bool IsFollowing(ulong platformId) {
    return Carriers.TryGetValue(platformId, out var carrier) && carrier.IsFollowing;
  }

  public static void ToggleFollow(ulong platformId) {
    if (Carriers.TryGetValue(platformId, out var carrier)) {
      carrier.ToggleFollow();
    }
  }

  public static void StartFollow(ulong platformId) {
    if (Carriers.TryGetValue(platformId, out var carrier)) {
      carrier.StartFollow();
    }
  }

  public static void StopFollow(ulong platformId) {
    if (Carriers.TryGetValue(platformId, out var carrier)) {
      carrier.StopFollow();
    }
  }

  public static void ClearAllLegacy() {
    var query = GameSystems.EntityManager.CreateEntityQuery(new EntityQueryDesc {
      All = new[] { ComponentType.ReadOnly<ServantCoffinstation>() },
      Options = EntityQueryOptions.IncludeDisabledEntities
    }).ToEntityArray(Allocator.Temp);

    foreach (var coffin in query) {
      ClearLegacyCoffinFromWorld(coffin);
    }
  }

  private static void ClearLegacyCoffinFromWorld(Entity coffin) {
    if (Entity.Null.Equals(coffin) || !coffin.Has<ServantCoffinstation>()) return;

    if (!coffin.Has<NameableInteractable>() || !coffin.Has<LocalTransform>()) return;

    var position = coffin.Read<LocalTransform>().Position;
    var id = coffin.Read<NameableInteractable>().Name.Value;

    if (position.y != Carrier.LegacyHeight || id != Carrier.LegacyId) return;
    var servant = coffin.Read<ServantCoffinstation>().ConnectedServant._Entity;

    if (!Entity.Null.Equals(servant) && servant.Has<Follower>()) {
      ClearLegacyServantFromWorld(servant);
    }

    var coffinBuffBuffer = coffin.ReadBuffer<BuffBuffer>();

    foreach (var buff in coffinBuffBuffer) {
      BuffService.TryRemoveBuff(coffin, buff.PrefabGuid);
    }

    Log.Info($"Clearing legacy coffin {coffin} from world.");

    coffin.Destroy();
  }

  private static void ClearLegacyServantFromWorld(Entity servant) {
    if (Entity.Null.Equals(servant) || !servant.Has<Follower>()) return;

    servant.Remove<Follower>();

    InventoryService.ClearInventory(servant);

    var servantBuffBuffer = servant.ReadBuffer<BuffBuffer>();

    foreach (var buff in servantBuffBuffer) {
      BuffService.TryRemoveBuff(servant, buff.PrefabGuid);
    }

    Log.Info($"Clearing legacy servant {servant} from world.");

    servant.Destroy();
  }
}
