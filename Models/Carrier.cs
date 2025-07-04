using System;
using System.Collections.Generic;
using ProjectM;
using ScarletCore;
using ScarletCore.Data;
using ScarletCore.Services;
using ScarletCore.Systems;
using ScarletCore.Utils;
using ScarletCarrier.Services;
using Stunlock.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Animation;

namespace ScarletCarrier.Models;

internal class Carrier(PlayerData ownerData) {
  private static Database Database => Plugin.Database;

  // Constants
  private const float MaxSpawnDistance = 3f;
  private const float MaxTeleportDistance = 15f;
  private const float DialogInterval = 2f;
  public const float Height = 221f;
  private const int MaxPositionHistory = 8;
  private const float ServantSpeedMultiplier = 0.95f;
  public const string Id = "__ScarletCarrier__";

  // Prefabs and data
  private static readonly PrefabGUID CoffinPrefab = new(723455393);
  private static readonly PrefabGUID DefaultServantPrefab = new(2142021685);
  private static readonly PrefabGUID SpawnAbility = new(2072201164);
  private static readonly PrefabGUID DespawnAbility = new(-597709516);
  private static readonly PrefabGUID DespawnVisualBuff = new(1185694153);
  private static readonly PrefabGUID NeutralFaction = new(-1430861195);
  private static readonly PrefabGUID MountedBuff = new(-978792376);

  private static readonly PrefabGUID[] ServantPermaBuffs = [
    new(-480024072), // Invulnerable Buff
    new(1934061152), // Disable aggro
    new(1360141727)  // Immaterial
  ];

  private static readonly Dictionary<PrefabGUID, float> ServantTempBuffs = new() {
    { new PrefabGUID(-1855386239), 0.2f }, // Blood
    { new PrefabGUID(-2061378836), 3f }    // Heart explosion
  };

  // Dialog constants
  private static readonly string[] GreetingsDialogLines = [
    "Hey there {playerName}!",
    "I'm here to help carry your stuff.",
    "Just hand me whatever you need to store.",
    "Give me the word when you're ready for me to go!"
  ];

  private const string PreparingToLeaveDialog = "All right, I'm about to head out.";
  private const string FarewellDialog = "See you later!";
  private const string StartFollowDialog = "Right behind you!";
  private const string StopFollowDialog = "I'll wait here for you.";
  private const string CarrierNameFormat = "{playerName}'s Carrier";

  // Instance properties
  public Entity CoffinEntity { get; private set; }
  public Entity ServantEntity { get; private set; }
  public PlayerData OwnerData { get; private set; } = ownerData;
  public ulong PlatformId => OwnerData.PlatformId;

  // Follow system state
  private List<float3> _positionHistory = [];
  private ActionId _followAction;
  private float3 _currentTargetPosition;
  private bool _isFollowing;

  // Action management
  private List<ActionId> _activeActions = [];
  private ActionId _spawnSequenceAction;
  private ActionId _dialogSequenceAction;
  private bool _dismissInProgress;

  public bool IsFollowing => _isFollowing;
  public bool IsDismissInProgress => _dismissInProgress;

  public void Create() {
    var position = OwnerData.Position;

    CoffinEntity = UnitSpawnerService.ImmediateSpawn(CoffinPrefab, position, owner: OwnerData.CharacterEntity, lifeTime: -1);

    CoffinEntity.AddWith((ref NameableInteractable nameable) => {
      nameable.Name = new FixedString64Bytes(Id);
    });

    CreateServant();
    TeleportService.TeleportToPosition(CoffinEntity, new float3(position.x, Height, position.z));
    CoffinEntity.SetTeam(OwnerData.CharacterEntity);
    ConfigureCoffinServantConnection();
    RemoveDisableComponents(CoffinEntity);
  }

  private void CreateServant() {
    var customAppearancesList = Database.Get<Dictionary<ulong, string>>(CarrierService.CustomAppearances) ?? [];
    var customServantPrefab = DefaultServantPrefab;

    if (customAppearancesList.TryGetValue(PlatformId, out var guidHash) && PrefabGUID.TryParse(guidHash, out var customServant)) {
      customServantPrefab = customServant;
    }

    ServantEntity = UnitSpawnerService.ImmediateSpawn(customServantPrefab, OwnerData.Position, owner: OwnerData.CharacterEntity, lifeTime: -1f);

    ServantEntity.AddWith((ref NameableInteractable nameable) => {
      nameable.Name = new FixedString64Bytes(Id);
    });

    ApplyServantBuffs();
    ConfigureServantBehavior();
    PositionServant();
    LookAtPlayer();
  }

