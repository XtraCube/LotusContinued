using AmongUs.Data.Player;
using HarmonyLib;
using Lotus.Logging;

namespace Lotus.Patches.Client;

#if DEBUG
[HarmonyPatch(typeof(PlayerBanData), nameof(PlayerBanData.IsBanned), MethodType.Getter)]
public class AmBannedPatch
{
    public static void Postfix(out bool __result)
    {
        __result = false;
    }
}
#endif