using System;
using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using ScarletCore.Data;
using ScarletCore.Events;
using ScarletCore.Services;
using ScarletCore.Utils;
using ScarletCore.Systems;
using ScarletRCON.Shared;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using ProjectM.UI;
using ProjectM.Behaviours;

namespace ScarletCarrier;

[RconCommandCategory("Test Commands")]
public static class TestCommands {
  public static Database Database => Plugin.Database;
  public static Dictionary<PrefabGUID, int> InventoryItems { get; set; } = [];
  public static Dictionary<string, PrefabGUID> EquipmentItems { get; set; } = new Dictionary<string, PrefabGUID>
  {
    { "Chest", new PrefabGUID(933057100) }, // ServantChest
    { "Legs", new PrefabGUID(-345596442) }, // ServantLegs
    { "Footgear", new PrefabGUID(1855323424) }, // ServantFootgear
    { "Gloves", new PrefabGUID(-1826382550) }, // ServantGloves
    { "Grimoire", new PrefabGUID(-1581189572) }, // ServantGrimoire
    { "Weapon", new PrefabGUID(869276797) } // ServantWeapon
  };

  public static List<Entity> SpawnedServants { get; set; } = [];
  public static readonly PrefabGUID _from_servant = new(-1128238456);
  private static readonly PrefabGUID _to_servant = new(-450600397);
  private static readonly PrefabGUID _coffin = new(723455393);
  private static readonly PrefabGUID _mapIconBuff = new(-1476191492); // MapIconBuff
  // private static readonly PrefabGUID _invisibleBuff = new(-1144825660); // InvisibleBuff
  private static readonly PrefabGUID _onSpawnBuff = new(390920678);

  public static List<PrefabGUID> BuffList { get; set; } = [
    // new(1185694153),  // Visual
    new(-480024072),  // Invunerable
    new(1934061152),  // Deaggro buff
    new(-1527408583), // Root buff
  ];

  [RconCommand("summon")]
  public static string SummonCommand(int duration) {
    if (!PlayerService.TryGetByName("Mark", out var playerData)) {
      return $"Player 'Mark' not found.";
    }

    CreateCoffin(playerData, duration);

    playerData.CharacterEntity.With((ref EntityInput entityInput) => {
      var characterPosition = playerData.CharacterEntity.Position();
      var currentAimPosition = playerData.CharacterEntity.Read<EntityAimData>().AimPosition;

      var aimDirection = MathUtility.GetDirection(characterPosition, currentAimPosition);
      var maxDistance = 4f;

      entityInput.AimPosition = characterPosition + (aimDirection * maxDistance);
    });

    AbilityService.CastAbility(playerData.CharacterEntity, new(2072201164));
    // BuffService.TryApplyBuff(playerData.CharacterEntity, new(-1527408583)); // Visual Buff

    // EventManager.OnServantDeath += HandleInventoryOnDeath;

    return $"Summoned Servant for player 'Mark'.";
  }

