using System.Collections.Generic;
using ProjectM;
using ProjectM.CastleBuilding;
using ScarletCore;
using ScarletCore.Systems;
using Stunlock.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ScarletCarrier.Services;


// Thx odjit for the methods :D
internal static class CleanupService {
  public static GenerateCastleSystem GenerateCastleSystem => GameSystems.Server.GetOrCreateSystemManaged<GenerateCastleSystem>();
  public static void ClearEntitiesInRadius(float2 center, float radius) {
    var entities = GetAllEntitiesInRadius<PrefabGUID>(center, radius);

    foreach (var entity in entities) {
      if (!entity.Has<PrefabGUID>()) continue;

      entity.Destroy();
    }
  }

  public static IEnumerable<Entity> GetAllEntitiesInRadius<T>(float2 center, float radius) {
    var spatialData = GenerateCastleSystem._TileModelLookupSystemData;
    var tileModelSpatialLookupRO = spatialData.GetSpatialLookupReadOnlyAndComplete(GenerateCastleSystem);

    var gridPosMin = ConvertPosToTileGrid(center - radius);
    var gridPosMax = ConvertPosToTileGrid(center + radius);
    var bounds = new BoundsMinMax(Mathf.FloorToInt(gridPosMin.x), Mathf.FloorToInt(gridPosMin.y), Mathf.CeilToInt(gridPosMax.x), Mathf.CeilToInt(gridPosMax.y));

    var entities = tileModelSpatialLookupRO.GetEntities(ref bounds, TileType.All);
    foreach (var entity in entities) {
      if (!entity.Has<PrefabGUID>()) continue;
      if (!entity.Has<Translation>()) continue;
      var pos = entity.Read<Translation>().Value;
      if (math.distance(center, pos.xz) <= radius) {
        yield return entity;
      }
    }
    entities.Dispose();
  }

  public static float2 ConvertPosToTileGrid(float2 pos) {
    return new float2(Mathf.FloorToInt(pos.x * 2) + 6400, Mathf.FloorToInt(pos.y * 2) + 6400);
  }

  public static float3 ConvertPosToTileGrid(float3 pos) {
    return new float3(Mathf.FloorToInt(pos.x * 2) + 6400, pos.y, Mathf.FloorToInt(pos.z * 2) + 6400);
  }
}