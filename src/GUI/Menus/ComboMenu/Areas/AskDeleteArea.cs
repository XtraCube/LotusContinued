using System;
using Lotus.Extensions;
using Lotus.GUI.Menus.ComboMenu.Objects;
using Lotus.GUI.Menus.OptionsMenu;
using Lotus.Utilities;
using TMPro;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;

namespace Lotus.GUI.Menus.ComboMenu.Areas;

[RegisterInIl2Cpp]
[Localized("GUI.ComboMenu")]
public class AskDeleteArea: MonoBehaviour, IComboMenuArea
{
    private DisplayedCombo targetCombo;
    private GameObject anchorObject;
    private ComboMenu comboMenu;

    public void Setup(HudManager _, MonoBehaviour menuBehaviour)
    {
        comboMenu = (ComboMenu)menuBehaviour;
        anchorObject = gameObject.CreateChild("AskDeleteArea", Vector3.zero, Vector3.one);
        CreateText("Title_TMP", Translations.TitleText, new Vector3(0f, 2f, 0f), 4f, anchorObject)
            .alignment = TextAlignmentOptions.Center;

        PassiveButton dummyButton = transform.parent.Find("CloseButton").GetComponent<PassiveButton>();
        PassiveButton confirmButton = Instantiate(dummyButton, anchorObject.transform);
        confirmButton.name = "ConfirmButton";
        confirmButton.Modify(Confirm);
        confirmButton.transform.localPosition = new Vector3(-0.5f, -.5f, 0f);
        confirmButton.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Buttons/lotus_checkmark.png", 200f);
        PassiveButton deletionButton = Instantiate(dummyButton, anchorObject.transform);
        deletionButton.name = "DeletionButton";
        deletionButton.Modify(Deny);
        deletionButton.transform.localPosition = new Vector3(0.5f, -.5f, 0f);
        deletionButton.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Buttons/lotus_x.png", 200f);
    }

    public void Open() => throw new NotSupportedException("Call 'OpenWithCombo' instead of just Open.");
    public void OpenWithCombo(MonoBehaviour deletedCombo)
    {
        if (targetCombo != null && IsNativeObjectAlive(targetCombo))
            targetCombo.gameObject.Destroy();
        targetCombo = (DisplayedCombo)deletedCombo;
        targetCombo.ToggleDeleteButton(false);
        targetCombo.transform.SetParent(anchorObject.transform);
        targetCombo.transform.localPosition = new Vector3(0f, 0.65f, 0f);
        targetCombo.transform.FindChild("ComboTypeText").transform.localPosition += new Vector3(.7f, 0f, 0f);
        anchorObject.SetActive(true);
    }

    public void Close()
    {
        anchorObject.SetActive(false);
    }

    private void Confirm()
    {
        Close();
        var menuArea = comboMenu.GetArea<MainMenuArea>();
        menuArea.DeleteCombo(targetCombo);
        targetCombo.gameObject.Destroy();
        menuArea.Open();
    }
    private void Deny()
    {
        Close();
        targetCombo.gameObject.Destroy();
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

    [Localized("DeleteArea")]
    private static class Translations
    {
        [Localized(nameof(TitleText))] public static string TitleText = "Are you sure you want\nto delete this combo?";
    }
}