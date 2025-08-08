using ScarletCore.Services;
using ScarletCarrier.Services;
using VampireCommandFramework;
using ScarletCore.Utils;
using System.Collections.Generic;
using ScarletCore;
using ProjectM;
using ScarletCarrier.Models;
using ScarletCore.Systems;
using ScarletCore.Data;
using Unity.Entities;

namespace ScarletCarrier.Commands;

[CommandGroup("carrier", "car")]
public static class CarrierCommands {
  public static Dictionary<PlayerData, ActionId> _selectedActions = [];

  [Command("call", shortHand: "c")]
  public static void SummonCommand(ChatCommandContext ctx) {
    var platformId = ctx.User.PlatformId;

    CarrierService.Spawn(platformId);

    ctx.Reply($"Your ~carrier~ has been summoned!".Format());
  }

  [Command("dismiss", shortHand: "d")]
  public static void DismissCommand(ChatCommandContext ctx) {
    var platformId = ctx.User.PlatformId;

    CarrierService.Dismiss(platformId);

    ctx.Reply($"Your ~carrier~ has been dismissed.".Format());
  }

  [Command("follow", shortHand: "f")]
  public static void FollowCommand(ChatCommandContext ctx) {
    CarrierService.StartFollow(ctx.User.PlatformId);

    ctx.Reply($"Your ~carrier~ will now follow you.".Format());
  }

  [Command("stop", shortHand: "s")]
  public static void StopCommand(ChatCommandContext ctx) {
    var platformId = ctx.User.PlatformId;

    if (!CarrierService.HasServant(platformId)) {
      ctx.Reply($"You do not have a ~carrier~ summoned.".FormatError());
      return;
    }

    if (!CarrierService.IsFollowing(platformId)) {
      ctx.Reply($"Your ~carrier~ is not following you.".FormatError());
      return;
    }

    CarrierService.StopFollow(platformId);

    ctx.Reply($"Your ~carrier~ will no longer follow you.".Format());
  }

  [Command("toggle emotes", shortHand: "te")]
  public static void ToggleEmotesCommand(ChatCommandContext ctx) {
    var disabledEmotes = Plugin.Database.Get<List<ulong>>("DisabledEmotes") ?? [];
    var platformId = ctx.User.PlatformId;

    if (disabledEmotes.Contains(platformId)) {
      disabledEmotes.Remove(platformId);
      ctx.Reply($"~Carrier emotes enabled~ for you.".Format());
    } else {
      disabledEmotes.Add(platformId);
      ctx.Reply($"~Carrier emotes disabled~ for you.".FormatError());
    }
    Plugin.Database.Save("DisabledEmotes", disabledEmotes);
  }

  [Command("list", shortHand: "l")]
  public static void AppearanceListCommand(ChatCommandContext ctx) {
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

  [Command("appearance", shortHand: "a")]
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
        ctx.Reply($"Your carrier is already using the ~{names[number]}~ appearance.".FormatError());
        return;
      }
    }

    appearanceData[playerData.PlatformId] = prefabs[number].GuidHash.ToString();

    Plugin.Database.Save(CarrierService.CustomAppearances, appearanceData);

    var carrier = CarrierService.GetCarrier(playerData.PlatformId);

    carrier.SwapServantAppearance();

    ctx.Reply($"Your carrier's appearance has been changed to ~{names[number]}~.".Format());
  }

  [Command("move", shortHand: "m", adminOnly: true)]
  public static void MoveCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
      return;
    }

    if (_selectedActions.ContainsKey(playerData)) {
      ctx.Reply($"You are already moving a carrier.".FormatError());
      return;
    }

    var hoveredEntity = playerData.CharacterEntity.Read<EntityInput>().HoveredEntity;
    if (!hoveredEntity.Exists()) {
      ctx.Reply($"Please aim at the carrier you want to move.".FormatError());
      return;
    }

    if (!hoveredEntity.Has<ServantData>() || !hoveredEntity.Has<NameableInteractable>() || hoveredEntity.Read<NameableInteractable>().Name.Value != Carrier.Id) {
      ctx.Reply($"The hovered entity is not a carrier.".FormatError());
      return;
    }

    _selectedActions[playerData] = ActionScheduler.OncePerFrame((end) => {
      var inp = playerData.CharacterEntity.Read<EntityInput>();

      if (inp.State.InputsDown == SyncedButtonInputAction.Primary) {
        end();
        _selectedActions.Remove(playerData);
        ctx.Reply("Carrier moved.".FormatSuccess());
        return;
      }

      hoveredEntity.SetPosition(inp.AimPosition);
    });

    ActionScheduler.Delayed(() => {
      if (!_selectedActions.ContainsKey(playerData)) return;
      ActionScheduler.CancelAction(_selectedActions[playerData]);
    }, 180);

    ctx.Reply($"You are now moving the carrier. ~Click to place it~.".Format());
  }

  [Command("forcedismiss", shortHand: "fd", adminOnly: true)]
  public static void ForceDismissCommand(ChatCommandContext ctx) {
    if (!PlayerService.TryGetById(ctx.User.PlatformId, out var playerData)) {
      ctx.Reply($"Error: Player ~{ctx.User.CharacterName}~ not found.".Format());
      return;
    }

    var hoveredEntity = playerData.CharacterEntity.Read<EntityInput>().HoveredEntity;
    if (!hoveredEntity.Exists()) {
      ctx.Reply($"Please aim at the carrier you want to move.".FormatError());
      return;
    }

    if (!hoveredEntity.Has<ServantData>() || !hoveredEntity.Has<NameableInteractable>() || hoveredEntity.Read<NameableInteractable>().Name.Value != Carrier.Id || !hoveredEntity.Has<EntityOwner>()) {
      ctx.Reply($"The hovered entity is not a carrier.".FormatError());
      return;
    }

    var owner = hoveredEntity.Read<EntityOwner>().Owner;
    var player = owner.GetPlayerData();
    var carrier = CarrierService.GetCarrier(player.PlatformId);

    if (carrier == null) {
      ctx.Reply($"Something went wrong, the carrier for player {player.Name} was not found.".FormatError());
      return;
    }

    carrier.Dismiss();

    ctx.Reply($"Carrier for player ~{player.Name}~ has been dismissed.".FormatSuccess());
  }

  [Command("forcedismiss", shortHand: "fd", adminOnly: true)]
  public static void ForceDismissCommand(ChatCommandContext ctx, string playerName) {
    if (!PlayerService.TryGetByName(playerName, out var playerData)) {
      ctx.Reply($"Error: Player ~{playerName}~ not found.".Format());
      return;
    }

    var carrier = CarrierService.GetCarrier(playerData.PlatformId);

    if (carrier == null) {
      ctx.Reply($"Carrier for player ~{playerData.Name}~ not found.".FormatError());
      return;
    }

    carrier.Dismiss();

    ctx.Reply($"Carrier for player ~{playerData.Name}~ has been dismissed.".FormatSuccess());
  }
}