using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Extensions;
using Lotus.Options;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.Roles.Internals;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Localization.Attributes;
using VentLib.Utilities.Collections;

namespace Lotus.Roles.RoleGroups.Impostors;

public class Grenadier : Vanilla.Impostor, IRoleUI
{
    [UIComponent(UI.Cooldown)]
    private Cooldown blindCooldown;
    [UIComponent(UI.Cooldown)]
    private Cooldown blindDuration;

    private float blindDistance;
    private bool canVent;
    private bool canBlindAllies;
    private int grenadeAmount;
    private int grenadesLeft;

    [NewOnSetup] private List<PlayerControl> affectedPlayers;
    [NewOnSetup] private List<Remote<GameOptionOverride>> grenadesOverride;

    public RoleButton PetButton(IRoleButtonEditor petButton) =>
        petButton
            .BindUses(() => grenadesLeft)
            .SetText(Translations.ButtonText)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/grenadier_blind.png", 130, true));

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target) => base.TryKill(target);

    [RoleAction(LotusActionType.RoundStart)]
    private void ResetCooldown(bool gameStart)
    {
        if (gameStart) grenadesLeft = grenadeAmount;
        if (blindDuration.NotReady())
        {
            EndGrenade();
            blindDuration.Finish(true);
        }
        UIManager.PetButton.BindCooldown(blindCooldown);
        blindCooldown.Start(gameStart ? 10 : float.MinValue);
    }

    [RoleAction(LotusActionType.OnPet)]
    private void GrenadierBlind()
    {
        if (blindCooldown.NotReady() || blindDuration.NotReady() || grenadesLeft <= 0) return;

        GameOptionOverride[] overrides = [ new(Override.CrewLightMod, 0f), new(Override.ImpostorLightMod, 0f) ];
        affectedPlayers = blindDistance > 0
            ? RoleUtils.GetPlayersWithinDistance(MyPlayer, blindDistance).ToList()
            : MyPlayer.GetPlayersInAbilityRangeSorted();

        affectedPlayers
            .Where(p => canBlindAllies || p.Relationship(MyPlayer) is not Relation.FullAllies)
            .Do(p => grenadesOverride.AddRange(overrides.Select(o => Game.MatchData.Roles.AddOverride(p.PlayerId, o))));

        UIManager.PetButton.BindCooldown(blindDuration);
        blindDuration.StartThenRun(() =>
        {
            EndGrenade();
            blindCooldown.Start();
            UIManager.PetButton.BindCooldown(blindCooldown);
        });
        grenadesLeft--;
    }

    private void EndGrenade()
    {
        grenadesOverride.Do(g => g.Delete());
        affectedPlayers.Do(p => p.PrimaryRole().SyncOptions());
        affectedPlayers.Clear();
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub
                .KeyName("Amount of Grenades", Translations.Options.AmountOfGrenades)
                .Bind(v => grenadeAmount = (int)v)
                .AddIntRange(1, 5, 1, 2)
                .Build())
            .SubOption(sub => sub
                .KeyName("Blind Cooldown", Translations.Options.BlindCooldown)
                .BindFloat(blindCooldown.SetDuration)
                .AddFloatRange(5f, 120f, 2.5f, 10, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub
                .KeyName("Blind Duration", Translations.Options.BlindDuration)
                .BindFloat(blindDuration.SetDuration)
                .AddFloatRange(5f, 60f, 2.5f, 4, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub
                .KeyName("Blind Effect Radius", Translations.Options.BlindRadius)
                .BindFloat(f => blindDistance = f)
                .Value(v => v.Text("Kill Distance").Value(-1f).Build())
                .AddFloatRange(1.5f, 3f, 0.1f, 4)
                .Build())
            .SubOption(sub => sub
                .KeyName("Can Blind Allies", Translations.Options.CanBlindAllies)
                .BindBool(b => canBlindAllies = b)
                .AddBoolean(false)
                .Build())
            .SubOption(sub => sub
                .KeyName("Can Vent", RoleTranslations.CanVent)
                .BindBool(b => canVent = b)
                .AddBoolean()
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .CanVent(canVent)
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet);

    [Localized(nameof(Grenadier))]
    public static class Translations
    {
        [Localized(nameof(ButtonText))]
        public static string ButtonText = "Grenade";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(AmountOfGrenades))]
            public static string AmountOfGrenades = "Amount of Grenades";

            [Localized(nameof(BlindCooldown))]
            public static string BlindCooldown = "Blind Cooldown";

            [Localized(nameof(BlindDuration))]
            public static string BlindDuration = "Blind Duration";

            [Localized(nameof(BlindRadius))]
            public static string BlindRadius = "Blind Effect Radius";

            [Localized(nameof(CanBlindAllies))]
            public static string CanBlindAllies = "Can Blind Allies";
        }
    }
}