  private void ConfigureCoffinServantConnection() {
    ServantEntity.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = NetworkedEntity.ServerEntity(CoffinEntity);
    });

    CoffinEntity.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant = NetworkedEntity.ServerEntity(ServantEntity);
      coffinStation.State = ServantCoffinState.ServantAlive;
    });

    SetCarrierName(OwnerData.Name);
  }

  private void RemoveDisableComponents(Entity entity) {
    if (entity.Has<DisableWhenNoPlayersInRange>()) {
      entity.Remove<DisableWhenNoPlayersInRange>();
    }

    if (entity.Has<DisableWhenNoPlayersInRangeOfChunk>()) {
      entity.Remove<DisableWhenNoPlayersInRangeOfChunk>();
    }
  }

  private void ApplyServantBuffs() {
    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(ServantEntity, permaBuffGuid);
    }

    foreach (var tempBuff in ServantTempBuffs) {
      BuffService.TryApplyBuff(ServantEntity, tempBuff.Key, tempBuff.Value);
    }
  }

  private void ConfigureServantBehavior() {
    ServantEntity.With((ref AggroConsumer aggroConsumer) => {
      aggroConsumer.Active = new ModifiableBool(false);
    });

    ServantEntity.With((ref Aggroable aggroable) => {
      aggroable.Value = new ModifiableBool(false);
      aggroable.DistanceFactor = new ModifiableFloat(0f);
      aggroable.AggroFactor = new ModifiableFloat(0f);
    });

    ServantEntity.With((ref FactionReference factionReference) => {
      factionReference.FactionGuid = new ModifiablePrefabGUID(NeutralFaction);
    });

    ServantEntity.With((ref Follower follower) => {
      follower.Followed = new ModifiableEntity(OwnerData.UserEntity);
    });

    RemoveDisableComponents(ServantEntity);
    ServantEntity.SetTeam(OwnerData.CharacterEntity);
  }

  private void PositionServant() {
    var characterPosition = OwnerData.Position;
    var aimPosition = OwnerData.CharacterEntity.Read<EntityAimData>().AimPosition;

    var distance = math.distance(characterPosition, aimPosition);

    var finalPosition = distance <= MaxSpawnDistance
      ? aimPosition
      : characterPosition + (MathUtility.GetDirection(characterPosition, aimPosition) * MaxSpawnDistance);

    finalPosition.y = characterPosition.y;

    TeleportService.TeleportToPosition(ServantEntity, finalPosition);
  }

  private void LookAtPlayer() {
    ServantEntity.With((ref EntityInput lookAtTarget) => {
      lookAtTarget.SetAllAimPositions(OwnerData.CharacterEntity.Position());
    });
  }

  public void StartPhase() {
    if (Entity.Null.Equals(ServantEntity)) return;
    LoadServantInventory();
    AbilityService.CastAbility(ServantEntity, SpawnAbility);
    var playerPosition = OwnerData.CharacterEntity.Position();
    var servantPosition = ServantEntity.Position();
    TeleportService.TeleportToPosition(ServantEntity, new(servantPosition.x, playerPosition.y, servantPosition.z));
  }

  private void LoadServantInventory() {
    Dictionary<int, int> inventoryItems = Database.Get<Dictionary<int, int>>(PlatformId.ToString()) ?? [];

    foreach (var item in inventoryItems) {
      InventoryService.AddItem(ServantEntity, new(item.Key), item.Value);
    }
  }

  public void RunDialogSequence() {
    if (Entity.Null.Equals(CoffinEntity)) return;

    StopCurrentDialog();

    var index = 0;

    _dialogSequenceAction = ActionScheduler.Repeating(() => {
      if (index == GreetingsDialogLines.Length) {
        SetCarrierName(OwnerData.Name);
      } else {
        SetDialog(GreetingsDialogLines[index].Replace("{playerName}", OwnerData.Name));
      }

      index++;
    }, DialogInterval, GreetingsDialogLines.Length + 1);
  }

  public void PrepareToLeave() {
    if (Entity.Null.Equals(CoffinEntity)) return;

    StopCurrentDialog();

    _dialogSequenceAction = ActionScheduler.CreateSequence()
      .Then(() => SetDialog(PreparingToLeaveDialog))
      .Execute();
  }

  public void EndPhase() {
    if (Entity.Null.Equals(CoffinEntity) || Entity.Null.Equals(ServantEntity)) return;

    StopCurrentDialog();

    _dialogSequenceAction = ActionScheduler.CreateSequence()
      .Then(() => {
        DisableInteraction();
        SetDialog(FarewellDialog);
        PlayPreDespawnEffects();
      })
      .Execute();
  }

  private void StopCurrentDialog() {
    if (_dialogSequenceAction != default) {
      ActionScheduler.CancelAction(_dialogSequenceAction);
      _dialogSequenceAction = default;
    }
  }

  public void StopDialog() {
    StopCurrentDialog();
  }

  public void ShowCustomDialogWithNameRestore(string message, float dialogDuration = 1.5f) {
    if (Entity.Null.Equals(CoffinEntity)) return;

    StopCurrentDialog();

    _dialogSequenceAction = ActionScheduler.CreateSequence()
      .Then(() => SetDialog(message))
      .ThenWait(dialogDuration)
      .Then(() => SetCarrierName(OwnerData.Name))
      .Execute();
  }

  public void StartFollow() {
    if (_isFollowing || _dismissInProgress) return;

    _positionHistory.Clear();
    _isFollowing = true;

    _followAction = ActionScheduler.OncePerFrame(end => {
      if (ServantEntity.IsNull() || !ServantEntity.Exists()) {
        end();
        _isFollowing = false;
        return;
      }

      UpdatePlayerPositionHistory();
      FollowPlayer();
    });

    ShowCustomDialogWithNameRestore(StartFollowDialog);
  }

  public void StopFollow() {
    if (!_isFollowing) return;

    _isFollowing = false;
    if (_followAction != default) {
      ActionScheduler.CancelAction(_followAction);
      _followAction = default;
    }

    StopSeekingPosition();
    _positionHistory.Clear();

    ShowCustomDialogWithNameRestore(StopFollowDialog);
  }

  public void TeleportToPlayer() {
    var playerPosition = OwnerData.CharacterEntity.Position();
    TeleportService.TeleportToPosition(ServantEntity, playerPosition);
  }

  public void ToggleFollow() {
    if (_isFollowing) {
      StopFollow();
    } else {
      StartFollow();
    }
  }

  private void FollowPlayer() {
    var playerSpeed = OwnerData.CharacterEntity.Read<MoveVelocity>().MoveVelocityMagnitude;
    var servantPos = ServantEntity.Position();
    var playerPos = OwnerData.CharacterEntity.Read<PlayerLastValidPosition>().LastValidPosition;
    var distanceToPlayer = math.distance(servantPos, playerPos);
    var heightDifference = math.abs(servantPos.y - playerPos.y);

    if (distanceToPlayer > MaxTeleportDistance || heightDifference > 4f) {
      TeleportService.TeleportToPosition(ServantEntity, playerPos);
      return;
    }

    var targetPos = GetTargetPositionFromHistory(servantPos);

    if (math.distance(_currentTargetPosition, targetPos) > 0.1f) {
      _currentTargetPosition = targetPos;
      SetSeekingPosition(targetPos);
    }

    if (playerSpeed < 2) {
      playerSpeed = 2f;
    }

    if (BuffService.HasBuff(OwnerData.CharacterEntity, MountedBuff)) {
      playerSpeed *= 5f;
    }

    ServantEntity.With((ref Movement movement) => {
      movement.Speed = new ModifiableFloat(playerSpeed * ServantSpeedMultiplier);
    });
  }

  private void SetSeekingPosition(float3 targetPos) {
    ServantEntity.With((ref AiMove_Server move) => {
      move.ForceLookAtTarget = AiForceLookAtTarget.Approach;
      move.IsSeekingGoalPosition = true;
      move.SeekOutwards = false;
      move.TargettingMode = AiTargettingMode.Position;
      move.TargetPosition = new(targetPos.x, targetPos.z);
      move.MovePattern = AiMovePattern.Approach;
      move.FreezeRotationWhenStationary = true;
    });
  }

  private void StopSeekingPosition() {
    ServantEntity.With((ref AiMove_Server move) => {
      move.IsSeekingGoalPosition = false;
      move.TargettingMode = AiTargettingMode.None;
      move.ForceLookAtTarget = AiForceLookAtTarget.None;
    });
  }

  private float3 GetTargetPositionFromHistory(float3 servantPosition) {
    if (_positionHistory.Count == 0) {
      return servantPosition;
    }

    var playerPosition = OwnerData.CharacterEntity.Read<PlayerLastValidPosition>().LastValidPosition;

    if (math.distance(servantPosition, playerPosition) <= MaxSpawnDistance) {
      _positionHistory.Clear();
      return servantPosition;
    }

    while (_positionHistory.Count > 0 && math.distance(servantPosition, _positionHistory[0]) <= 0.2f) {
      _positionHistory.RemoveAt(0);
    }

    if (_positionHistory.Count == 0) {
      return servantPosition;
    }

    return _positionHistory[0];
  }

  private void UpdatePlayerPositionHistory() {
    var currentPos = OwnerData.CharacterEntity.Read<PlayerLastValidPosition>().LastValidPosition;

    if (_positionHistory.Count == 0 || math.distance(_positionHistory[^1], currentPos) > 0.5f) {
      _positionHistory.Add(currentPos);

      if (_positionHistory.Count > MaxPositionHistory) {
        _positionHistory.RemoveAt(0);
      }
    }
  }

  public void ClearInventory() {
    if (InventoryUtilities.TryGetInventoryEntity(GameSystems.EntityManager, ServantEntity, out var inventoryEntity)) {
      var items = InventoryService.GetInventoryItems(ServantEntity);
      var sgm = GameSystems.ServerGameManager;

      foreach (var item in items) {
        sgm.TryRemoveInventoryItem(inventoryEntity, item.ItemType, 999999999);
      }
    }
  }

  public void Destroy() {
    CancelAllActions();
    RemoveCoffin();
    RemoveServant();
  }

  private void RemoveCoffin() {
    if (CoffinEntity.IsNull() || !CoffinEntity.Exists()) return;

    var coffinBuffBuffer = CoffinEntity.ReadBuffer<BuffBuffer>();

    foreach (var buff in coffinBuffBuffer) {
      BuffService.TryRemoveBuff(CoffinEntity, buff.PrefabGuid);
    }

    CoffinEntity.Destroy();
    CoffinEntity = Entity.Null;
  }

  private void RemoveServant() {
    if (ServantEntity.IsNull() || !ServantEntity.Exists()) return;

    ServantEntity.Remove<Follower>();
    ClearInventory();

    var servantBuffBuffer = ServantEntity.ReadBuffer<BuffBuffer>();

    foreach (var buff in servantBuffBuffer) {
      BuffService.TryRemoveBuff(ServantEntity, buff.PrefabGuid);
    }

    ServantEntity.Destroy();
    ServantEntity = Entity.Null;
  }

  public bool IsValid() {
    return !Entity.Null.Equals(CoffinEntity) && !Entity.Null.Equals(ServantEntity) && CoffinEntity.Exists() && ServantEntity.Exists();
  }

  // Action management methods
  public void AddAction(ActionId action) {
    _activeActions.Add(action);
  }

  public void CancelAllActions() {
    foreach (var action in _activeActions) {
      ActionScheduler.CancelAction(action);
    }
    _activeActions.Clear();

    if (_spawnSequenceAction != default) {
      ActionScheduler.CancelAction(_spawnSequenceAction);
      _spawnSequenceAction = default;
    }

    if (_dialogSequenceAction != default) {
      ActionScheduler.CancelAction(_dialogSequenceAction);
      _dialogSequenceAction = default;
    }

    if (_followAction != default) {
      ActionScheduler.CancelAction(_followAction);
      _followAction = default;
    }
  }

  public ActionId CreateSpawnSequence() {
    _spawnSequenceAction = ActionScheduler.CreateSequence()
      .ThenWaitFrames(10)
      .Then(StartPhase)
      .ThenWait(2f)
      .Then(RunDialogSequence)
      .Execute();

    return _spawnSequenceAction;
  }

  public ActionId CreateDismissSequence(Action onDismissComplete = null) {
    if (_dismissInProgress) return default;

    _dismissInProgress = true;

    var dismissAction = ActionScheduler.CreateSequence()
      .Then(PrepareToLeave)
      .ThenWait(2)
      .Then(EndPhase)
      .ThenWait(3)
      .Then(() => {
        Destroy();
        onDismissComplete();
      })
      .Execute();

    AddAction(dismissAction);
    return dismissAction;
  }

  // Private helper methods for dialog and interaction management
  private void SetCarrierName(string playerName) {
    SetDialog(CarrierNameFormat.Replace("{playerName}", playerName));
  }

  private void SetDialog(string message) {
    CoffinEntity.With((ref ServantCoffinstation coffinStation) => {
      coffinStation.ServantName = new FixedString64Bytes(message);
    });
  }

  private void DisableInteraction() {
    ServantEntity.With((ref Interactable interactable) => {
      interactable.Disabled = true;
    });
  }

  private void PlayPreDespawnEffects() {
    BuffService.TryApplyBuff(ServantEntity, DespawnVisualBuff);
    AbilityService.CastAbility(ServantEntity, DespawnAbility);
  }
}
