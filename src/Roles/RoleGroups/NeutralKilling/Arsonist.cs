using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Stats;
using Lotus.Factions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using Lotus.Managers.History.Events;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Utilities;
using Lotus.Extensions;
using Lotus.Options;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.Overrides;
using Lotus.RPC;
using UnityEngine;
using VentLib;
using VentLib.Localization.Attributes;
using VentLib.Networking.RPC.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;

namespace Lotus.Roles.RoleGroups.NeutralKilling;

public class Arsonist : NeutralKillingBase, IRoleUI
{
    private static IAccumulativeStatistic<int> _dousedPlayers = Statistic<int>.CreateAccumulative($"Roles.{nameof(Arsonist)}.DousedPlayers", () => Translations.DousedPlayerStatistic);
    private static IAccumulativeStatistic<int> _incineratedPlayers = Statistic<int>.CreateAccumulative($"Roles.{nameof(Arsonist)}.IncineratedPlayers", () => Translations.IncineratedPlayerStatistic);

    public override List<Statistic> Statistics()
    {
        if (MyPlayer == null) return new List<Statistic> { _incineratedPlayers, _dousedPlayers };
        if (_incineratedPlayers.GetValue(MyPlayer.UniquePlayerId()) >= _dousedPlayers.GetValue(MyPlayer.UniquePlayerId())) return new List<Statistic> { _incineratedPlayers, _dousedPlayers };
        return new List<Statistic> { _dousedPlayers, _incineratedPlayers };
    }

    private static string[] _douseProgressIndicators = { "◦", "◎", "◉", "●" };

    private int requiredAttacks;
    private bool canIgniteAnyitme;

    private int backedAlivePlayers;
    private int knownAlivePlayers;
    [NewOnSetup] private HashSet<byte> dousedPlayers;
    [NewOnSetup] private Dictionary<byte, Remote<IndicatorComponent>> indicators;
    [NewOnSetup] private Dictionary<byte, int> douseProgress;

    private bool sharesCooldown;
    private Cooldown douseTimer;
    [UIComponent(UI.Cooldown)] private Cooldown igniteTimer;

    public RoleButton KillButton(IRoleButtonEditor killButton) => killButton
        .SetText(Translations.ButtonText)
        .SetSprite(() => LotusAssets.LoadSprite("Buttons/Neut/arsonist_douse.png", 130, true));

    public RoleButton PetButton(IRoleButtonEditor petButton) => petButton
        .BindCooldown(igniteTimer)
        .SetText(Translations.IgniteButtonText)
        .SetSprite(() => LotusAssets.LoadSprite("Buttons/Neut/arsonist_ignite.png", 130, true));

    [UIComponent(UI.Counter)]
    private string DouseCounter() => RoleUtils.Counter(dousedPlayers.Count, knownAlivePlayers);

    [UIComponent(UI.Text)]
    private string DisplayWin() => dousedPlayers.Count >= backedAlivePlayers ? RoleColor.Colorize(Translations.PressIgniteToWinMessage) : "";

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        if (sharesCooldown && igniteTimer.NotReady() || douseTimer.NotReady()) return false;

