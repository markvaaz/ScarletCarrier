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

namespace ScarletCarrier.Models;

internal class Carrier {
  private const float MaxSpawnDistance = 3f;
  private const float MaxTeleportDistance = 15f;
  public const float LegacyHeight = 221f;
  public const float Height = 215f;
  private const int MaxPositionHistory = 8;
  private const float ServantSpeedMultiplier = 0.95f;
  public const string Id = "__ScarletCarrier1.0__";
  public const string LegacyId = "__ScarletCarrier__";

  private static readonly PrefabGUID CoffinPrefab = new(723455393);
  private static readonly PrefabGUID DefaultServantPrefab = new(2142021685);
  private static readonly PrefabGUID NeutralFaction = new(-1430861195);
  private static readonly PrefabGUID PlayerMountedBuff = new(-978792376);
  private static readonly PrefabGUID SpawnPlayerBuff = new(-1879665573);

  private static readonly PrefabGUID[] ServantPermaBuffs = [
    new(-480024072), // Invulnerable Buff
    new(1934061152), // Disable aggro
    new(1360141727)  // Immaterial
  ];

  private const string NameFormat = "{playerName}'s Carrier";
  public Entity CoffinEntity { get; private set; }
  public Entity ServantEntity { get; private set; }
  public PlayerData Owner { get; private set; }
  public float3 DismissPosition = new(-359, 15, -280);
  public ulong PlatformId => Owner.PlatformId;
  private List<float3> _positionHistory = [];
  private ActionId _followAction;
  private float3 _currentTargetPosition;
  private bool _isFollowing;
  public bool IsFollowing => _isFollowing;
  public bool Busy { get; private set; } = false;
  public float BusyDuration = 1.5f;
  public static int MaxDays => Plugin.Settings.Get<int>("ExpireDays") * 24 * 60 * 60;

  public Carrier(PlayerData ownerData) {
    Owner = ownerData;
  }

  public Carrier(Entity coffin, Entity servant, PlayerData ownerData) {
    CoffinEntity = coffin;
    ServantEntity = servant;
    Owner = ownerData;
    BindCoffinServant();
  }

  public void Call() {
    if (Busy) return;
    SetAsBusy();
    ActionScheduler.Delayed(() => Busy = false, BusyDuration);
    SetName(Owner.Name);

    if (BuffService.HasBuff(ServantEntity, CarrierState.Hidden)) {
      BuffService.TryRemoveBuff(ServantEntity, CarrierState.Hidden);
    }

    if (!BuffService.HasBuff(Owner.CharacterEntity, SpawnPlayerBuff)) {
      BuffService.TryApplyBuff(Owner.CharacterEntity, SpawnPlayerBuff);
    }

    if (!BuffService.HasBuff(ServantEntity, CarrierState.Spawning)) {
      BuffService.TryApplyBuff(ServantEntity, CarrierState.Spawning, BusyDuration);
    }

    ResetLifeTime();
    PositionServantOnPlayerAim();
    LookAtPlayer();
    LoadServantInventoryFromLegacy();
  }

  public void ResetLifeTime() {
    ServantEntity.AddWith((ref LifeTime lifetime) => {
      lifetime.Duration = MaxDays;
      lifetime.EndAction = LifeTimeEndAction.Destroy;
    });

    CoffinEntity.AddWith((ref LifeTime lifetime) => {
      lifetime.Duration = MaxDays;
      lifetime.EndAction = LifeTimeEndAction.Destroy;
    });

    ServantEntity.With((ref Age age) => {
      age.Value = 0f;
    });

    CoffinEntity.With((ref Age age) => {
      age.Value = 0f;
    });
  }

  public void Dismiss() {
    if (Busy) return;

    SetAsBusy();
    StopFollow();
    if (!BuffService.HasBuff(ServantEntity, CarrierState.Leaving)) {
      BuffService.TryApplyBuff(ServantEntity, CarrierState.Leaving, BusyDuration);
    }
    if (!BuffService.HasBuff(ServantEntity, CarrierState.Hidden)) {
      BuffService.TryApplyBuff(ServantEntity, CarrierState.Hidden, -1);
    }
    ActionScheduler.DelayedFrames(Hide, 3);
  }

  private void SetAsBusy() {
    Busy = true;
    ActionScheduler.Delayed(() => {
      Busy = false;
    }, BusyDuration);
  }

  public bool IsValid() {
    return !Entity.Null.Equals(CoffinEntity) && !Entity.Null.Equals(ServantEntity) && CoffinEntity.Exists() && ServantEntity.Exists();
  }

