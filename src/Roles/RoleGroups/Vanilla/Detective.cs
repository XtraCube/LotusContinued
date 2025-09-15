using AmongUs.GameOptions;
using Lotus.Extensions;
using Lotus.Options;
using Lotus.Roles.Overrides;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;

namespace Lotus.Roles.RoleGroups.Vanilla;

public class Detective : Crewmate
{
    protected int SuspectsPerCase;

    protected GameOptionBuilder AddDetectiveOptions(GameOptionBuilder builder) => builder
        .SubOption(sub => sub
            .KeyName("Suspects per Case", Translations.Options.SuspectsPerCase)
            .AddIntRange(2, 4, 1)
            .BindInt(i => SuspectsPerCase = i)
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
            if (callingType == typeof(AbstractBaseRole)) return AddDetectiveOptions(base.RegisterOptions(optionStream));
            else return base.RegisterOptions(optionStream);
        }
        catch
        {
            return base.RegisterOptions(optionStream);
        }
    }

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .CanVent(true)
            .VanillaRole(RoleTypes.Detective)
            .OptionOverride(Override.DetectiveSuspectsPerCase, () => (float)SuspectsPerCase);

    [Localized(nameof(Detective))]
    public static class Translations
    {
        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(SuspectsPerCase))]
            public static string SuspectsPerCase = "Suspects per Case";
        }
    }
}