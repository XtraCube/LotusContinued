using System.Linq;
using System.Reflection;
using Lotus.Managers;
using Lotus.Options.Patches;
using Lotus.Roles;
using VentLib.Options.UI.Options;
using VentLib.Utilities.Harmony.Attributes;
using VentLib.Utilities.Optionals;

namespace Lotus.Patches.VentLib;

class RoleOptionPatch
{
    public static FieldInfo RoleOptionField = typeof(RoleOption).GetField("Behaviour", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [QuickPostfix(typeof(RoleOption), "BindPlusMinusButtons")]
    public static void BindButtonsPatch(RoleOption __instance)
    {
        UnityOptional<RoleOptionSetting> behaviour = (UnityOptional<RoleOptionSetting>)RoleOptionField.GetValue(__instance)!;
        if (!behaviour.Exists()) return;
        string optionKey = __instance.Key();
        CustomRole? targetRole = GlobalRoleManager.Instance.AllCustomRoles()
            .FirstOrDefault(r => r.EnglishRoleName == optionKey);
        if (targetRole == null) return;
        GearIconPatch.AddGearToSettings(behaviour.Get(), targetRole);
        RoleOptionImagePatch.TryAddImageToSetting(behaviour.Get(), targetRole);
    }
}