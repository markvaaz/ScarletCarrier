using System.Collections.Generic;
using ProjectM;
using ScarletCore;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletCarrier.Models;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

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
    var query = GameSystems.EntityManager.CreateEntityQuery(new EntityQueryDesc {
      All = new[] { ComponentType.ReadOnly<ServantData>() },
      Options = EntityQueryOptions.IncludeDisabled
    }).ToEntityArray(Allocator.Temp);

    foreach (var servant in query) {
      if (servant.IsNull() || !servant.Exists()) continue;
      if (!servant.Has<ServantData>() || !servant.Has<NameableInteractable>() || !servant.Has<EntityOwner>()) continue;
      if (!servant.Has<NameableInteractable>() || servant.Read<NameableInteractable>().Name.Value != Carrier.Id) continue;
      var owner = servant.Read<EntityOwner>().Owner;
      var player = owner.GetPlayerData();

      if (player == null) continue;

      var coffin = servant.Read<ServantConnectedCoffin>().CoffinEntity._Entity;

      if (!coffin.Exists()) {
        Log.Info($"Found orphaned servant for player {player.Name} ({player.PlatformId}), recreating coffin...");

        // Create carrier with orphaned servant and recreate coffin
        var carrier = new Carrier(Entity.Null, servant, player);
        carrier.RecreateCoffin();

        Carriers[player.PlatformId] = carrier;
        carrier.Hide();
        continue;
      }

      var validCarrier = new Carrier(coffin, servant, player);

      Carriers[player.PlatformId] = validCarrier;

      validCarrier.Hide();
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

    // Check if entities are missing and handle accordingly
    if (!carrier.ServantEntity.Exists() && !carrier.CoffinEntity.Exists()) {
      // Both are missing - create new carrier
      Log.Info($"Both servant and coffin missing for player {playerData.Name}, creating new carrier.");
      Carriers.Remove(playerData.PlatformId);
      carrier = new(playerData);
      carrier.Create();
      return;
    } else if (!carrier.CoffinEntity.Exists() && carrier.ServantEntity.Exists()) {
      // Only coffin is missing - recreate it
      Log.Info($"Coffin missing for player {playerData.Name}, recreating coffin...");
      carrier.RecreateCoffin();
    } else if (!carrier.ServantEntity.Exists() && carrier.CoffinEntity.Exists()) {
      // Only servant is missing - this shouldn't happen, but recreate both
      Log.Warning($"Servant missing but coffin exists for player {playerData.Name}, recreating both...");
      Carriers.Remove(playerData.PlatformId);
      carrier = new(playerData);
      carrier.Create();
      return;
    }

    InventoryService.ModifyInventorySize(carrier.ServantEntity, 27);

    // Ensure entities are enabled before calling
    carrier.EnsureEntitiesEnabled();

    carrier.Call();

    Carriers[playerData.PlatformId] = carrier;
  }

  public static void Dismiss(ulong platformId) {
    var playerData = platformId.GetPlayerData();

    if (!Carriers.TryGetValue(playerData.PlatformId, out var carrier)) {
      return;
    }

    // Check if entities are missing and handle accordingly
    if (!carrier.ServantEntity.Exists() && !carrier.CoffinEntity.Exists()) {
      // Both are missing - create new carrier
      Log.Info($"Both servant and coffin missing for player {playerData.Name}, creating new carrier for dismiss.");
      Carriers.Remove(playerData.PlatformId);
      carrier = new(playerData);
      carrier.Create();
    } else if (!carrier.CoffinEntity.Exists() && carrier.ServantEntity.Exists()) {
      // Only coffin is missing - recreate it
      Log.Info($"Coffin missing for player {playerData.Name}, recreating coffin for dismiss...");
      carrier.RecreateCoffin();
    } else if (!carrier.ServantEntity.Exists() && carrier.CoffinEntity.Exists()) {
      // Only servant is missing - this shouldn't happen, but recreate both
      Log.Warning($"Servant missing but coffin exists for player {playerData.Name}, recreating both for dismiss...");
      Carriers.Remove(playerData.PlatformId);
      carrier = new(playerData);
      carrier.Create();
    }

    carrier.Dismiss();
  }

  public static void RemoveCarrier(ulong platformId) {
    if (HasServant(platformId)) {
      Carriers.Remove(platformId);
    }
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
      Options = EntityQueryOptions.IncludeDisabled
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
