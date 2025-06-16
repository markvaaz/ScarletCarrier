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

    try {
      for (var i = 0; i < events.Length; i++) {
        var moveItemEvent = events[i];

        var entity = entities[i];
        var fromCharacter = entity.Read<FromCharacter>();

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
          Log.Components(item);
          if (!ItemService.IsValid(item)) {
            GameSystems.EntityManager.DestroyEntity(entity);
          }
        }
      }
    } catch (System.Exception ex) {
      Log.Error($"Error in MoveItemBetweenInventoriesSystemPatch: {ex.Message}");
      return;
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
        var platformId = playerOwner.Read<User>().PlatformId.ToString();

        foreach (var item in inventory) {
          if (item.ItemType.GuidHash == 0) continue;

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