        bool douseAttempt = MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this)) is InteractionResult.Proceed;
        if (!douseAttempt) return false;

        int progress = douseProgress[target.PlayerId] = douseProgress.GetValueOrDefault(target.PlayerId) + 1;
        if (progress > requiredAttacks) return false;

        RenderProgress(target, progress);
        if (progress < requiredAttacks) return false;

        dousedPlayers.Add(target.PlayerId);
        MyPlayer.RpcMark(target);
        if (sharesCooldown)
        {
            douseTimer.Start(KillCooldown);
            igniteTimer.Start(KillCooldown);
            if (!MyPlayer.AmOwner && MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateArsonist)?.Send([MyPlayer.OwnerId], KillCooldown);
        }
        Game.MatchData.GameHistory.AddEvent(new GenericTargetedEvent(MyPlayer, target, Translations.DouseEventMessage.Formatted(MyPlayer.name, target.name)));
        _dousedPlayers.Update(MyPlayer.UniquePlayerId(), i => i + 1);

        MyPlayer.NameModel().Render();
        backedAlivePlayers = CountAlivePlayers();

        return false;
    }

    private void RenderProgress(PlayerControl target, int progress)
    {
        if (progress > requiredAttacks) return;
        string indicator = _douseProgressIndicators[Mathf.Clamp(Mathf.FloorToInt(progress / (requiredAttacks / (float)_douseProgressIndicators.Length) - 1), 0, 3)];

        Remote<IndicatorComponent> IndicatorSupplier() => target.NameModel().GetComponentHolder<IndicatorHolder>().Add(new IndicatorComponent("", Game.InGameStates, viewers: MyPlayer));

        Remote<IndicatorComponent> component = indicators.GetOrCompute(target.PlayerId, IndicatorSupplier);
        component.Get().SetMainText(new LiveString(indicator, RoleColor));
    }


    [RoleAction(LotusActionType.OnPet)]
    private void KillDoused()
    {
        if (dousedPlayers.Count < CountAlivePlayers() && !canIgniteAnyitme) return;
        if (canIgniteAnyitme && sharesCooldown && douseTimer.NotReady()) return;
        if (igniteTimer.NotReady()) return;
        if (canIgniteAnyitme)
        {
            igniteTimer.Start();
            if (!MyPlayer.AmOwner && MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateArsonist)?.Send([MyPlayer.OwnerId], -1);
        }
        dousedPlayers.Filter(Utils.PlayerById).Where(p => p.IsAlive()).Do(p =>
        {
            FatalIntent intent = new(true, () => new CustomDeathEvent(p, MyPlayer, Translations.IncineratedDeathName));
            IndirectInteraction interaction = new(intent, this);
            MyPlayer.InteractWith(p, interaction);
            _incineratedPlayers.Update(MyPlayer.UniquePlayerId(), i => i + 1);
        });
    }

    [RoleAction(LotusActionType.RoundStart)]
    protected override void PostSetup()
    {
        knownAlivePlayers = CountAlivePlayers();
        dousedPlayers.RemoveWhere(p => Utils.PlayerById(p).Transform(pp => !pp.IsAlive(), () => true));
    }

    [RoleAction(LotusActionType.Disconnect)]
    [RoleAction(LotusActionType.PlayerDeath, ActionFlag.GlobalDetector)]
    private int CountAlivePlayers() => backedAlivePlayers = Players.GetPlayers(PlayerFilter.Alive | PlayerFilter.NonPhantom).Count(p => p.PlayerId != MyPlayer.PlayerId && Relationship(p) is not Relation.FullAllies);

    [RoleAction(LotusActionType.PlayerDeath)]
    private void ArsonistDies() => indicators.Values.ForEach(v => v.Delete());

    [ModRPC((uint)ModCalls.UpdateArsonist, RpcActors.Host, RpcActors.NonHosts)]
    private static void RpcUpdateArsonist(float duration) => PlayerControl.LocalPlayer.PrimaryRole<Arsonist>()?.igniteTimer.Start(duration);

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        AddKillCooldownOptions(base.RegisterOptions(optionStream), "Douse Cooldown", Translations.Options.DouseCooldown)
            .SubOption(sub => sub.KeyName("Attacks to Complete Douse", Translations.Options.AttacksToCompleteDouse)
                .AddIntRange(1, 100, defaultIndex: 2)
                .BindInt(i => requiredAttacks = i)
                .Build())
            .SubOption(sub => sub.KeyName("Can Ignite Anytime", Translations.Options.CanIgniteAnytime)
                .AddBoolean(false)
                .BindBool(b => canIgniteAnyitme = b)
                .ShowSubOptionPredicate(v => (bool)v)
                .SubOption(sub2 => sub2
                    .KeyName("Ignite Cooldown", Translations.Options.IgniteCooldown)
                    .AddFloatRange(0, 60f, 2.5f, suffix: GeneralOptionTranslations.SecondsSuffix)
                    .BindFloat(igniteTimer.SetDuration)
                    .Build())
                .SubOption(sub2 => sub2
                    .KeyName("Share Same Cooldowns", Translations.Options.ShareSameCooldown)
                    .AddBoolean()
                    .BindBool(b => sharesCooldown = b)
                    .Build())
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .RoleColor(new Color(1f, 0.4f, 0.2f))
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet)
            .IntroSound(AmongUs.GameOptions.RoleTypes.Crewmate)
            .OptionOverride(new IndirectKillCooldown(KillCooldown))
            .RoleAbilityFlags(RoleAbilityFlag.CannotSabotage | RoleAbilityFlag.CannotVent);

    [Localized(nameof(Arsonist))]
    public static class Translations
    {
        [Localized(nameof(ButtonText))]
        public static string ButtonText = "Douse";

        [Localized(nameof(IgniteButtonText))]
        public static string IgniteButtonText = "Ignite";

        [Localized(nameof(IncineratedDeathName))]
        public static string IncineratedDeathName = "Incinerated";

        [Localized(nameof(DouseEventMessage))]
        public static string DouseEventMessage = "{0} doused {1}.";

        [Localized(nameof(PressIgniteToWinMessage))]
        public static string PressIgniteToWinMessage = "Press Ignite to Win";

        [Localized(nameof(DousedPlayerStatistic))]
        public static string DousedPlayerStatistic = "Doused Players";

        [Localized(nameof(IncineratedPlayerStatistic))]
        public static string IncineratedPlayerStatistic = "Incinerated Players";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(DouseCooldown))]
            public static string DouseCooldown = "Douse Cooldown";

            [Localized(nameof(IgniteCooldown))]
            public static string IgniteCooldown = "Ignite Cooldown";

            [Localized(nameof(AttacksToCompleteDouse))]
            public static string AttacksToCompleteDouse = "Attacks to Complete Douse";

            [Localized(nameof(CanIgniteAnytime))]
            public static string CanIgniteAnytime = "Can Ignite Anytime";

            [Localized(nameof(ShareSameCooldown))]
            public static string ShareSameCooldown = "Can't Douse/Ignite when other is on Cooldown";
        }
    }
}