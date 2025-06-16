using ScarletCore;
using ScarletCarrier.Data;
using Unity.Entities;
using ProjectM;

namespace ScarletCarrier.Services;

internal static class ItemService {
  public static InstancedItemType GetItemType(Entity itemEntity) {
    if (!itemEntity.Exists()) return InstancedItemType.None;

    if (itemEntity.Has<WeaponLevelSource>()) {
      return InstancedItemType.Weapon;
    }

    if (itemEntity.Has<ArmorLevelSource>()) {
      return InstancedItemType.Armor;
    }

    if (itemEntity.Has<SpellLevelSource>()) {
      return InstancedItemType.SpellSource;
    }

    if (itemEntity.Has<ConsumableCondition>()) {
      return InstancedItemType.Consumable;
    }

    if (itemEntity.Has<JewelLevelSource>()) {
      return InstancedItemType.Jewel;
    }

    return InstancedItemType.None;
  }

  public static bool IsValid(Entity itemEntity) {
    if (!itemEntity.Exists()) return false;

    if (GetItemType(itemEntity) != InstancedItemType.None) {
      return false;
    }

    return true;
  }

  /* work in progress */
}