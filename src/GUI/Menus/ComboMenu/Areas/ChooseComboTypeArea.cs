using System;
using Lotus.Extensions;
using Lotus.GUI.Menus.ComboMenu.Objects;
using Lotus.GUI.Menus.OptionsMenu;
using Lotus.Roles;
using Lotus.Roles.Subroles;
using Lotus.Utilities;
using TMPro;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;
using Object = UnityEngine.Object;

namespace Lotus.GUI.Menus.ComboMenu.Areas;

[RegisterInIl2Cpp]
[Localized("GUI.ComboMenu")]
public class ChooseComboTypeArea: MonoBehaviour, IComboMenuArea
{
    private DisplayedCombo displayedCombo;
    private GameObject anchorObject;
    private ComboMenu comboMenu;

    private SpriteRenderer forceRender;
    private SpriteRenderer nextArrow;
    private SpriteRenderer banRender;

    private ComboType targetType;
    private CustomRole? role1;
    private CustomRole? role2;

    private TextMeshPro comboDescriptionText;
    private TextMeshPro comboText;

    public void Setup(HudManager _, MonoBehaviour menuBehaviour)
    {
        comboMenu = (ComboMenu)menuBehaviour;
        anchorObject = gameObject.CreateChild("ComboTypeArea", Vector3.zero, Vector3.one);
        CreateText("Title_TMP", Translations.TitleText, new Vector3(0f, 2f, 0f), 4f, anchorObject)
            .alignment = TextAlignmentOptions.Center;
        CreateText("BanText_TMP", Translations.BanComboTitleText, new Vector3(-1f, -.6f, 0f), 2f, anchorObject, Color.red)
            .alignment = TextAlignmentOptions.Center;
        CreateText("ForceText_TMP", Translations.ForceComboTitleText, new Vector3(1f, -.6f, 0f), 2f, anchorObject, Color.cyan)
            .alignment = TextAlignmentOptions.Center;

        TextMeshPro nextText = CreateText("NextText_TMP", ChooseRoleArea.Translations.NextText, new Vector3(-6.3f, -2.2f, 0f), 3f, anchorObject);
        nextText.alignment = TextAlignmentOptions.Right;

        TextMeshPro returnText = CreateText("ReturnText_TMP", ChooseRoleArea.Translations.ReturnText, new Vector3(6.23f, 2.2f, 0f), 3f, anchorObject);
        returnText.alignment = TextAlignmentOptions.Left;

        PassiveButton dummyButton = transform.parent.Find("CloseButton").GetComponent<PassiveButton>();
        PassiveButton returnButton = Instantiate(dummyButton, anchorObject.transform);
        returnButton.name = "ReturnButton";
        returnButton.Modify(Return);
        returnButton.transform.localRotation = Quaternion.Euler(0, 180, 0);
        returnButton.transform.localPosition = new Vector3(-4f, 2.2f, 0f);
        returnButton.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);

