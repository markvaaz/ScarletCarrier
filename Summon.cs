using ScarletCore.Services;
using ScarletRCON.Shared;
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