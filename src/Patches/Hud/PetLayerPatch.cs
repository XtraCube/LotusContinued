using HarmonyLib;
using Lotus.Extensions;
using TMPro;
using UnityEngine;
using VentLib.Utilities.Extensions;

namespace Lotus.Patches.Hud;

[HarmonyPatch(typeof(ActionButton), nameof(ActionButton.Start))]
class PetLayerPatch
{
    public static void Prefix(ActionButton __instance)
    {
        if (__instance.TryCast(out PetButton _))
            __instance.FindChild<TextMeshPro>("Pet/Text_TMP", true).transform.localPosition -= new Vector3(0, 0, 0.1f);
    }
}