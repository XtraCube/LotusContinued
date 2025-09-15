using System.Linq;
using Lotus.API.Odyssey;
using Lotus.Factions;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Options;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.RoleGroups.Vanilla;
using Lotus.Extensions;
using Lotus.Logging;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using Lotus.API.Player;
using Lotus.Managers.History.Events;
using Lotus.Roles.GUI;
using Lotus.Roles.GUI.Interfaces;
using Lotus.RPC;
using VentLib;
using VentLib.Utilities.Extensions;

namespace Lotus.Roles.RoleGroups.Impostors;

public class Sniper : Shapeshifter, IRoleUI
{
    private bool preciseShooting;
    private int playerPiercing;
    private bool refundOnKill;

    private int totalBulletCount;
    private int currentBulletCount;
    private Vector2? startingLocation;

    public RoleButton AbilityButton(IRoleButtonEditor button) => button
        .BindUses(() => currentBulletCount)
        .SetText(SniperTranslations.ButtonText)
        .SetSprite(() => LotusAssets.LoadSprite("Buttons/Imp/sniper_aim.png", 130, true));

    [UIComponent(UI.Counter)]
    private string BulletCountCounter() => currentBulletCount >= 0 ? RoleUtils.Counter(currentBulletCount, color: ModConstants.Palette.MadmateColor) : "";

    protected override void PostSetup()
    {
        currentBulletCount = totalBulletCount;
        ShapeshiftDuration = 5f;
    }

    [RoleAction(LotusActionType.Attack)]
    public override bool TryKill(PlayerControl target)
    {
        bool success = currentBulletCount == 0 && base.TryKill(target);
        if (success && refundOnKill && currentBulletCount >= 0)
        {
            currentBulletCount++;
            if (!MyPlayer.AmOwner && MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateSniper)?.Send([MyPlayer.OwnerId], startingLocation == null, currentBulletCount);
        }

        return success;
    }

    [RoleAction(LotusActionType.Shapeshift)]
    private void StartSniping()
    {
        startingLocation = MyPlayer.GetTruePosition();
        if (MyPlayer.AmOwner) UpdateShapeshiftButton(true);
        else if (MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateSniper)?.Send([MyPlayer.OwnerId], true, currentBulletCount);
        // DevLogger.Log($"Starting position: {startingLocation}");
    }

    [RoleAction(LotusActionType.Unshapeshift)]
    private bool FireBullet()
    {
        if (currentBulletCount == 0 || startingLocation == null) return false;
        currentBulletCount--;

        Vector2 targetPosition = (MyPlayer.GetTruePosition() - startingLocation.Value).normalized;
        // DevLogger.Log($"Target Position: {targetPosition}");
        int kills = 0;

        foreach (PlayerControl target in Players.GetAlivePlayers().Where(p => p.PlayerId != MyPlayer.PlayerId && p.Relationship(MyPlayer) is not Relation.FullAllies))
        {
            DevLogger.Log(target.name);
            Vector3 targetPos = target.transform.position - (Vector3)MyPlayer.GetTruePosition();
            Vector3 targetDirection = targetPos.normalized;
            // DevLogger.Log($"Target direction: {targetDirection}");
            float dotProduct = Vector3.Dot(targetPosition, targetDirection);
            // DevLogger.Log($"Dot Product: {dotProduct}");
            float error = !preciseShooting ? targetPos.magnitude : Vector3.Cross(targetPosition, targetPos).magnitude;
            // DevLogger.Log($"Error: {error}");
            if (dotProduct < 0.98 || (error >= 1.0 && preciseShooting)) continue;
            float distance = Vector2.Distance(MyPlayer.transform.position, target.transform.position);
            InteractionResult result = MyPlayer.InteractWith(target, new RangedInteraction(new FatalIntent(true, () => new CustomDeathEvent(target, MyPlayer, ModConstants.DeathNames.Sniped)), distance, this));
            if (result is InteractionResult.Halt) continue;
            kills++;
            MyPlayer.RpcMark();
            if (kills > playerPiercing && playerPiercing != -1) break;
        }

        if (kills > 0 && refundOnKill) currentBulletCount++;
        startingLocation = null;
        if (MyPlayer.AmOwner) UpdateShapeshiftButton(false);
        else if (MyPlayer.IsModded()) Vents.FindRPC((uint)ModCalls.UpdateSniper)?.Send([MyPlayer.OwnerId], false, currentBulletCount);

        return kills > 0;
    }

    private void UpdateShapeshiftButton(bool isShifted) => UIManager.AbilityButton.SetSprite(() => isShifted
        ? LotusAssets.LoadSprite("Buttons/Imp/sniper_shoot.png", 130, true)
        : LotusAssets.LoadSprite("Buttons/Imp/sniper_aim.png", 130, true));

    private static void RpcUpdateSniper(bool isShifted, int bulletCount)
    {
        Sniper? sniper = PlayerControl.LocalPlayer.PrimaryRole<Sniper>();
        if (sniper == null) return;
        sniper.currentBulletCount = bulletCount;
        sniper.UpdateShapeshiftButton(isShifted);
    }

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub
                .KeyName("Sniper Bullet Count", SniperTranslations.Options.SniperBulletCount)
                .Value(v => v.Text(ModConstants.Infinity).Color(ModConstants.Palette.InfinityColor).Value(-1).Build())
                .BindInt(v => totalBulletCount = v)
                .SubOption(sub2 => sub2
                    .KeyName("Refund Bullet on Kills", SniperTranslations.Options.RefundBulletOnKill)
                    .BindBool(b => refundOnKill = b)
                    .AddBoolean(false)
                    .Build())
                .AddIntRange(1, 20, 1, 8)
                .Build())
            .SubOption(sub => sub
                .KeyName("Sniping Cooldown", SniperTranslations.Options.SnipingCooldown)
                .BindFloat(f => ShapeshiftCooldown = f + 5f)
                .AddFloatRange(2.5f, 120f, 2.5f, 19, GeneralOptionTranslations.SecondsSuffix)
                .Build())
            .SubOption(sub => sub
                .KeyName("Precise Shooting", SniperTranslations.Options.PreciseShooting)
                .BindBool(v => preciseShooting = v)
                .AddBoolean(false)
                .Build())
            .SubOption(sub => sub
                .KeyName("Player Piercing", SniperTranslations.Options.PlayerPiercing)
                .Value(v => v.Text(ModConstants.Infinity).Color(ModConstants.Palette.InfinityColor).Value(-1).Build())
                .BindInt(v => playerPiercing = v)
                .AddIntRange(1, ModConstants.MaxPlayers, 1, 2)
                .Build());

    [Localized(nameof(Sniper))]
    public static class SniperTranslations
    {
        [Localized(nameof(ButtonText))]
        public static string ButtonText = "Aim";

        [Localized(ModConstants.Options)]
        public static class Options
        {
            [Localized(nameof(SniperBulletCount))]
            public static string SniperBulletCount = "Sniper Bullet Count";

            [Localized(nameof(SnipingCooldown))]
            public static string SnipingCooldown = "Sniping Cooldown";

            [Localized(nameof(RefundBulletOnKill))]
            public static string RefundBulletOnKill = "Refund Bullet on Kills";

            [Localized(nameof(PreciseShooting))]
            public static string PreciseShooting = "Precise Shooting";

            [Localized(nameof(PlayerPiercing))]
            public static string PlayerPiercing = "Player Piercing";
        }
    }

}