extern alias JBAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using JBAnnotations::JetBrains.Annotations;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Managers.History.Events;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Interactions.Interfaces;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Extensions;
using Lotus.Logging;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.RoleGroups.Neutral;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Roles.Subroles;
using Lotus.RPC;
using UnityEngine;
using VentLib;
using VentLib.Localization.Attributes;
using VentLib.Logging;
using VentLib.Networking.RPC;
using VentLib.Networking.RPC.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using Object = UnityEngine.Object;

namespace Lotus.Roles.RoleGroups.Impostors;

public class Janitor : Impostor, IRoleUI
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Janitor));
    public static HashSet<Type> JanitorBannedModifiers = new() { typeof(Oblivious), typeof(Sleuth) };
    public override HashSet<Type> BannedModifiers() => cleanOnKill ? new HashSet<Type>() : JanitorBannedModifiers;

    private bool cleanOnKill;
    private float killMultiplier;

    private float JanitorKillCooldown() => cleanOnKill ? KillCooldown * killMultiplier : KillCooldown;

    [UIComponent(UI.Cooldown)] private Cooldown cleanCooldown;

    public RoleButton KillButton(IRoleButtonEditor button) => cleanOnKill
        ? button
            .SetText(Translations.ButtonText)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/janitor_clean.png", 130, true))
        : button.Default(true);

    public RoleButton ReportButton(IRoleButtonEditor button) => cleanOnKill
        ? button.Default(true)
        : button
            .BindCooldown(cleanCooldown)
            .SetText(Translations.ButtonText)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/janitor_clean.png", 130, true));

    protected override void PostSetup()
    {
        cleanCooldown.SetDuration(JanitorKillCooldown());
    }

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        cleanCooldown.Start();
        if (MyPlayer.IsModded() && !MyPlayer.AmOwner)
            Vents.FindRPC((uint)ModCalls.UpdateJanitor)?.Send([MyPlayer.OwnerId]);

        if (!cleanOnKill) return base.TryKill(target);

        MyPlayer.RpcMark(target);
        if (MyPlayer.InteractWith(target, new LotusInteraction(new FakeFatalIntent(), this)) is InteractionResult.Halt)
            return false;
        MyPlayer.RpcVaporize(target);
        Game.MatchData.GameHistory.AddEvent(new KillEvent(MyPlayer, target));
        Game.MatchData.GameHistory.AddEvent(new GenericAbilityEvent(MyPlayer,
            $"{Color.red.Colorize(MyPlayer.name)} cleaned {target.GetRoleColor().Colorize(target.name)}."));
        return true;
    }

    [RoleAction(LotusActionType.ReportBody)]
    private void JanitorCleanBody(Optional<NetworkedPlayerInfo> target, ActionHandle handle)
    {
        if (!target.Exists() || cleanOnKill) return;
        if (cleanCooldown.NotReady()) return;
        handle.Cancel();
        cleanCooldown.Start(AUSettings.KillCooldown());

        byte playerId = target.Get().PlayerId;

        foreach (DeadBody deadBody in Object.FindObjectsOfType<DeadBody>())
            if (deadBody.ParentId == playerId)
                if (ModVersion.AllClientsModded()) CleanBody(playerId);
                else Game.MatchData.UnreportableBodies.Add(playerId);

        MyPlayer.RpcMark(MyPlayer);
        if (MyPlayer.IsModded() && !MyPlayer.AmOwner)
            Vents.FindRPC((uint)ModCalls.UpdateJanitor)?.Send([MyPlayer.OwnerId]);
    }

    [ModRPC(RoleRPC.RemoveBody, invocation: MethodInvocation.ExecuteAfter)]
    private static void CleanBody(byte playerId)
    {
        log.Debug("Destroying Bodies", "JanitorClean");
        Object.FindObjectsOfType<DeadBody>().ToArray().Where(db => db.ParentId == playerId)
            .ForEach(b => Object.Destroy(b.gameObject));
    }

    [UsedImplicitly]
    [ModRPC(ModCalls.UpdateJanitor, RpcActors.Host, RpcActors.NonHosts)]
    private static void RpcUpdateJanitor()
    {
        Janitor? janitor = PlayerControl.LocalPlayer.PrimaryRole<Janitor>();
        if (janitor == null) return;
        janitor.cleanCooldown.Finish(true);
        if (janitor.cleanOnKill)
        {
            janitor.cleanCooldown.Start();
            return;
        }
        var reportButton = janitor.UIManager.ReportButton
            .RevertSprite()
            .SetText(Vulture.Translations.ReportButtonText);
        janitor.cleanCooldown.StartThenRun(() => reportButton
            .SetText(Translations.ButtonText)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/janitor_clean.png", 130, true)));
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.KeyName("Clean On Kill", Translations.Options.CleanOnKill)
                .AddBoolean()
                .BindBool(b => cleanOnKill = b)
                .ShowSubOptionPredicate(b => (bool)b)
                .SubOption(sub2 => sub2
                    .KeyName("Kill Cooldown Multiplier", Translations.Options.KillCooldownMultiplier)
                    .AddFloatRange(1, 3, 0.25f, 2, "x")
                    .BindFloat(f => killMultiplier = f)
                    .Build())
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .OptionOverride(new IndirectKillCooldown(() => KillCooldown * 2, () => !cleanOnKill && cleanCooldown.NotReady()))
            .OptionOverride(new IndirectKillCooldown(JanitorKillCooldown, () => cleanOnKill));

    [Localized(nameof(Janitor))]
    public static class Translations
    {
        [Localized(nameof(ButtonText))]
        public static string ButtonText = "Clean";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(CleanOnKill))]
            public static string CleanOnKill = "Clean On Kill";

            [Localized(nameof(KillCooldownMultiplier))]
            public static string KillCooldownMultiplier = "Kill Cooldown Multiplier";
        }
    }

    private class FakeFatalIntent : IFatalIntent
    {
        public void Action(PlayerControl actor, PlayerControl target)
        {
        }

        public void Halted(PlayerControl actor, PlayerControl target)
        {
        }

        public Optional<IDeathEvent> CauseOfDeath() => Optional<IDeathEvent>.Null();

        public bool IsRanged() => false;

        private Dictionary<string, object?>? meta;
        public object? this[string key]
        {
            get => (meta ?? new Dictionary<string, object?>()).GetValueOrDefault(key);
            set => (meta ?? new Dictionary<string, object?>())[key] = value;
        }
    }
}