  private void SetName(string playerName) {
    CoffinEntity.With((ref ServantCoffinstation coffinStation) => {
      coffinStation.ServantName = new FixedString64Bytes(NameFormat.Replace("{playerName}", playerName));
    });
  }

  public void Create() {
    CreateCoffin();
    CreateServant();
    BindCoffinServant();
    RemoveDisableComponents(CoffinEntity);
    RemoveDisableComponents(ServantEntity);
  }

  private void CreateCoffin() {
    CoffinEntity = UnitSpawnerService.ImmediateSpawn(CoffinPrefab, Owner.Position + new float3(0, Height, 0), owner: Owner.CharacterEntity, lifeTime: MaxDays);
    CoffinEntity.AddWith((ref NameableInteractable nameable) => {
      nameable.Name = new FixedString64Bytes(Id);
    });
    CoffinEntity.AddWith((ref EntityOwner owner) => {
      owner.Owner = Owner.CharacterEntity;
    });
    CoffinEntity.SetTeam(Owner.CharacterEntity);
  }

  private void CreateServant() {
    var customServantPrefab = GetServantAppearance();

    ServantEntity = UnitSpawnerService.ImmediateSpawn(customServantPrefab, Owner.Position, owner: Owner.CharacterEntity, lifeTime: MaxDays);
    ConfigureServant();
  }

  private PrefabGUID GetServantAppearance() {
    var customAppearancesList = Plugin.Database.Get<Dictionary<ulong, string>>(CarrierService.CustomAppearances) ?? [];
    var customServantPrefab = DefaultServantPrefab;
    if (customAppearancesList.TryGetValue(PlatformId, out var guidHash) && PrefabGUID.TryParse(guidHash, out var customServant)) {
      customServantPrefab = customServant;
    }
    return customServantPrefab;
  }

  public void SwapServantAppearance() {
    if (ServantEntity.IsNull() || !ServantEntity.Exists()) {
      Log.Error("Current servant entity is invalid.");
      return;
    }


    StopFollow();

    var customServantPrefab = GetServantAppearance();
    var oldServantPosition = ServantEntity.Position();

    TeleportService.TeleportToPosition(CoffinEntity, oldServantPosition + new float3(0, Height, 0));

    var newServant = UnitSpawnerService.ImmediateSpawn(customServantPrefab, oldServantPosition, 0f, 0f, owner: Owner.CharacterEntity, lifeTime: MaxDays);

    var inventoryItems = InventoryService.GetInventoryItems(ServantEntity);
    var oldServantEquipment = ServantEntity.Read<ServantEquipment>();
    var newServantEquipment = newServant.Read<ServantEquipment>();

    ActionScheduler.CreateSequence()
      .ThenWaitFrames(5)
      .Then(() => {
        for (int i = 0; i < inventoryItems.Length; i++) {
          InventoryService.TransferItem(ServantEntity, newServant, i);
        }

        newServant.Write(oldServantEquipment);
        ServantEntity.Write(new ServantEquipment());

        ServantEntity.Destroy();
        ServantEntity = newServant;

        TeleportService.TeleportToPosition(CoffinEntity, oldServantPosition + new float3(0, Height, 0));
        if (!BuffService.HasBuff(ServantEntity, CarrierState.Spawning)) {
          BuffService.TryApplyBuff(ServantEntity, CarrierState.Spawning, 2f);
        }
        LookAtPlayer();
        ConfigureServant();
        BindCoffinServant();
      }).Execute();
  }

