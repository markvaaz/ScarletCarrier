using ScarletCore.Services;
using ScarletCarrier.Services;
using VampireCommandFramework;
using ScarletCore.Utils;

namespace ScarletCarrier.Commands;

[CommandGroup("carrier")]
public static class TestCommands {

  [Command("summon")]
  public static void SummonCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetByName(ctx.User.CharacterName.ToString(), out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
    }

    CarrierService.Spawn(playerData);

    ctx.Reply($"Your ~carrier~ has been summoned!".Format());
    ctx.Reply($"It will automatically despawn in ~60 seconds~.".Format());
    ctx.Reply($"Use ~.carrier dismiss~ to dismiss it early.".Format());
  }

  [Command("dismiss")]
  public static void DismissCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetByName(ctx.User.CharacterName.ToString(), out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
    }

    CarrierService.Dismiss(playerData);

    ctx.Reply($"Your carrier has been dismissed successfully.".Format());
  }
}