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
using Unity.Mathematics;

namespace ScarletCarrier.Services;

internal static class CarrierService {
  private static Database Database => Plugin.Database;

  private static readonly Dictionary<ulong, List<Entity>> _spawnedServants = [];
  private static readonly Dictionary<ulong, List<ActionId>> _spawnSequences = [];

  private static readonly PrefabGUID CoffinPrefab = new(723455393);
  private static readonly PrefabGUID ServantPrefab = new(-450600397);
  private static readonly PrefabGUID SpawnAbility = new(2072201164);
  private static readonly PrefabGUID DespawnAbility = new(-597709516);
  private static readonly PrefabGUID DespawnVisualBuff = new(1185694153);
  private static readonly PrefabGUID NeutralFaction = new(-1430861195);

  private const float MaxDuration = 60f;
  private const float TeleportDistance = 3f;
  private const float DialogInterval = 2f;

  private static readonly PrefabGUID[] ServantPermaBuffs = [
    new(-480024072), // Invulnerable Buff
    new(1934061152), // Disable aggro
  ];

  private static readonly Dictionary<PrefabGUID, float> ServantTempBuffs = new() {
    { new PrefabGUID(-1855386239), 0.2f }, // Blood
    { new PrefabGUID(-2061378836), 3f } // Heart explosion
  };

  private static readonly string[] GreetingsDialogLines = [
    "Hey there {playerName}!",
    "I'm here to help carry your stuff.",
    "Just hand me whatever you need to store.",
    "Give me the word when you're ready for me to go!"
  ];

  private const string PreparingToLeaveDialog = "All right, I'm about to head out.";
  private const string FarewellDialog = "See you later!";
  private const string CarrierNameFormat = "{playerName}'s Carrier";

  public static void Spawn(PlayerData playerData) {
    if (_spawnedServants.ContainsKey(playerData.PlatformId)) {
      return;
    }

    AbilityService.CastAbility(playerData.CharacterEntity, SpawnAbility);
    CreateCoffin(playerData);
  }

  public static void Dismiss(PlayerData playerData) {
    if (!_spawnedServants.TryGetValue(playerData.PlatformId, out var entities) || entities.Count == 0) {
      return;
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

    _spawnedServants.Remove(playerData.PlatformId);

    foreach (var action in _spawnSequences.GetValueOrDefault(playerData.PlatformId, [])) {
      ActionScheduler.CancelAction(action);
    }
  }
  private static void CreateCoffin(PlayerData playerData) {
    var coffin = UnitSpawnerService.ImmediateSpawn(CoffinPrefab, playerData.CharacterEntity.Position(), owner: Entity.Null, lifeTime: MaxDuration);
    var position = playerData.CharacterEntity.Position();
    var servant = CreateServant(playerData, coffin);

    _spawnedServants.Add(playerData.PlatformId, [coffin, servant]);

    TeleportService.TeleportToPosition(coffin, new float3(position.x, 200, position.z));

    coffin.SetTeam(playerData.CharacterEntity);

    ConfigureCoffinServantConnection(coffin, servant, playerData);
    RemoveDisableComponents(coffin);

    AfterSpawnScript(coffin, servant, playerData);
  }
  private static void ConfigureCoffinServantConnection(Entity coffin, Entity servant, PlayerData playerData) {
    servant.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = coffin;
    });