  private void ConfigureServant() {
    ServantEntity.AddWith((ref NameableInteractable nameable) => {
      nameable.Name = new FixedString64Bytes(Id);
    });

    foreach (var permaBuffGuid in ServantPermaBuffs) {
      BuffService.TryApplyBuff(ServantEntity, permaBuffGuid, -1);
    }

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
      follower.Followed = new ModifiableEntity(Owner.UserEntity);
    });

    ServantEntity.SetTeam(Owner.CharacterEntity);
  }

  private void BindCoffinServant() {
    ServantEntity.AddWith((ref ServantConnectedCoffin servantConnectedCoffin) => {
      servantConnectedCoffin.CoffinEntity = NetworkedEntity.ServerEntity(CoffinEntity);
    });

    CoffinEntity.AddWith((ref ServantCoffinstation coffinStation) => {
      coffinStation.ConnectedServant = NetworkedEntity.ServerEntity(ServantEntity);
      coffinStation.State = ServantCoffinState.ServantAlive;
    });

    SetName(Owner.Name);
  }

  private void RemoveDisableComponents(Entity entity) {
    if (entity.Has<DisableWhenNoPlayersInRange>()) {
      entity.Remove<DisableWhenNoPlayersInRange>();
    }

    if (entity.Has<DisableWhenNoPlayersInRangeOfChunk>()) {
      entity.Remove<DisableWhenNoPlayersInRangeOfChunk>();
    }
  }

  private void PositionServantOnPlayerAim() {
    var characterPosition = Owner.Position;
    var aimPosition = Owner.CharacterEntity.Read<EntityAimData>().AimPosition;

    var distance = math.distance(characterPosition, aimPosition);

    var finalPosition = distance <= MaxSpawnDistance
      ? aimPosition
      : characterPosition + (MathUtility.GetDirection(characterPosition, aimPosition) * MaxSpawnDistance);

    finalPosition.y = characterPosition.y;
    TeleportToPosition(finalPosition);
  }

  private void LoadServantInventoryFromLegacy() {
    if (!Plugin.Database.Has(PlatformId.ToString())) return;

    Dictionary<int, int> inventoryItems = Plugin.Database.Get<Dictionary<int, int>>(PlatformId.ToString()) ?? [];

    foreach (var item in inventoryItems) {
      InventoryService.AddItem(ServantEntity, new(item.Key), item.Value);
    }

    Plugin.Database.Delete(PlatformId.ToString());
  }

  public void StartFollow() {
    if (_isFollowing) return;

    _positionHistory.Clear();
    _isFollowing = true;

    _followAction = ActionScheduler.OncePerFrame(end => {
      if (ServantEntity.IsNull() || !ServantEntity.Exists() || BuffService.HasBuff(ServantEntity, CarrierState.Hidden)) {
        end();
        _isFollowing = false;
        return;
      }
      UpdatePlayerPositionHistory();
      FollowPlayer();
    });
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
  }

  public void TeleportToPosition(float3 position) {
    if (Entity.Null.Equals(ServantEntity) || !ServantEntity.Exists()) return;
    TeleportService.TeleportToPosition(ServantEntity, position);
    TeleportService.TeleportToPosition(CoffinEntity, position + new float3(0, Height, 0));
  }

  public void Hide() {
    SetName(Owner.Name);
    TeleportToPosition(DismissPosition);
  }

  private void LookAtPlayer() {
    ServantEntity.With((ref EntityInput lookAtTarget) => {
      lookAtTarget.SetAllAimPositions(Owner.Position);
    });
  }

  public void ToggleFollow() {
    if (_isFollowing) {
      StopFollow();
    } else {
      StartFollow();
    }
  }

  private void FollowPlayer() {
    var playerSpeed = Owner.CharacterEntity.Read<MoveVelocity>().MoveVelocityMagnitude;
    var servantPos = ServantEntity.Position();
    var playerPos = Owner.CharacterEntity.Read<PlayerLastValidPosition>().LastValidPosition;
    var distanceToPlayer = math.distance(servantPos, playerPos);
    var heightDifference = math.abs(servantPos.y - playerPos.y);

    if (distanceToPlayer > MaxTeleportDistance || heightDifference > 4f) {
      TeleportToPosition(playerPos);
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

    if (BuffService.HasBuff(Owner.CharacterEntity, PlayerMountedBuff)) {
      playerSpeed *= 5f;
    }

    ServantEntity.With((ref Movement movement) => {
      movement.Speed = new ModifiableFloat(playerSpeed * ServantSpeedMultiplier);
    });

    TeleportService.TeleportToPosition(CoffinEntity, ServantEntity.Position() + new float3(0, Height, 0));
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

    var playerPosition = Owner.CharacterEntity.Read<PlayerLastValidPosition>().LastValidPosition;

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
    var currentPos = Owner.CharacterEntity.Read<PlayerLastValidPosition>().LastValidPosition;

    if (_positionHistory.Count == 0 || math.distance(_positionHistory[^1], currentPos) > 0.5f) {
      _positionHistory.Add(currentPos);

      if (_positionHistory.Count > MaxPositionHistory) {
        _positionHistory.RemoveAt(0);
      }
    }
  }
}

internal class CarrierState {
  public static PrefabGUID Leaving = new(826269213);
  public static PrefabGUID Hidden = new(-1144825660);
  public static PrefabGUID Spawning = new(1497959076);
}