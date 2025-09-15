using HarmonyLib;
using Hazel;

namespace Lotus.Patches;

// Innersloth thought it would be a great idea to move Phantom's RPCs into its own class.
// There was no point in doing that considering Phantom already had client side checks anyway.
// Let's just hope they don't pull this on Shapeshifter as well.

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRoleRpc))]
public class HandleRoleRpcPatch
{
    public static void Postfix(PlayerControl __instance, byte callId, MessageReader reader)
    {
        if (__instance.Data.Role is not PhantomRole)
            switch (callId)
            {
                case 62:
                    __instance.CheckVanish();
                    return;
                case 63:
                    __instance.HandleServerVanish();
                    return;
                case 64:
                    __instance.CheckAppear(reader.ReadBoolean());
                    return;
                case 65:
                    __instance.HandleServerAppear(reader.ReadBoolean());
                    return;
                default:
                    return;
            }
    }
}