using ScarletCore.Services;
using ScarletCarrier.Services;
using VampireCommandFramework;
using ScarletCore.Utils;

namespace ScarletCarrier.Commands;

[CommandGroup("carrier")]
public static class TestCommands {

  [Command("call", shortHand: "c")]
  public static void SummonCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
    }

    if (CarrierService.HasServant(playerData.PlatformId)) {
      ctx.Reply($"You already have a ~carrier~ summoned.".FormatError());
      return;
    }

    CarrierService.Spawn(playerData);

    ctx.Reply($"Your ~carrier~ has been summoned!".Format());
    ctx.Reply($"Use ~.carrier dismiss~ to dismiss it early.".Format());
    ctx.Reply($"Do ~NOT~ store equipment in the carrier, it will be lost.".FormatError());
  }

  [Command("dismiss", shortHand: "d")]
  public static void DismissCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
    }

    if (!CarrierService.HasServant(playerData.PlatformId)) {
      ctx.Reply($"You do not have a ~carrier~ summoned.".FormatError());
      return;
    }

    CarrierService.Dismiss(playerData);

    ctx.Reply($"Your ~carrier~ has been dismissed.".Format());
  }
}