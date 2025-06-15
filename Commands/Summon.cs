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

    CarrierService.Spawn(playerData);

    ctx.Reply($"Your ~carrier~ has been summoned!".Format());
    ctx.Reply($"It will automatically despawn in ~60 seconds~.".Format());
    ctx.Reply($"Use ~.carrier dismiss~ to dismiss it early.".Format());
  }

  [Command("dismiss", shortHand: "d")]
  public static void DismissCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
    }

    CarrierService.Dismiss(playerData);

    ctx.Reply($"Your carrier has been dismissed successfully.".Format());
  }
}