using System;
using Lotus.Extensions;
using Lotus.GUI.Menus.ComboMenu.Objects;
using Lotus.GUI.Menus.OptionsMenu;
using Lotus.Logging;
using Lotus.Managers;
using Lotus.Roles;
using Lotus.Utilities;
using TMPro;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;

namespace Lotus.GUI.Menus.ComboMenu.Areas;

[RegisterInIl2Cpp]
[Localized("GUI.ComboMenu")]
public class FinalizeArea: MonoBehaviour, IComboMenuArea
{
    private GameObject anchorObject;
    private ComboMenu comboMenu;

    private DisplayedCombo displayedCombo;
    private ComboType targetType;
    private CustomRole? role1;
    private CustomRole? role2;

    public void Setup(HudManager _, MonoBehaviour menuBehaviour)
    {
        comboMenu = (ComboMenu)menuBehaviour;
        anchorObject = gameObject.CreateChild("FinalizeArea", Vector3.zero, Vector3.one);
        CreateText("Title_TMP", Translations.TitleText, new Vector3(0f, 1.3f, 0f), 4f, anchorObject)
            .alignment = TextAlignmentOptions.Center;
        CreateText("CreateText_TMP", Translations.CreateComboText, new Vector3(0f, 0f, 0f), 4f, anchorObject)
            .alignment = TextAlignmentOptions.Center;


        // TextMeshPro nextText = CreateText("NextText_TMP", ChooseRoleArea.Translations.NextText, new Vector3(-6.3f, -2.2f, 0f), 3f, anchorObject);
        // nextText.alignment = TextAlignmentOptions.Right;

        TextMeshPro returnText = CreateText("ReturnText_TMP", ChooseRoleArea.Translations.ReturnText, new Vector3(6.23f, 2.2f, 0f), 3f, anchorObject);
        returnText.alignment = TextAlignmentOptions.Left;

        PassiveButton dummyButton = transform.parent.Find("CloseButton").GetComponent<PassiveButton>();
        PassiveButton returnButton = Instantiate(dummyButton, anchorObject.transform);
        returnButton.name = "ReturnButton";
        returnButton.Modify(Return);
        returnButton.transform.localRotation = Quaternion.Euler(0, 180, 0);
        returnButton.transform.localPosition = new Vector3(-4f, 2.2f, 0f);
        returnButton.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);

        PassiveButton createButton = Instantiate(dummyButton, anchorObject.transform);
        createButton.name = "ConfirmButton";
        createButton.Modify(CreateCombo);
        createButton.transform.localPosition = new Vector3(0f, -.5f, 0f);
        createButton.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Buttons/lotus_checkmark.png", 200f);

    }

    public void Open() => throw new NotSupportedException("Calling Open by itself is not supported. You must call OpenWithComboInfo.");

    public void OpenWithComboInfo(CustomRole? selectedRole1, CustomRole? selectedRole2, ComboType selectedType)
    {
        if (displayedCombo != null && IsNativeObjectAlive(displayedCombo))
            displayedCombo.gameObject.Destroy();
        targetType = selectedType;
        role1 = selectedRole1;
        role2 = selectedRole2;
        displayedCombo = anchorObject.QuickComponent<DisplayedCombo>("DisplayedCombo", new Vector3(0f, 0.65f, 0f), Vector3.one);
        displayedCombo.Setup(new RoleComboInfo
        {
            Role1EnglishName = selectedRole1?.EnglishRoleName ?? string.Empty,
            Role2EnglishName = selectedRole2?.EnglishRoleName ?? string.Empty,
            ComboType = (byte)selectedType,
        }, false);
        displayedCombo.gameObject.GetChildren(true).ForEach(go => go.layer = LayerMask.NameToLayer("UI"));
        displayedCombo.transform.FindChild("ComboTypeText").transform.localPosition += new Vector3(.7f, 0f, 0f);
        anchorObject.SetActive(true);
    }

    public void Close()
    {
        anchorObject.SetActive(false);
        if (displayedCombo != null && IsNativeObjectAlive(displayedCombo))
            displayedCombo.gameObject.Destroy();
    }

    private void Return()
    {
        Close();
        if (role2 == null) comboMenu.GetArea<ChooseRoleArea>().Open(role1);
        else comboMenu.GetArea<ChooseComboTypeArea>().OpenWithRoles(role1, role2, targetType);
    }

    private void CreateCombo()
    {
        Close();
        DevLogger.Log("creating combo");
        PluginDataManager.ComboListManager.AddCombo(new RoleComboInfo
        {
            Role1EnglishName = role1?.EnglishRoleName ?? string.Empty,
            Role2EnglishName = role2?.EnglishRoleName ?? string.Empty,
            ComboType = (byte)targetType
        });
        comboMenu.GetArea<MainMenuArea>().Open();
    }

    private static TextMeshPro CreateText(string objectName, string text, Vector3 position, float fontSize, GameObject targetObject)
    {
        TextMeshPro outputText = targetObject.QuickComponent<TextMeshPro>(objectName, position);
        outputText.fontSize = outputText.fontSizeMax = outputText.fontSizeMin = fontSize;
        outputText.font = CustomOptionContainer.GetGeneralFont();
        outputText.color = Color.white;
        outputText.text = text;
        return outputText;
    }

    [Localized("FinalizeArea")]
    private static class Translations
    {
        [Localized(nameof(TitleText))] public static string TitleText = "Are you sure this information is correct?";
        [Localized(nameof(CreateComboText))] public static string CreateComboText = "Create Combo";
    }
}