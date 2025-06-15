using System.Collections.Generic;
using HarmonyLib;
using ProjectM;
using ProjectM.Network;
using ScarletCore.Data;
using ScarletCore.Services;
using Unity.Collections;
using Unity.Entities;
using ScarletCore;

namespace ScarletCarrier.Patches;


[HarmonyPatch]
internal static class EquipmentTransferSystemPatch {
  private static readonly Database Database = Plugin.Database;
  [HarmonyPatch(typeof(ReactToInventoryChangedSystem), nameof(ReactToInventoryChangedSystem.OnUpdate))]
  [HarmonyPrefix]
  public static void Prefix(EquipServantItemFromInventorySystem __instance) {
    var query = __instance.__query_1850506269_0.ToComponentDataArray<InventoryChangedEvent>(Allocator.Temp);
    foreach (var e in query) {
      if (e.InventoryEntity.Equals(Entity.Null) || !e.InventoryEntity.Has<Attach>()) continue;

      var servant = e.InventoryEntity.Read<Attach>().Parent;

      if (e.ChangeType == InventoryChangedEventType.Moved || !servant.Has<ServantData>() || !servant.Has<Follower>()) continue;

      var playerOwner = servant.Read<Follower>().Followed._Value;
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
  }
}
