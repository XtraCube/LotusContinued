using System;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using AmongUs.Data;
using Lotus.RPC.CustomObjects;
using Lotus.RPC.CustomObjects.Interfaces;
using UnityEngine;
using VentLib.Utilities.Extensions;
using Object = UnityEngine.Object;

namespace Lotus.Patches.Client;

[HarmonyPatch(typeof(ShapeshifterMinigame), nameof(ShapeshifterMinigame.Begin))]
class AddShiftableNetObjectsPatch
{
    public static bool Prefix(ShapeshifterMinigame __instance, PlayerTask task)
    {
        if (!AmongUsClient.Instance.AmHost || CustomNetObject.AllObjects.Count == 0) return true;
        {
            Minigame.Instance = __instance;
            __instance.MyTask = task;
            __instance.MyNormTask = (task as NormalPlayerTask);
            __instance.timeOpened = Time.realtimeSinceStartup;
            if (PlayerControl.LocalPlayer)
            {
                if (MapBehaviour.Instance)
                    MapBehaviour.Instance.Close();

                PlayerControl.LocalPlayer.MyPhysics.SetNormalizedVelocity(Vector2.zero);
            }
            __instance.logger.Info("Opening minigame " + __instance.GetType().Name, null);
            __instance.StartCoroutine(__instance.CoAnimateOpen());
            DestroyableSingleton<DebugAnalytics>.Instance.Analytics.MinigameOpened(PlayerControl.LocalPlayer.Data, __instance.TaskType);
        }
        List<byte> bodies = [];
        Object.FindObjectsOfType<DeadBody>().ForEach(body => bodies.Add(body.ParentId));
        List<PlayerControl> list = PlayerControl.AllPlayerControls
            .ToArray()
            .Where(p => p != PlayerControl.LocalPlayer &&
                                          (!p.Data.IsDead || (p.Data.IsDead && bodies.Contains(p.PlayerId))))
            .ToList();
        List<uint> cnoNetIds = [];
        CustomNetObject.AllObjects.ForEach(cno =>
        {
            if (cno is not ShiftableNetObject sno) return;
            if (sno.VisibleId == byte.MaxValue || sno.VisibleId == PlayerControl.LocalPlayer.PlayerId)
            {
                list.Add(sno.playerControl);
                cnoNetIds.Add(sno.playerControl.NetId);
            }
        });
        __instance.potentialVictims = new Il2CppSystem.Collections.Generic.List<ShapeshifterPanel>();
        var list2 = new Il2CppSystem.Collections.Generic.List<UiElement>();
        for (int i = 0; i < list.Count; i++)
        {
            PlayerControl player = list[i];
            bool isCno = cnoNetIds.Contains(player.NetId);
            int num = i % 3;
            int num2 = i / 3;
            bool flag;
            if (isCno) flag = false;
            else flag = PlayerControl.LocalPlayer.Data.Role.NameColor == player.Data.Role.NameColor;
            ShapeshifterPanel shapeshifterPanel = Object.Instantiate<ShapeshifterPanel>(__instance.PanelPrefab, __instance.transform);
            shapeshifterPanel.transform.localPosition = new Vector3(__instance.XStart + (float)num * __instance.XOffset, __instance.YStart + (float)num2 * __instance.YOffset, -1f);
            if (isCno)
            {
                shapeshifterPanel.shapeshift = (Action)(() => __instance.Shapeshift(player));
                shapeshifterPanel.PlayerIcon.SetFlipX(false);
                shapeshifterPanel.PlayerIcon.ToggleName(false);
                SpriteRenderer[] componentsInChildren = shapeshifterPanel.GetComponentsInChildren<SpriteRenderer>();
                for (int e = 0; e < componentsInChildren.Length; e++)
                    componentsInChildren[e].material.SetInt(PlayerMaterial.MaskLayer, e + 2);

                shapeshifterPanel.PlayerIcon.SetMaskLayer(i + 2);
                shapeshifterPanel.PlayerIcon.UpdateFromEitherPlayerDataOrCache(PlayerControl.LocalPlayer.Data, PlayerOutfitType.Default, PlayerMaterial.MaskType.ComplexUI, false, null);
                shapeshifterPanel.LevelNumberText.text = ProgressionManager.FormatVisualLevel(1);
                shapeshifterPanel.Background.sprite = ShipStatus.Instance.CosmeticsCache.GetNameplate(PlayerControl.LocalPlayer.Data.DefaultOutfit.NamePlateId).Image;
                shapeshifterPanel.NameText.text = player.cosmetics.nameText.text;
                DataManager.Settings.Accessibility.OnColorBlindModeChanged += (Action)shapeshifterPanel.SetColorblindText;
                shapeshifterPanel.SetColorblindText();
            } else shapeshifterPanel.SetPlayer(i, player.Data, (Action)(() => __instance.Shapeshift(player)));
            shapeshifterPanel.NameText.color = (flag ? player.Data.Role.NameColor : Color.white);
            __instance.potentialVictims.Add(shapeshifterPanel);
            list2.Add(shapeshifterPanel.Button);
        }
        ControllerManager.Instance.OpenOverlayMenu(__instance.name, __instance.BackButton, __instance.DefaultButtonSelected, list2, false);
        return false;
    }
}