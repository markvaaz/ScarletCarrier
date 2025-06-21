using ScarletCore.Services;
using ScarletCarrier.Services;
using VampireCommandFramework;
using ScarletCore.Utils;
using System.Collections.Generic;

namespace ScarletCarrier.Commands;

[CommandGroup("carrier", "c")]
public static class CarrierCommands {

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

  [Command("list", shortHand: "l")]
  public static void AppearanceListCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
      return;
    }

    var names = CarrierService.AppearanceNames;
    if (names.Length == 0) {
      ctx.Reply($"No appearances found.".FormatError());
      return;
    }

    var reply = "~Available appearances:~".Format();

    for (var i = 0; i < names.Length; i++) {
      reply += $"\n{i + 1} - ~{names[i]}~".Format();

      if ((i + 1) % 5 == 0 && i != names.Length - 1) {
        ctx.Reply(reply);
        reply = string.Empty;
      }
    }

    ctx.Reply(reply);

    ctx.Reply("Use ~.carrier <number>~ to change your carrier's appearance.".Format());
  }
}

public static class CarrierAppearanceCommand {
  [Command("carrier", "c")]
  public static void AppearanceCommand(ChatCommandContext ctx, int number) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
      return;
    }

    var prefabs = CarrierService.AppearancePrefabs;
    var names = CarrierService.AppearanceNames;

    if (number < 1 || number > prefabs.Length) {
      ctx.Reply($"Invalid appearance number. Use ~.carrier appearance list~ to see available appearances.".FormatError());
      return;
    }

    number--;

    var appearanceData = Plugin.Database.Get<Dictionary<ulong, string>>(CarrierService.CustomAppearances) ?? [];

    if (appearanceData.TryGetValue(playerData.PlatformId, out var currentAppearance)) {
      if (currentAppearance == prefabs[number].GuidHash.ToString()) {
        ctx.Reply($"Your carrier is already using the ~{prefabs[number]}~ appearance.".FormatError());
        return;
      }
    }

    appearanceData[playerData.PlatformId] = prefabs[number].GuidHash.ToString();

    Plugin.Database.Save(CarrierService.CustomAppearances, appearanceData);

    ctx.Reply($"Your carrier's appearance has been changed to ~{names[number]}~.".Format());
  }
}