    coffin.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant._Entity = servant;
      coffinStation.State = ServantCoffinState.ServantAlive;
    });

    SetCarrierName(coffin, playerData.Name);
  }

  private static void RemoveDisableComponents(Entity entity) {
    if (entity.Has<DisableWhenNoPlayersInRange>()) {
      entity.Remove<DisableWhenNoPlayersInRange>();
    }

    if (entity.Has<DisableWhenNoPlayersInRangeOfChunk>()) {
      entity.Remove<DisableWhenNoPlayersInRangeOfChunk>();
    }
  }
  private static Entity CreateServant(PlayerData playerData, Entity coffin) {
    var servant = UnitSpawnerService.ImmediateSpawn(ServantPrefab, coffin.Position(), owner: playerData.CharacterEntity, lifeTime: -1f);

    ApplyServantBuffs(servant);
    ConfigureServantBehavior(servant, playerData);
    PositionServant(servant, playerData);

    return servant;
  }

  private static void ApplyServantBuffs(Entity servant) {
    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(servant, permaBuffGuid);
    }

    foreach (var tempBuff in ServantTempBuffs) {
      BuffService.TryApplyBuff(servant, tempBuff.Key, tempBuff.Value);
    }
  }

  private static void ConfigureServantBehavior(Entity servant, PlayerData playerData) {
    servant.With((ref AggroConsumer aggroConsumer) => {
      aggroConsumer.Active._Value = false;
    });

    servant.With((ref Aggroable aggroable) => {
      aggroable.Value._Value = false;
      aggroable.DistanceFactor._Value = 0f;
      aggroable.AggroFactor._Value = 0f;
    });

    servant.With((ref FactionReference factionReference) => {
      factionReference.FactionGuid._Value = NeutralFaction;
    });

    servant.With((ref Follower follower) => {
      follower.Followed._Value = playerData.UserEntity;
    });

    servant.Remove<ServantEquipment>();

    RemoveDisableComponents(servant);
    servant.SetTeam(playerData.CharacterEntity);
  }

  private static void PositionServant(Entity servant, PlayerData playerData) {
    var characterPosition = playerData.CharacterEntity.Position();
    var aimPosition = playerData.CharacterEntity.Read<EntityAimData>().AimPosition;
    var aimDirection = MathUtility.GetDirection(characterPosition, aimPosition);
    var finalPosition = characterPosition + (aimDirection * TeleportDistance);

    TeleportService.TeleportToPosition(servant, finalPosition);
  }
  private static void AfterSpawnScript(Entity coffin, Entity servant, PlayerData playerData) {
    var action = ActionScheduler.CreateSequence()
      .ThenWaitFrames(5)
      .Then(() => StartPhase(servant, playerData))
      .ThenWait(2f)
      .Then(() => RunDialogSequence(coffin, playerData))
      .ThenWait(MaxDuration - 8f)
      .Then(() => PrepareToLeave(coffin))
      .ThenWait(2)
      .Then(() => EndPhase(coffin, servant))
      .ThenWait(2)
      .Then(() => RemoveServant(servant))
      .Execute();

    AddAction(playerData, action);
  }

  private static void StartPhase(Entity servant, PlayerData playerData) {
    if (Entity.Null.Equals(servant)) return;

    LoadServantInventory(servant, playerData);
    AbilityService.CastAbility(servant, SpawnAbility);
  }

  private static void LoadServantInventory(Entity servant, PlayerData playerData) {
    var inventoryItems = Database.Get<Dictionary<int, int>>(playerData.PlatformId.ToString());

    foreach (var item in inventoryItems) {
      InventoryService.AddItem(servant, new(item.Key), item.Value);
    }
  }

  private static void RunDialogSequence(Entity coffin, PlayerData playerData) {
    var index = 0;

    var action = ActionScheduler.Repeating(() => {
      if (index == GreetingsDialogLines.Length) {
        SetCarrierName(coffin, playerData.Name);
      } else {
        SetDialog(coffin, GreetingsDialogLines[index].Replace("{playerName}", playerData.Name));
      }

      index++;
    }, DialogInterval, GreetingsDialogLines.Length + 1);

    AddAction(playerData, action);
  }
  private static void PrepareToLeave(Entity coffin) {
    if (Entity.Null.Equals(coffin)) return;

    SetDialog(coffin, PreparingToLeaveDialog);
  }

  private static void EndPhase(Entity coffin, Entity servant) {
    if (Entity.Null.Equals(coffin) || Entity.Null.Equals(servant)) return;

    CleanupServant(servant);
    SetDialog(coffin, FarewellDialog);
    PlayPreDespawnEffects(servant);
  }

  private static void CleanupServant(Entity servant) {
    servant.Remove<Follower>();
    InventoryService.ClearInventory(servant);

    servant.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });
  }

  private static void SetDialog(Entity coffin, string message) {
    coffin.With((ref ServantCoffinstation coffinStation) => {
      coffinStation.ServantName = new FixedString64Bytes(message);
    });
  }

  private static void SetCarrierName(Entity coffin, string playerName) {
    SetDialog(coffin, CarrierNameFormat.Replace("{playerName}", playerName));
  }

  private static void PlayPreDespawnEffects(Entity servant) {
    BuffService.TryApplyBuff(servant, DespawnVisualBuff);
    AbilityService.CastAbility(servant, DespawnAbility);
  }

  private static void AddAction(PlayerData playerData, ActionId action) {
    if (!_spawnSequences.ContainsKey(playerData.PlatformId)) {
      _spawnSequences.Add(playerData.PlatformId, [action]);
    } else {
      _spawnSequences[playerData.PlatformId].Add(action);
    }
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
    if (Entity.Null.Equals(servant)) return;

    var userEntity = servant.Read<Follower>().Followed._Value;

    _spawnedServants.Remove(userEntity.Read<User>().PlatformId);

    servant.Remove<Follower>();
    InventoryService.ClearInventory(servant);
    servant.Destroy();
  }
}