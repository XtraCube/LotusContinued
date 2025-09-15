using System.Collections.Generic;
using System.Linq;
using Lotus.API.Player;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Roles.Events;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Overrides;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Utilities;
using Lotus.Extensions;
using Lotus.Options;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.RPC;
using Rewired;
using VentLib;
using VentLib.Logging;
using VentLib.Networking.RPC.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;

namespace Lotus.Roles.RoleGroups.Impostors;

public class Vampiress : Impostor, IRoleUI
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(Vampiress));
    private float killDelay;
    private VampireMode mode = VampireMode.Biting;
    private bool killButtonMode = false;
    [NewOnSetup] private HashSet<byte> bitten = null!;


    public RoleButton PetButton(IRoleButtonEditor petButton) => petButton
        .SetText(RoleTranslations.Switch)
        .SetSprite(() => LotusAssets.LoadSprite("Buttons/generic_switch_ability.png", 130, true));

    public RoleButton KillButton(IRoleButtonEditor killButton) => killButton
        .SetText(Vampire.Translations.ButtonText)
        .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/vampire_bite.png", 130, true));

    [UIComponent(UI.Text)]
    private string CurrentMode() => mode is VampireMode.Biting ? RoleColor.Colorize("(Bite)") : RoleColor.Colorize("(Kill)");

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        SyncOptions();
        if (mode is VampireMode.Killing) return base.TryKill(target);
        MyPlayer.RpcMark(target);
        InteractionResult result = MyPlayer.InteractWith(target, LotusInteraction.HostileInteraction.Create(this));
        if (result is InteractionResult.Halt) return false;

        bitten.Add(target.PlayerId);
        Async.Schedule(() =>
        {
            if (!bitten.Remove(target.PlayerId)) return;
            if (!target.IsAlive()) return;
            FatalIntent intent = new(true, () => new BittenDeathEvent(target, MyPlayer));
            DelayedInteraction interaction = new(intent, killDelay, this);
            MyPlayer.InteractWith(target, interaction);
        }, killDelay);

        return false;
    }

    [RoleAction(LotusActionType.RoundStart)]
    private void ResetKillState()
    {
        mode = VampireMode.Killing;
        killButtonMode = true;
        if (MyPlayer.AmOwner) UpdateKillButton();
        else if (MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateVampiress)?.Send([MyPlayer.OwnerId], (int)mode);
    }

    [RoleAction(LotusActionType.OnPet)]
    public void SwitchMode()
    {
        VampireMode currentMode = mode;
        mode = mode is VampireMode.Killing ? VampireMode.Biting : VampireMode.Killing;
        killButtonMode = mode is VampireMode.Killing;
        log.Trace($"Swapping Vampire Mode: {currentMode} => {mode}");
        if (MyPlayer.AmOwner) UpdateKillButton();
        else if (MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateVampiress)?.Send([MyPlayer.OwnerId], (int)mode);
    }

    [RoleAction(LotusActionType.RoundEnd, ActionFlag.WorksAfterDeath)]
    private void KillBitten()
    {
        bitten.Filter(Players.PlayerById).Where(p => p.IsAlive()).ForEach(p =>
        {
            FatalIntent intent = new(true, () => new BittenDeathEvent(p, MyPlayer));
            DelayedInteraction interaction = new(intent, killDelay, this);
            MyPlayer.InteractWith(p, interaction);
        });
        bitten.Clear();
    }

    private void UpdateKillButton()
    {
        bool tempKill = mode is VampireMode.Killing;
        if (tempKill == killButtonMode) return;
        killButtonMode = tempKill;

        if (killButtonMode) UIManager.KillButton
            .RevertSprite()
            .SetText(Witch.Translations.KillButtonText);
        else UIManager.KillButton
            .SetText(Vampire.Translations.ButtonText)
            .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/vampire_bite.png", 130, true));
    }

    [ModRPC((uint)ModCalls.UpdateVampiress, RpcActors.Host, RpcActors.NonHosts)]
    private static void RpcUpdateVampiress(int newMode)
    {
        Vampiress? vampires = PlayerControl.LocalPlayer.PrimaryRole<Vampiress>();
        if (vampires == null) return;
        vampires.mode = (VampireMode)newMode;
        vampires.UpdateKillButton();
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        AddKillCooldownOptions(base.RegisterOptions(optionStream))
            .SubOption(sub => sub
                .Name("Kill Delay")
                .BindFloat(v => killDelay = v)
                .AddFloatRange(2.5f, 60f, 2.5f, 2, GeneralOptionTranslations.SecondsSuffix)
                .Build());

    public override RoleType GetRoleType() => RoleType.Variation;

    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        base.Modify(roleModifier)
            .OptionOverride(new IndirectKillCooldown(KillCooldown, () => mode is VampireMode.Biting))
            .RoleFlags(RoleFlag.VariationRole)
            .RoleAbilityFlags(RoleAbilityFlag.UsesPet)
            .IntroSound(AmongUs.GameOptions.RoleTypes.Shapeshifter);

    public enum VampireMode
    {
        Killing,
        Biting
    }
}