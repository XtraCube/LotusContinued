extern alias JBAnnotations;
using System.Collections.Generic;
using System.Linq;
using JBAnnotations::JetBrains.Annotations;
using Lotus.API.Odyssey;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Roles.RoleGroups.Crew;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Options;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.RPC;
using UnityEngine;
using VentLib;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Localization.Attributes;
using VentLib.Networking.RPC.Attributes;

namespace Lotus.Roles.RoleGroups.Impostors;

public class Consort : Impostor, IRoleUI
{
    private float roleblockDuration;
    private bool blocking;

    [NewOnSetup] private Dictionary<byte, Escort.BlockDelegate> blockedPlayers;

    [UIComponent(UI.Cooldown)]
    private Cooldown roleblockCooldown;

    public RoleButton KillButton(IRoleButtonEditor killButton) => killButton
        .Default(false);

    [UIComponent(UI.Text)]
    private string BlockingText() => !blocking ? "" : Color.red.Colorize("Blocking");

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        if (!blocking) return base.TryKill(target);
        if (blockedPlayers.ContainsKey(target.PlayerId)) return false;

        if (MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Halt)
            return false;

        roleblockCooldown.Start();
        blocking = false;

        blockedPlayers[target.PlayerId] = Escort.BlockDelegate.Block(target, MyPlayer, roleblockDuration);
        MyPlayer.RpcMark(target);
        Game.MatchData.GameHistory.AddEvent(new GenericTargetedEvent(MyPlayer, target,
            $"{RoleColor.Colorize(MyPlayer.name)} role blocked {target.GetRoleColor().Colorize(target.name)}."));

        if (roleblockDuration > 0) Async.Schedule(() => blockedPlayers.Remove(target.PlayerId), roleblockDuration);
        return false;
    }

    [RoleAction(LotusActionType.OnPet)]
    private void ChangeToBlockMode()
    {
        if (roleblockCooldown.IsReady())
        {
            blocking = !blocking;
            UpdateKillButton(UIManager.KillButton);
        }
    }

    [RoleAction(LotusActionType.RoundStart)]
    [RoleAction(LotusActionType.RoundEnd)]
    private void UnblockPlayers()
    {
        blockedPlayers.ToArray().ForEach(k =>
        {
            blockedPlayers.Remove(k.Key);
            k.Value.Delete();
        });
    }

    [RoleAction(LotusActionType.PlayerAction, ActionFlag.GlobalDetector)]
    private void BlockAction(PlayerControl source, ActionHandle handle, RoleAction action)
    {
        if (action.Blockable) Block(source, handle);
    }

    [RoleAction(LotusActionType.VentEntered, ActionFlag.GlobalDetector)]
    private void Block(PlayerControl source, ActionHandle handle)
    {
        Escort.BlockDelegate? blockDelegate = blockedPlayers.GetValueOrDefault(source.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

    [RoleAction(LotusActionType.SabotageStarted, ActionFlag.GlobalDetector)]
    private void BlockSabotage(PlayerControl caller, ActionHandle handle)
    {
        Escort.BlockDelegate? blockDelegate = blockedPlayers.GetValueOrDefault(caller.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

    [RoleAction(LotusActionType.ReportBody, ActionFlag.GlobalDetector)]
    private void BlockReport(PlayerControl reporter, ActionHandle handle)
    {
        Escort.BlockDelegate? blockDelegate = blockedPlayers.GetValueOrDefault(reporter.PlayerId);
        if (blockDelegate == null) return;

        handle.Cancel();
        blockDelegate.UpdateDelegate();
    }

    private RoleButton UpdateKillButton(RoleButton killButton)
    {
        if (!MyPlayer.AmOwner)
        {
            if (MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateConsort)?.Send([MyPlayer.OwnerId], blocking);
            return killButton;
        }

        return blocking
            ? killButton
                .SetText(Translations.ButtonText)
                .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/consort_roleblock.png", 130, true))
            : killButton
                .SetText(Witch.Translations.KillButtonText)
                .RevertSprite();
    }

    [UsedImplicitly]
    [ModRPC((uint)ModCalls.UpdateConsort, RpcActors.Host, RpcActors.NonHosts)]
    private static void RpcUpdateConsort(bool isBlocking)
    {
        Consort? consort = PlayerControl.LocalPlayer.PrimaryRole<Consort>();
        if (consort == null) return;
        consort.blocking = isBlocking;
        consort.UpdateKillButton(consort.UIManager.KillButton);
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.KeyName("Roleblock Cooldown", Translations.Options.roleblockCooldown)
                .BindFloat(roleblockCooldown.SetDuration)
                .AddFloatRange(0, 120, 2.5f, 18, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub
                .KeyName("Roleblock Duration", Translations.Options.roleblockDuration)
                .BindFloat(v => roleblockDuration = v)
                .Value(v => v.Text("Until Meeting").Value(-1f).Build())
                .AddFloatRange(5, 120, 5, suffix: GeneralOptionTranslations.SecondsSuffix)
                .Build());


    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .OptionOverride(new IndirectKillCooldown(KillCooldown, () => blocking))
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet);

    [Localized(nameof(Consort))]
    public static class Translations
    {
        [Localized(nameof(ButtonText))]
        public static string ButtonText = "Roleblock";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(roleblockCooldown))]
            public static string roleblockCooldown = "Role-Block Cooldown";

            [Localized(nameof(roleblockDuration))]
            public static string roleblockDuration = "Role-Block Duration";
        }
    }
}

