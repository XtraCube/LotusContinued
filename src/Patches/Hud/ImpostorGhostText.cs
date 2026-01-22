using HarmonyLib;

namespace Lotus.Patches.Hud;

[HarmonyPatch(typeof(ImpostorGhostRole), nameof(ImpostorGhostRole.SpawnTaskHeader))]
class ImpostorGhostText
{
    public static bool Prefix(ImpostorGhostRole __instance, [HarmonyArgument(0)] PlayerControl playerControl)
    {
        ImportantTextTask textTask = PlayerTask.GetOrCreateTask<ImportantTextTask>(playerControl, 0);
        textTask.Text = "\n" + TranslationController.Instance.GetString(StringNames.GhostImpostor);
        return false;
    }
}