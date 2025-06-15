using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;

namespace ScarletCarrier.Services;

internal static class CarrierService {
  public static Database Database => Plugin.Database;
  public static Dictionary<PrefabGUID, int> InventoryItems { get; set; } = [];
  public static Dictionary<ulong, List<Entity>> SpawnedServants { get; set; } = [];
  public static Dictionary<ulong, List<ActionId>> SpawnSequences { get; set; } = [];
  private static readonly PrefabGUID _coffin = new(723455393);
  public static readonly PrefabGUID _from_servant = new(-1128238456);
  private static readonly PrefabGUID _to_servant = new(-450600397);
  private static readonly PrefabGUID _onDespawnVisualBuff = new(1185694153);
  private static readonly PrefabGUID _spawnAbility = new(2072201164);
  private static readonly PrefabGUID _despawnAbility = new(-597709516);
  private static float _maxDuration = 60f; // Default duration
  private static List<PrefabGUID> BuffList { get; set; } = [
    new(-480024072),  // Invunerable
    new(1934061152),  // Deaggro buff
    new(-1527408583), // Root buff
  ];

  private static readonly List<string> _servantDialog = [
    "Hi I'm your carrier.",
    "You can give me your items to store them.",
    "Once you are done, please dismiss me.",
  ];

  public static void Spawn(PlayerData playerData) {
    if (SpawnedServants.ContainsKey(playerData.PlatformId)) {
      return; // Already spawned a servant for this player
    }
    AbilityService.CastAbility(playerData.CharacterEntity, _spawnAbility);

    CreateCoffin(playerData);
  }

  public static void Dismiss(PlayerData playerData) {
    if (!SpawnedServants.TryGetValue(playerData.PlatformId, out var entities) || entities.Count == 0) {
      return; // No servant to dismiss
    }

    var coffin = entities[0];
    var servant = entities[1];

    ActionScheduler.CreateSequence()
      .Then(() => PrepareToLeave(coffin))
      .ThenWait(2)
      .Then(() => EndPhase(coffin, servant))
      .ThenWait(2)
      .Then(() => RemoveServant(servant))
      .Execute();

    SpawnedServants.Remove(playerData.PlatformId);

    foreach (var action in SpawnSequences.GetValueOrDefault(playerData.PlatformId, [])) {
      ActionScheduler.CancelAction(action);
    }
  }

  public static void CreateCoffin(PlayerData playerData) {
    var coffin = UnitSpawnerService.ImmediateSpawn(_coffin, playerData.CharacterEntity.Position(), owner: Entity.Null, lifeTime: _maxDuration);
    var position = playerData.CharacterEntity.Position();
    var servant = CreateServant(playerData, coffin);

    SpawnedServants.Add(playerData.PlatformId, [coffin, servant]);

    TeleportService.TeleportToPosition(coffin, new(position.x, 200, position.z));

    coffin.SetTeam(playerData.CharacterEntity);

    servant.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = coffin;
    });

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

    AfterSpawnScript(coffin, servant, playerData);
  }

  public static Entity CreateServant(PlayerData playerData, Entity coffin) {
    var servant = UnitSpawnerService.ImmediateSpawn(_to_servant, coffin.Position(), owner: playerData.CharacterEntity, lifeTime: -1f);

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

    var teleportDistance = 3f;

    var finalPosition = characterPosition + (aimDirection * teleportDistance);

    TeleportService.TeleportToPosition(servant, finalPosition);

    servant.SetTeam(playerData.CharacterEntity);

    return servant;
  }

  public static void AfterSpawnScript(Entity coffin, Entity servant, PlayerData playerData) {
    var action = ActionScheduler.CreateSequence()
      .ThenWaitFrames(5)
      .Then(() => StartPhase(servant, playerData))
      .ThenWait(2f)
      .Then(() => RunDialogSequence(coffin, playerData))
      .ThenWait(_maxDuration - 8f)
      .Then(() => PrepareToLeave(coffin))
      .ThenWait(2)
      .Then(() => EndPhase(coffin, servant))
      .ThenWait(2)
      .Then(() => RemoveServant(servant))
      .Execute();

    AddAction(playerData, action);
  }

  public static void StartPhase(Entity servant, PlayerData playerData) {
    if (Entity.Null.Equals(servant)) return;

    var inventoryItems = Database.Get<Dictionary<int, int>>(playerData.PlatformId.ToString());

    foreach (var item in inventoryItems) {
      InventoryService.AddItem(servant, new(item.Key), item.Value);
    }

    CastAbilityOnServant(servant, _spawnAbility);
  }

  public static void RunDialogSequence(Entity coffin, PlayerData playerData) {
    var index = 0;

    var action = ActionScheduler.Repeating(() => {
      coffin.With((ref ServantCoffinstation coffinStation) => {
        if (index == _servantDialog.Count) {
          coffinStation.ServantName = new FixedString64Bytes($"{playerData.Name}'s Carrier");
        } else {
          coffinStation.ServantName = new FixedString64Bytes(_servantDialog[index]);
        }
      });

      index++;
    }, 1.5f, _servantDialog.Count + 1);

    AddAction(playerData, action);
  }

  public static void PrepareToLeave(Entity coffin) {
    if (Entity.Null.Equals(coffin)) {
      return;
    }

    coffin.With((ref ServantCoffinstation coffinStation) => {
      coffinStation.ServantName = new FixedString64Bytes("All right, I'm about to head out.");
    });
  }

  public static void EndPhase(Entity coffin, Entity servant) {
    if (Entity.Null.Equals(coffin) || Entity.Null.Equals(servant)) {
      return;
    }

    servant.Remove<Follower>();

    servant.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });

    InventoryService.ClearInventory(servant);

    coffin.With((ref ServantCoffinstation coffinStation) => {
      coffinStation.ServantName = new FixedString64Bytes("See you later!");
    });

    BuffService.TryApplyBuff(servant, _onDespawnVisualBuff);

    CastAbilityOnServant(servant, _despawnAbility);
  }

  public static void AddAction(PlayerData playerData, ActionId action) {
    if (!SpawnSequences.ContainsKey(playerData.PlatformId)) {
      SpawnSequences.Add(playerData.PlatformId, [action]);
    } else SpawnSequences[playerData.PlatformId].Add(action);
  }

  public static void CastAbilityOnServant(Entity entity, PrefabGUID abilityGroup) {
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

  public static void ClearServants() {
    var servants = GameSystems.EntityManager.CreateEntityQuery(
      ComponentType.ReadOnly<ServantData>(),
      ComponentType.ReadOnly<Follower>()
    );

    foreach (var servant in servants.ToEntityArray(Allocator.Temp)) {
      RemoveServant(servant);
    }
  }

  private static void RemoveServant(Entity servant) {
    if (Entity.Null.Equals(servant)) {
      return;
    }

    servant.Remove<Follower>();
    InventoryService.ClearInventory(servant);
    servant.Destroy();
  }
}