        PassiveButton nextButton = Instantiate(dummyButton, anchorObject.transform);
        nextButton.name = "NextButton";
        nextButton.Modify(Next);
        nextButton.transform.localPosition = new Vector3(4f, -2.2f, 0f);
        nextArrow = nextButton.GetComponent<SpriteRenderer>();
        nextArrow.sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);

        PassiveButton bannedButton = Instantiate(dummyButton, anchorObject.transform);
        bannedButton.name = "ChangeToBanButton";
        bannedButton.Modify(ChangeToBannedCombo);
        bannedButton.transform.localPosition = new Vector3(-1f, -1f, 0f);

        PassiveButton forcedButton = Instantiate(dummyButton, anchorObject.transform);
        forcedButton.name = "ChangeToForceButton";
        forcedButton.Modify(ChangeToForcedCombo);
        forcedButton.transform.localPosition = new Vector3(1f, -1f, 0f);

        banRender = bannedButton.GetComponent<SpriteRenderer>();
        banRender.sprite = LotusAssets.LoadSprite("Presets/Plus.png", 200f);
        forceRender = forcedButton.GetComponent<SpriteRenderer>();
        forceRender.sprite = LotusAssets.LoadSprite("Presets/Plus.png", 200f);

        comboDescriptionText = CreateText("ComboDescriptionText_TMP", string.Empty, new Vector3(0f, 1f, 0f), 3f, anchorObject);
        comboDescriptionText.alignment = TextAlignmentOptions.Center;
    }

    public void Open() => throw new NotSupportedException("Calling Open by itself is not supported. You must call OpenWithRoles.");

    public void OpenWithRoles(CustomRole? selectedRole1, CustomRole? selectedRole2, ComboType? selectedComboType = null)
    {
        if (displayedCombo != null && IsNativeObjectAlive(displayedCombo))
            displayedCombo.gameObject.Destroy();
        role1 = selectedRole1;
        role2 = selectedRole2;
        if (selectedComboType.HasValue) targetType = selectedComboType.Value;
        else
        {
            if (selectedRole2 == null) targetType = ComboType.Forced;
            else if (selectedRole2 is Subrole && selectedRole1 is not Subrole ||
                     selectedRole2 is Subrole && selectedRole1 is not Subrole) targetType = ComboType.Banned;
            else targetType = ComboType.Banned;
        }
        displayedCombo = anchorObject.QuickComponent<DisplayedCombo>("DisplayedCombo", new Vector3(0f, 0.15f, 0f), Vector3.one);
        displayedCombo.Setup(new RoleComboInfo
        {
            Role1EnglishName = selectedRole1?.EnglishRoleName ?? string.Empty,
            Role2EnglishName = selectedRole2?.EnglishRoleName ?? string.Empty,
            ComboType = (byte)targetType,
        }, false);
        displayedCombo.gameObject.GetChildren(true).ForEach(go => go.layer = LayerMask.NameToLayer("UI"));
        comboText = displayedCombo.transform.FindChild("ComboTypeText").GetComponent<TextMeshPro>();
        comboText.transform.localPosition += new Vector3(.7f, 0f, 0f);
        if (targetType is ComboType.Forced) ChangeToForcedCombo();
        else  ChangeToBannedCombo();
        anchorObject.SetActive(true);
    }

    public void Close()
    {
        if (displayedCombo != null && IsNativeObjectAlive(displayedCombo))
            displayedCombo.gameObject.Destroy();
        anchorObject.SetActive(false);
    }

    private void Next()
    {
        Close();
        comboMenu.GetArea<FinalizeArea>().OpenWithComboInfo(role1, role2, targetType);
    }

    private void Return()
    {
        Close();
        comboMenu.GetArea<ChooseRoleArea>().Open(role1, role2);
    }

    private void ChangeToForcedCombo()
    {
        targetType = ComboType.Forced;
        forceRender.color = Color.white;
        Color color = banRender.color;
        color.a = .5f;
        banRender.color = color;
        UpdateText();
    }

    private void ChangeToBannedCombo()
    {
        targetType = ComboType.Banned;
        banRender.color = Color.white;
        Color color = forceRender.color;
        color.a = .5f;
        forceRender.color = color;
        UpdateText();
    }

    private void UpdateText()
    {
        CustomRole? formattedRole1;
        CustomRole? formattedRole2;

        bool role1IsModifier = role1 is Subrole;
        bool role2IsModifier = role2 is Subrole;
        bool isBanned = targetType is ComboType.Banned;
        string stringToFormat;
        if (role1IsModifier && role2IsModifier)
        {
            stringToFormat = isBanned
                ? Translations.BannedModifierModifierComboText
                : Translations.ForcedModifierModifierComboText;
            formattedRole1 = role1;
            formattedRole2 = role2;
        }
        else if (role1IsModifier && !role2IsModifier)
        {
            stringToFormat = isBanned
                ? Translations.BannedRoleModifierComboText
                : Translations.ForcedRoleModifierComboText;
            formattedRole1 = role2;
            formattedRole2 = role1;
        }
        else if (!role1IsModifier && role2IsModifier)
        {
            stringToFormat = isBanned
                ? Translations.BannedRoleModifierComboText
                : Translations.ForcedRoleModifierComboText;
            formattedRole1 = role1;
            formattedRole2 = role2;
        }
        else
        {
            stringToFormat = isBanned
                ? Translations.BannedRoleRoleComboText
                : Translations.ForcedRoleRoleComboText;
            formattedRole1 = role1;
            formattedRole2 = role2;
        }

        comboText.text = isBanned ? "Banned" : "Forced";
        comboText.color = isBanned ? Color.red : Color.cyan;
        comboDescriptionText.text = stringToFormat.Formatted(formattedRole1?.ColoredRoleName(), formattedRole2?.ColoredRoleName());
    }

    private static TextMeshPro CreateText(string objectName, string text, Vector3 position, float fontSize, GameObject targetObject, Color? color = null)
    {
        TextMeshPro outputText = targetObject.QuickComponent<TextMeshPro>(objectName, position);
        outputText.fontSize = outputText.fontSizeMax = outputText.fontSizeMin = fontSize;
        outputText.font = CustomOptionContainer.GetGeneralFont();
        outputText.color = color ?? Color.white;
        outputText.text = text;
        return outputText;
    }

    [Localized("ChooseComboTypeArea")]
    private static class Translations
    {
        [Localized(nameof(ForceComboTitleText))] public static string ForceComboTitleText = "Forced Combo Type";
        [Localized(nameof(BanComboTitleText))] public static string BanComboTitleText = "Banned Combo Type";
        [Localized(nameof(TitleText))] public static string TitleText = "Choose the type of combo.";

        // forced combo types
        [Localized(nameof(ForcedRoleModifierComboText))]
        public static string ForcedRoleModifierComboText = "A player will always have the modifier, {1},\nif their main role is {0}.";

        [Localized(nameof(ForcedModifierModifierComboText))]
        public static string ForcedModifierModifierComboText = "A player with one of the modifiers, {0} or {1},\nwill always have the other one.";

        [Localized(nameof(ForcedRoleRoleComboText))]
        public static string ForcedRoleRoleComboText = "{1} will always spawn when {0} spawns.\nIf there aren't enough players for {1},\nthen {0} will be skipped.";

        // banned combo types
        [Localized(nameof(BannedRoleRoleComboText))]
        public static string BannedRoleRoleComboText = "{0} and {1} cannot spawn\nin the same game together.";

        [Localized(nameof(BannedRoleModifierComboText))]
        public static string BannedRoleModifierComboText = "A player with the role {0}\ncannot have the modifier {1}.";

        [Localized(nameof(BannedModifierModifierComboText))]
        public static string BannedModifierModifierComboText = "A player cannot have the modifiers\n{0} and {1} at the same time.";
    }
}