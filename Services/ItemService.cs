using ScarletCore;
using ScarletCarrier.Data;
using Unity.Entities;
using ProjectM;

namespace ScarletCarrier.Services;

internal static class ItemService {
  public static ModifiedItemType GetItemType(Entity itemEntity) {
    if (!itemEntity.Exists()) return ModifiedItemType.None;

    if (itemEntity.Has<WeaponLevelSource>()) {
      return ModifiedItemType.Weapon;
    }

    if (itemEntity.Has<ArmorLevelSource>()) {
      return ModifiedItemType.Armor;
    }

    if (itemEntity.Has<SpellLevelSource>()) {
      return ModifiedItemType.SpellSource;
    }

    if (itemEntity.Has<ConsumableCondition>()) {
      return ModifiedItemType.Consumable;
    }

    if (itemEntity.Has<JewelLevelSource>()) {
      return ModifiedItemType.Jewel;
    }

    return ModifiedItemType.None;
  }

  public static bool IsValid(Entity itemEntity) {
    if (!itemEntity.Exists()) return false;

    if (GetItemType(itemEntity) != ModifiedItemType.None) {
      return false;
    }

    return true;
  }

  /* work in progress */
}