  public static void CreateCoffin(PlayerData playerData, float duration) {
    var coffin = UnitSpawnerService.ImmediateSpawn(_coffin, playerData.CharacterEntity.Position(), owner: Entity.Null, lifeTime: duration);
    var position = playerData.CharacterEntity.Position();
    var servant = CreateServant(playerData, coffin);

    SpawnedServants.Add(servant);

    TeleportService.TeleportToPosition(coffin, new(position.x, 200, position.z));

    coffin.SetTeam(playerData.CharacterEntity);

    servant.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = coffin;
    });

    // BuffService.TryApplyBuff(servant, _onSpawnBuff);
    // BuffService.TryApplyBuff(servant, new(-784079255), 1);

    coffin.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant._Entity = servant;
      coffinStation.ServantName = new FixedString64Bytes($"{playerData.Name}'s Carrier");
      coffinStation.State = ServantCoffinState.ServantAlive;
    });

    if (coffin.Has<DisableWhenNoPlayersInRange>()) {
      coffin.Remove<DisableWhenNoPlayersInRange>();
    }

    if (coffin.Has<DisableWhenNoPlayersInRangeOfChunk>()) {
      coffin.Remove<DisableWhenNoPlayersInRangeOfChunk>();
    }

    var servantDialog = new List<string> {
      "Hi I'm your carrier.",
      "You can give me your items to store them.",
      "Once you are done, please dismiss me.",
    };

    var index = 0;

    ActionScheduler.CreateSequence()
      .ThenWaitFrames(10)
      .Then(() => {
        var inventoryItems = Database.Get<Dictionary<int, int>>(playerData.PlatformId.ToString());

        foreach (var item in inventoryItems) {
          InventoryService.AddItem(servant, new(item.Key), item.Value);
        }

        servant.With((ref EntityAimData aimData) => {
          aimData.AimPosition = servant.Position();
        });

        CustomCastAbility(servant, new(2072201164));
      })
      .ThenWait(2f)
      .Then((cancelAction) => {
        // BuffService.TryRemoveBuff(playerData.CharacterEntity, new(-1527408583));
        // BuffService.TryRemoveBuff(servant, _onSpawnBuff);
        ActionScheduler.Repeating(() => {
          coffin.With((ref ServantCoffinstation coffinStation) => {
            if (index == servantDialog.Count) {
              coffinStation.ServantName = new FixedString64Bytes($"{playerData.Name}'s Carrier");
            } else {
              coffinStation.ServantName = new FixedString64Bytes(servantDialog[index]);
            }
          });

          index++;
        }, 2f, servantDialog.Count + 1);
      })
      .ThenWait(15)
      .Then(() => {
        // -597709516
        coffin.With((ref ServantCoffinstation coffinStation) => {
          coffinStation.ServantName = new FixedString64Bytes("All right, I'm about to head out.");
        });
      })
      .ThenWait(2)
      .Then(() => {
        // BuffService.TryRemoveBuff(playerData.CharacterEntity, new(-127008514)); // Remove interaction buff

        servant.Remove<Follower>();

        InventoryService.ClearInventory(servant);

        servant.With((ref Interactable interactable) => {
          interactable.Disabled = true;
        });

        coffin.With((ref ServantCoffinstation coffinStation) => {
          coffinStation.ServantName = new FixedString64Bytes("See you later!");
        });
        BuffService.TryRemoveBuff(servant, new(1124414432));
        BuffService.TryApplyBuff(servant, new(1185694153)); // Visual Buff
        CustomCastAbility(servant, new(-597709516));
      })
      .Execute();
  }

  public static void Unload() {
    foreach (var servant in SpawnedServants) {
      InventoryService.ClearInventory(servant);
      servant.Destroy(true);
    }
  }


  public static Entity CreateServant(PlayerData playerData, Entity owner) {
    var servant = UnitSpawnerService.ImmediateSpawn(_to_servant, owner.Position(), owner: playerData.CharacterEntity, lifeTime: -1f);

    foreach (var guid in BuffList) {
      BuffService.TryApplyBuff(servant, guid);
    }

    servant.Remove<ServantEquipment>();

    servant.With((ref AggroConsumer aggroConsumer) => {
      aggroConsumer.Active._Value = false;
    });

    servant.With((ref Aggroable aggroable) => {
      aggroable.Value._Value = false;
      aggroable.DistanceFactor._Value = 0f;
      aggroable.AggroFactor._Value = 0f;
    });

    servant.With((ref FactionReference factionReference) => {
      factionReference.FactionGuid._Value = new(-1430861195);
    });

    servant.With((ref Follower follower) => {
      follower.Followed._Value = playerData.UserEntity;
    });

    if (servant.Has<DisableWhenNoPlayersInRange>()) {
      servant.Remove<DisableWhenNoPlayersInRange>();
    }

    if (servant.Has<DisableWhenNoPlayersInRangeOfChunk>()) {
      servant.Remove<DisableWhenNoPlayersInRangeOfChunk>();
    }

    var characterPosition = playerData.CharacterEntity.Position();
    var aimPosition = playerData.CharacterEntity.Read<EntityAimData>().AimPosition;

    var aimDirection = MathUtility.GetDirection(characterPosition, aimPosition);

    var teleportDistance = 4f;

    var finalPosition = characterPosition + (aimDirection * teleportDistance);

    TeleportService.TeleportToPosition(servant, finalPosition);

    servant.SetTeam(playerData.CharacterEntity);

    return servant;
  }

  public static void asdasdasd(Entity entity, Entity target) {
    BuffService.TryApplyBuff(entity, new(1124414432));

    var servantPosition = entity.Position();
    var playerPosition = target.Position();

    var direction = playerPosition - servantPosition;
    direction.y = 0;
    var directionToPlayer = math.normalize(direction);

    entity.With((ref EntityInput entityInput) => {
      entityInput.AimDirection = directionToPlayer;
    });
  }

  public static void HandleInventoryOnDeath(object instance, DeathEventArgs deathInfo) {
    // EventManager.OnServantDeath -= HandleInventoryOnDeath;

    var deaths = deathInfo.Deaths;

    foreach (var death in deaths) {
      if (!death.Died.Has<ServantData>() || !death.Died.Has<Follower>() || !BuffService.HasBuff(death.Died, BuffList[0])) continue;

      var userEntity = death.Died.Read<Follower>().Followed._Value;

      var servant = death.Died;

      var inventoryItems = new Dictionary<int, int>();

      var items = InventoryService.GetInventoryItems(servant);

      foreach (var item in items) {
        if (item.ItemType.GuidHash == 0) continue;

        if (inventoryItems.ContainsKey(item.ItemType.GuidHash)) {
          inventoryItems[item.ItemType.GuidHash] += item.Amount;
        } else {
          inventoryItems[item.ItemType.GuidHash] = item.Amount;
        }

        // InventoryService.RemoveItem(servant, item.ItemType, item.Amount);
      }

      foreach (var item in inventoryItems) {
        Console.WriteLine($"Item: {item.Key}, Amount: {item.Value}");
      }

      Database.Save(userEntity.Read<User>().PlatformId.ToString(), inventoryItems);
    }
  }

  // [RconCommand("summon-chest")]
  public static string SpawnChest() {
    // -1374682671
    if (!PlayerService.TryGetByName("Mark", out var playerData)) {
      return $"Player 'Mark' not found.";
    }

    var guid = new PrefabGUID(-198459384);
    var position = playerData.CharacterEntity.Position();

    UnitSpawnerService.SpawnWithPostAction(guid, position, -1f, (entity) => {
      InventoryService.AddItem(entity, EquipmentItems["Chest"], 1);
    });

    return $"Summoned '{guid.GuidHash}' for player 'Mark'.";
  }

  [RconCommand("ability")]
  public static string AbilityCommand(string playerName, string prefabGUID) {
    if (!PlayerService.TryGetByName(playerName, out var playerData)) {
      return $"Player '{playerName}' not found.";
    }

    if (!PrefabGUID.TryParse(prefabGUID, out var guid)) {
      return $"Invalid GUID format: {prefabGUID}.";
    }

    // buff saindo do chão -784079255
    AbilityService.CastAbility(playerData.CharacterEntity, guid);

    return $"Triggered ability '{guid.GuidHash}' for player 'Mark'.";
  }

  public static void CustomCastAbility(Entity entity, PrefabGUID abilityGroup) {
    CastAbilityServerDebugEvent castAbilityServerDebugEvent = new() {
      AbilityGroup = abilityGroup,
      Who = entity.Read<NetworkId>(),
    };

    FromCharacter fromCharacter = new() {
      Character = entity,
      User = entity
    };

    GameSystems.DebugEventsSystem.CastAbilityServerDebugEvent(0, ref castAbilityServerDebugEvent, ref fromCharacter);
  }

  [RconCommand("debug.spawn")]
  public static string DebugSpawn(string prefabGUID, int duration = -1) {
    if (!PlayerService.TryGetByName("Mark", out var playerData)) {
      return $"Player 'Mark' not found.";
    }

    if (!PrefabGUID.TryParse(prefabGUID, out var guid)) {
      return $"Invalid GUID format: {prefabGUID}.";
    }

    UnitSpawnerService.SpawnWithPostAction(guid, playerData.CharacterEntity.Position(), duration, (entity) => {
      var components = GameSystems.EntityManager.GetComponentTypes(entity);

      foreach (var component in components) {
        Log.Info($"{component}");
      }
    });

    return $"Spawned entity with GUID '{guid.GuidHash}' for player 'Mark'.";
  }

  public static void LogComponents(Entity entity) {
    var components = GameSystems.EntityManager.GetComponentTypes(entity);
    foreach (var component in components) {
      Log.Info($"Entity has component: {component}");
    }
  }

  // [RconCommand("mapicon")]
  // public static string MapIconCommand(string playerName, string prefabGUID) {
  //   if (!PlayerService.TryGetByName(playerName, out var playerData)) {
  //     return $"Player '{playerName}' not found.";
  //   }

  //   if (!PrefabGUID.TryParse(prefabGUID, out var guid)) {
  //     return $"Invalid GUID format: {prefabGUID}.";
  //   }

  //   var coffin = Coffin.Read<ServantCoffinstation>();
  //   var servant = coffin.ConnectedServant.GetEntityOnServer();

  //   HandleMapIcon(playerData.CharacterEntity, servant, guid);

  //   return $"Added map icon '{guid.GuidHash}' for player '{playerName}'.";
  // }

  [RconCommand("test-spawn-lifetime")]
  public static string TestSpawnLifetime(string prefabGUID, float lifetime = 5f) {
    if (!PlayerService.TryGetByName("Mark", out var playerData)) {
      return $"Player 'Mark' not found.";
    }

    if (!PrefabGUID.TryParse(prefabGUID, out var guid)) {
      return $"Invalid GUID format: {prefabGUID}.";
    }

    var position = playerData.CharacterEntity.Position();
    var immediateEntity = UnitSpawnerService.ImmediateSpawn(guid, position, lifeTime: lifetime);

    UnitSpawnerService.SpawnWithPostAction(guid, position, lifeTime: lifetime, (entity) => {
      Log.Info($"Spawned entity with GUID '{guid.GuidHash}' for player 'Mark'.");
      var missingComponents = new List<ComponentType>();
      var immediateEntityComponents = GameSystems.EntityManager.GetComponentTypes(immediateEntity);
      var entityComponents = GameSystems.EntityManager.GetComponentTypes(entity);

      foreach (var component in entityComponents) {
        if (!immediateEntityComponents.Contains(component)) {
          Log.Info(component);
        }
      }

      Log.Info(entity.Read<UnitSpawnHandler>().StationEntity);
    });

    return $"Spawned test entities with {lifetime}s lifetime. Check console for component comparison.";
  }

  static void HandleMapIcon(Entity playerCharacter, Entity targetEntity, PrefabGUID prefabGUID) {
    if (GameSystems.ServerGameManager.TryInstantiateBuffEntityImmediate(playerCharacter, targetEntity, _mapIconBuff, out Entity buffEntity)) {
      var buffer = buffEntity.ReadBuffer<AttachMapIconsToEntity>();

      AttachMapIconsToEntity attachMapIconsToEntity = buffer[0];
      attachMapIconsToEntity.Prefab = prefabGUID;

      buffer[0] = attachMapIconsToEntity;

      if (!buffEntity.TryGetAttached(out Entity attached) || !attached.Exists() || attached.Equals(playerCharacter)) {
        buffEntity.Remove<Attached>();
        buffEntity.Write(new Attach(targetEntity));
      }

      if (!buffEntity.GetBuffTarget().Equals(targetEntity)) {
        buffEntity.With((ref Buff buff) => {
          buff.Target = targetEntity;
        });
      }
    }
  }

  // [Command(name: "spell", adminOnly: true, usage: ".spell [Nome] [Slot] [PrefabGuid]")]
  // public static void SetSpellCommand(ChatCommandContext ctx, string name, int slot, int ability) {
  //   PlayerData PlayerData = name.GetPlayerData();
  //   if (!PlayerData.UserEntity.Exists()) {
  //     OutputUtils.CustomErrorMessage(ctx, "Não foi possível encontrar o jogador!");
  //     return;
  //   }

  //   ulong steamId = PlayerData.User.PlatformId;

  //   ServerGameManager.ModifyAbilityGroupOnSlot(PlayerData.CharEntity, PlayerData.CharEntity, slot, new(ability), 99);
  //   OutputUtils.CustomNormalMessage(ctx, $"Slot <color=red>{slot}</color> definido como <color=white>{new PrefabGUID(ability).LookupName()}</color> para <color=green>{PlayerData.User.CharacterName.Value}</color>.");
  // }
}