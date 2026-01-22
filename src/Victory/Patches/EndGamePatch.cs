using HarmonyLib;
using Lotus.API.Odyssey;
using Lotus.Patches;
using Lotus.RPC.CustomObjects;

namespace Lotus.Victory.Patches;

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameEnd))]
class EndGamePatch
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(EndGamePatch));

    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ref EndGameResult endGameResult)
    {
        Game.Cleanup();
        CustomNetObject.Reset();

        SelectRolesPatch.desyncedIntroText = new();

        log.Info("-----------Game End----------- Phase");
    }
}