using System.Collections.Generic;
using System.Diagnostics;
using AmongUs.GameOptions;
using Lotus.API;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.Extensions;
using Lotus.Logging;
using Lotus.Options;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Overrides;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Collections;
using VentLib.Utilities.Extensions;

namespace Lotus.Roles.RoleGroups.Vanilla;

public class Viper : Impostor
{
    protected float DissolveTime;

    [RoleAction(LotusActionType.Attack, Subclassing = false)]
    public override bool TryKill(PlayerControl target) => base.TryKill(target);

    [RoleAction(LotusActionType.Attack, priority:Priority.VeryHigh)]
    public void SendDissolveTime()
    {
        List<Remote<GameOptionOverride>> remoteOverrides = [];
        Players.GetAllPlayers().ForEach(p => remoteOverrides.Add(Game.MatchData.Roles.AddOverride(p.PlayerId, new GameOptionOverride(Override.ViperDissolveTime, DissolveTime))));
        Game.SyncAll();
        DevLogger.Log("Synced dissolve time.");
        Async.Schedule(() => remoteOverrides.RemoveAll(remote => remote.Delete()), NetUtils.DeriveDelay(0.5f));
    }

    protected GameOptionBuilder AddViperOptions(GameOptionBuilder builder) => builder
        .SubOption(sub => sub.KeyName("Dissolve Time", Translations.Options.DissolveTime)
                .AddFloatRange(0, 30, 2.5f, 6, GeneralOptionTranslations.SecondsSuffix)
                .BindFloat(f => DissolveTime = f)
                .Build());


    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream)
    {
        try
        {
            var callingMethod = Mirror.GetCaller();
            var callingType = callingMethod?.DeclaringType;

            if (callingType == null)
            {
                return base.RegisterOptions(optionStream);
            }
            if (callingType == typeof(AbstractBaseRole)) return AddViperOptions(base.RegisterOptions(optionStream));
            else return base.RegisterOptions(optionStream);
        }
        catch
        {
            return base.RegisterOptions(optionStream);
        }
    }

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .VanillaRole(RoleTypes.Viper)
            .RoleColor(Color.red)
            .CanVent(true);

    [Localized(nameof(Viper))]
    public static class Translations
    {
        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(DissolveTime))]
            public static string DissolveTime = "Dissolve Time";
        }
    }
}