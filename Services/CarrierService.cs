using System.Collections.Generic;
using ProjectM;
using ProjectM.Network;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
  private const float MaxDistance = 3f;
  private const float DialogInterval = 2f;
  private const float Height = 221f; // The height serve as a reference for despawning the coffin and servant.
  private const float LegacyHeight = 200f; // Legacy height for compatibility with older data. (Will be removed in future updates)
  private static readonly PrefabGUID[] ServantPermaBuffs = [
    new(-480024072), // Invulnerable Buff
    new(1934061152), // Disable aggro
    new(1360141727)  // Immaterial
  ];

  private static readonly Dictionary<PrefabGUID, float> ServantTempBuffs = new() {
    { new PrefabGUID(-1855386239), 0.2f }, // Blood
    { new PrefabGUID(-2061378836), 3f }    // Heart explosion
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
      .Then(() => RemoveCoffin(coffin))
      .Execute();

    foreach (var action in _spawnSequences.GetValueOrDefault(playerData.PlatformId, [])) {
      ActionScheduler.CancelAction(action);
    }
  }

  public static bool HasServant(ulong platformId) {
    return _spawnedServants.ContainsKey(platformId) && _spawnedServants[platformId].Count > 0;
  }

  public static Entity GetServant(ulong platformId) {
    if (_spawnedServants.TryGetValue(platformId, out var entities) && entities.Count > 1) {
      return entities[1];
    }

    return Entity.Null;
  }

  private static void CreateCoffin(PlayerData playerData) {
    var coffin = UnitSpawnerService.ImmediateSpawn(CoffinPrefab, playerData.CharacterEntity.Position(), owner: Entity.Null, lifeTime: -1);
    var position = playerData.CharacterEntity.Position();
    var servant = CreateServant(playerData, coffin);

    _spawnedServants.Add(playerData.PlatformId, [coffin, servant]);

    TeleportService.TeleportToPosition(coffin, new float3(position.x, Height, position.z));

    coffin.SetTeam(playerData.CharacterEntity);

    ConfigureCoffinServantConnection(coffin, servant, playerData);
    RemoveDisableComponents(coffin);

    AfterSpawnScript(coffin, servant, playerData);
  }

  private static void ConfigureCoffinServantConnection(Entity coffin, Entity servant, PlayerData playerData) {
    servant.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = NetworkedEntity.ServerEntity(coffin);
    });

    coffin.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant = NetworkedEntity.ServerEntity(servant);
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
    LookAtPlayer(servant, playerData.CharacterEntity);

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

    var distance = math.distance(characterPosition, aimPosition);

    var finalPosition = distance <= MaxDistance
      ? aimPosition
      : characterPosition + (MathUtility.GetDirection(characterPosition, aimPosition) * MaxDistance);

    finalPosition.y = characterPosition.y;

    TeleportService.TeleportToPosition(servant, finalPosition);
  }

  private static void AfterSpawnScript(Entity coffin, Entity servant, PlayerData playerData) {
    var action = ActionScheduler.CreateSequence()
      .ThenWaitFrames(10)
      .Then(() => StartPhase(servant, playerData))
      .ThenWait(2f)
      .Then(() => RunDialogSequence(coffin, playerData))
      .ThenWait(MaxDuration - 8f)
      .Then(() => PrepareToLeave(coffin))
      .ThenWait(2f)
      .Then(() => {
        MessageService.Send(playerData.User, "Your ~carrier~ has reached its time limit and will now despawn.".Format());
        EndPhase(coffin, servant);
      })
      .ThenWait(2f)
      .Then(() => RemoveCoffin(coffin))
      .Execute();

    AddAction(playerData, action);
  }

  private static void StartPhase(Entity servant, PlayerData playerData) {
    if (Entity.Null.Equals(servant)) return;
    LoadServantInventory(servant, playerData);
    AbilityService.CastAbility(servant, SpawnAbility);
    var playerPosition = playerData.CharacterEntity.Position();
    var servantPosition = servant.Position();
    TeleportService.TeleportToPosition(servant, new(servantPosition.x, playerPosition.y, servantPosition.z));
  }

  private static void LookAtPlayer(Entity servant, Entity playerEntity) {
    servant.With((ref EntityInput lookAtTarget) => {
      lookAtTarget.SetAllAimPositions(playerEntity.Position());
    });
  }

  private static void LoadServantInventory(Entity servant, PlayerData playerData) {
    Dictionary<int, int> inventoryItems = Database.Get<Dictionary<int, int>>(playerData.PlatformId.ToString()) ?? [];

    foreach (var item in inventoryItems) {
      InventoryService.AddItem(servant, new(item.Key), item.Value);
    }
  }

  private static void RunDialogSequence(Entity coffin, PlayerData playerData) {
    if (Entity.Null.Equals(coffin)) return;
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

    DisableInteraction(servant);
    SetDialog(coffin, FarewellDialog);
    PlayPreDespawnEffects(servant);
  }

  private static void DisableInteraction(Entity servant) {
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

  public static void ClearAll() {
    var coffins = GameSystems.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<ServantCoffinstation>()).ToEntityArray(Allocator.Temp);

    foreach (var coffin in coffins) {
      RemoveCoffin(coffin);
    }

    coffins.Dispose();
  }

  private static void RemoveCoffin(Entity coffin) {
    if (Entity.Null.Equals(coffin) || !coffin.Has<ServantCoffinstation>()) return;

    if (!coffin.Has<LocalTransform>()) return;

    var position = coffin.Read<LocalTransform>().Position;

    if (position.y != Height && position.y != LegacyHeight) return;

    var servant = coffin.Read<ServantCoffinstation>().ConnectedServant._Entity;

    if (Entity.Null.Equals(servant) || !servant.Has<Follower>()) return;

    RemoveServant(servant);

    var coffinBuffBuffer = coffin.ReadBuffer<BuffBuffer>();

    foreach (var buff in coffinBuffBuffer) {
      BuffService.TryRemoveBuff(coffin, buff.PrefabGuid);
    }

    coffin.Destroy();
  }

  private static void RemoveServant(Entity servant) {
    if (Entity.Null.Equals(servant) || !servant.Has<Follower>()) return;

    var userEntity = servant.Read<Follower>().Followed._Value;

    if (Entity.Null.Equals(userEntity) || !userEntity.Has<User>()) return;

    servant.Remove<Follower>();

    _spawnedServants.Remove(userEntity.Read<User>().PlatformId);

    InventoryService.ClearInventory(servant);

    var servantBuffBuffer = servant.ReadBuffer<BuffBuffer>();

    foreach (var buff in servantBuffBuffer) {
      BuffService.TryRemoveBuff(servant, buff.PrefabGuid);
    }

    servant.Destroy();
  }
}