using UnityEngine;

namespace Lotus.Roles.GUI;

public class AmongUsButtonSpriteReferences
{
    public static ReportButton ReportButton => HudManager.InstanceExists ? HudManager.Instance.ReportButton : null!;
    public static Sprite ReportButtonSprite => ReportButton != null ? ReportButton.graphic.sprite : null!;

    public static VentButton VentButton => HudManager.InstanceExists ? HudManager.Instance.ImpostorVentButton : null!;
    public static Sprite VentButtonSprite => VentButton != null ? VentButton.graphic.sprite : null!;

    public static UseButton UseButton => HudManager.InstanceExists ? HudManager.Instance.UseButton : null!;
    public static Sprite UseButtonSprite => UseButton != null ? UseButton.graphic.sprite : null!;

    public static PetButton PetButton => HudManager.InstanceExists ? HudManager.Instance.PetButton : null!;
    public static Sprite PetButtonSprite => PetButton != null ? PetButton.graphic.sprite : null!;

    public static KillButton KillButton => HudManager.InstanceExists ? HudManager.Instance.KillButton : null!;
    public static Sprite KillButtonSprite => KillButton != null ? KillButton.graphic.sprite : null!;

    public static AbilityButton AbilityButton => HudManager.InstanceExists ? HudManager.Instance.AbilityButton : null!;
    public static Sprite AbilityButtonSprite => AbilityButton != null ? AbilityButton.graphic.sprite : null!;
}