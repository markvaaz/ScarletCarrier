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
using ScarletCarrier.Services;

namespace ScarletCarrier;

[RconCommandCategory("Test Commands")]
public static class TestCommands {

  [RconCommand("summon")]
  public static string SummonCommand() {
    if (!PlayerService.TryGetByName("Mark", out var playerData)) {
      return $"Player 'Mark' not found.";
    }

    CarrierService.Spawn(playerData);

    return $"Summoned Servant for player 'Mark'.";
  }

  [RconCommand("dismiss")]
  public static string DismissCommand() {
    if (!PlayerService.TryGetByName("Mark", out var playerData)) {
      return $"Player 'Mark' not found.";
    }

    CarrierService.Dismiss(playerData);

    return $"Dismissed Servant for player 'Mark'.";
  }
}