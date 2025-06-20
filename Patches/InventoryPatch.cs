using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ScarletCore.Data;
using ScarletCore.Services;
using Unity.Collections;
using Unity.Entities;
using ScarletCore;
using ScarletCore.Systems;
using ScarletCarrier.Services;
using ScarletCore.Utils;

namespace ScarletCarrier.Patches;


[HarmonyPatch]
internal static class MoveItemBetweenInventoriesSystemPatch {
  private static readonly Database Database = Plugin.Database;
  [HarmonyPatch(typeof(MoveItemBetweenInventoriesSystem), nameof(MoveItemBetweenInventoriesSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(MoveItemBetweenInventoriesSystem __instance) {
    if (!GameSystems.Initialized) return;
    var entities = __instance.__query_133601321_0.ToEntityArray(Allocator.Temp);
    var events = __instance.__query_133601321_0.ToComponentDataArray<MoveItemBetweenInventoriesEvent>(Allocator.Temp);
    var fromCharacters = __instance.__query_133601321_0.ToComponentDataArray<FromCharacter>(Allocator.Temp);

    try {
      for (var i = 0; i < events.Length; i++) {
        var moveItemEvent = events[i];
        var entity = entities[i];
        var fromCharacter = fromCharacters[i];

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var user = fromCharacter.User.Read<User>();

        if (!CarrierService.HasServant(user.PlatformId)) continue;

        var servantEntity = CarrierService.GetServant(user.PlatformId);

        if (!servantEntity.Has<NetworkId>() || !servantEntity.Read<NetworkId>().Equals(moveItemEvent.ToInventory)) continue;

        var characterEntity = fromCharacter.Character;
        var characterInventory = InventoryService.GetInventoryItems(characterEntity);
        var itemSlot = moveItemEvent.FromSlot;

        if (InventoryUtilities.TryGetItemAtSlot(characterInventory, itemSlot, out var itemBuffer)) {
          var item = GameSystems.PrefabCollectionSystem._PrefabGuidToEntityMap[itemBuffer.ItemType];

          if (!ItemService.IsValid(item) || itemBuffer.ItemEntity._Entity.Has<Equippable>()) {
            entity.Destroy(true);
          }
        }
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in MoveItemBetweenInventoriesSystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
      events.Dispose();
    }
  }
}

[HarmonyPatch]
internal static class ReactToInventoryChangedSystemPatch {
  private static readonly Database Database = Plugin.Database;
  [HarmonyPatch(typeof(ReactToInventoryChangedSystem), nameof(ReactToInventoryChangedSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(ReactToInventoryChangedSystem __instance) {
    if (!GameSystems.Initialized) return;
    var query = __instance.__query_2096870026_0.ToComponentDataArray<InventoryChangedEvent>(Allocator.Temp);

    try {
      foreach (var e in query) {
        if (e.InventoryEntity.Equals(Entity.Null) || !e.InventoryEntity.Has<Attach>()) continue;

        var servant = e.InventoryEntity.Read<Attach>().Parent;

        if (servant.Equals(Entity.Null)) continue;

        if (e.ChangeType == InventoryChangedEventType.Moved || !servant.Has<ServantData>() || !servant.Has<Follower>()) continue;

        var playerOwner = servant.Read<Follower>().Followed._Value;

        if (playerOwner.Equals(Entity.Null) || !playerOwner.Exists() || !playerOwner.Has<User>()) continue;

        var inventory = InventoryService.GetInventoryItems(servant);
        var inventoryItems = new Dictionary<int, int>();
        var user = playerOwner.Read<User>();
        var platformId = user.PlatformId.ToString();

        foreach (var item in inventory) {
          if (item.ItemType.GuidHash == 0) continue;
          var itemEntity = GameSystems.PrefabCollectionSystem._PrefabGuidToEntityMap[item.ItemType];

          if (!ItemService.IsValid(itemEntity)) {
            MessageService.Send(user, "You ignored the warnings and tried to store equipment in your carrier. ~The item has been deleted forever~!".FormatError());
            InventoryService.RemoveItem(servant, item.ItemType, item.Amount);
            continue;
          }

          if (inventoryItems.ContainsKey(item.ItemType.GuidHash)) {
            inventoryItems[item.ItemType.GuidHash] += item.Amount;
          } else {
            inventoryItems[item.ItemType.GuidHash] = item.Amount;
          }
        }

        Database.Save(platformId, inventoryItems);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in ReactToInventoryChangedSystemPatch: {ex.Message}");
      return;
    } finally {
      query.Dispose();
    }
  }
}

[HarmonyPatch]
internal static class EquipServantItemFromInventorySystemPatch {
  [HarmonyPatch(typeof(EquipServantItemFromInventorySystem), nameof(EquipServantItemFromInventorySystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(EquipServantItemFromInventorySystem __instance) {
    var entities = __instance.EntityQueries[0].ToEntityArray(Allocator.Temp);
    var events = __instance.EntityQueries[0].ToComponentDataArray<EquipServantItemFromInventoryEvent>(Allocator.Temp);

    try {
      var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;
      for (var i = 0; i < events.Length; i++) {
        var moveItemEvent = events[i];
        var entity = entities[i];

        if (!niem.TryGetValue(moveItemEvent.FromInventory, out Entity fromInventory)) continue;

        if (!fromInventory.Has<Attach>()) continue;

        var characterEntity = fromInventory.Read<Attach>().Parent;

        if (!characterEntity.Has<PlayerCharacter>()) continue;

        var userEntity = characterEntity.Read<PlayerCharacter>().UserEntity;
        var user = userEntity.Read<User>();

        if (!CarrierService.HasServant(user.PlatformId)) continue;

        var servantEntity = CarrierService.GetServant(user.PlatformId);

        if (!servantEntity.Has<NetworkId>() || !servantEntity.Read<NetworkId>().Equals(moveItemEvent.ToEntity)) continue;

        entity.Destroy(true);
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in EquipServantItemFromInventorySystemPatch: {ex.Message}");
    } finally {
      entities.Dispose();
      events.Dispose();
    }
  }

  [HarmonyPatch(typeof(EquipmentTransferSystem), nameof(EquipmentTransferSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void OnUpdatePrefix(EquipmentTransferSystem __instance) {
    var entities = __instance.EntityQueries[0].ToEntityArray(Allocator.Temp);
    var events = __instance.EntityQueries[0].ToComponentDataArray<EquipmentToEquipmentTransferEvent>(Allocator.Temp);
    var fromCharacters = __instance.EntityQueries[0].ToComponentDataArray<FromCharacter>(Allocator.Temp);

    try {
      var niem = GameSystems.NetworkIdSystem._NetworkIdLookupMap._NetworkIdToEntityMap;
      for (int i = 0; i < entities.Length; i++) {
        var equipmentEvent = events[i];
        var entity = entities[i];
        var fromCharacter = fromCharacters[i];

        if (!fromCharacter.Character.Has<PlayerCharacter>() || !fromCharacter.User.Has<User>()) continue;

        var user = fromCharacter.User.Read<User>();

        if (!CarrierService.HasServant(user.PlatformId)) continue;

        if (!equipmentEvent.ServantToCharacter && niem.TryGetValue(equipmentEvent.ToEntity, out Entity servant) && servant.Has<Follower>()) {
          entity.Destroy(true);
        }
      }
    } finally {
      entities.Dispose();
      events.Dispose();
      fromCharacters.Dispose();
    }
  }
}
