using System.Collections.Generic;
using Lotus.Extensions;
using Lotus.GUI.Menus.ComboMenu.Objects;
using Lotus.GUI.Menus.OptionsMenu;
using Lotus.Logging;
using Lotus.Managers;
using Lotus.Utilities;
using TMPro;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using xCloud;

namespace Lotus.GUI.Menus.ComboMenu.Areas;

[RegisterInIl2Cpp]
[Localized("GUI.ComboMenu")]
public class MainMenuArea: MonoBehaviour, IComboMenuArea
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(MainMenuArea));

    private readonly int MaxDisplayed = 6;

    private GameObject comboListObject;
    private GameObject anchorObject;
    private ComboMenu comboMenu;

    private SpriteRenderer downArrow;
    private SpriteRenderer upArrow;
    private int startIndex;

    private TextMeshPro presetText;

    private List<RoleComboInfo> allCombos;
    private List<DisplayedCombo> displayedCombos;

    public void Setup(HudManager _, MonoBehaviour menuBehaviour)
    {
        comboMenu = (ComboMenu)menuBehaviour;
        anchorObject = gameObject.CreateChild("MainMenu", Vector3.zero, Vector3.one);
        comboListObject = anchorObject.CreateChild("ComboList", new Vector3(-2.15f, 1.3f, 0f), Vector3.one);
        allCombos = [];
        displayedCombos = [];

        CreateText("Title_TMP", Translations.TitleText, new Vector3(0f, 2f, 0f), 4f, anchorObject)
            .alignment = TextAlignmentOptions.Center;
        CreateText("AddCombo_TMP", Translations.AddComboText, new Vector3(1.7f, 1.3f, 0f), 3f, anchorObject)
            .alignment = TextAlignmentOptions.Center;
        CreateText("ScrollDown_TMP", Translations.CycleDownText, new Vector3(1.7f, -.1f, 0f), 3f, anchorObject)
            .alignment = TextAlignmentOptions.Center;
        CreateText("ScrollUp_TMP", Translations.CycleUpText, new Vector3(1.5f, .6f, 0f), 3f, anchorObject)
            .alignment = TextAlignmentOptions.Center;

        PassiveButton dummyButton = transform.parent.Find("CloseButton").GetComponent<PassiveButton>();

        PassiveButton addComboButton = Instantiate(dummyButton, anchorObject.transform);
        addComboButton.name = "AddComboButton";
        addComboButton.Modify(GoToAddComboMenu);
        addComboButton.transform.localPosition = new Vector3(0.5f, 1.3f, 0f);
        addComboButton.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Presets/Plus.png", 200f);
        PassiveButton cycleUpButton = Instantiate(dummyButton, anchorObject.transform);
        cycleUpButton.Modify(CycleUp);
        cycleUpButton.name = "CycleUpButton";
        cycleUpButton.transform.localPosition = new Vector3(0.5f, .6f, 0f);
        cycleUpButton.transform.rotation = new(0f, 0f, .7071f, .7071f);
        upArrow = cycleUpButton.GetComponent<SpriteRenderer>();
        upArrow.sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);
        PassiveButton cycleDownButton = Instantiate(dummyButton, anchorObject.transform);
        cycleDownButton.Modify(CycleDown);
        cycleDownButton.name = "CycleDownButton";
        cycleDownButton.transform.localPosition = new Vector3(0.5f, -.1f, 0f);
        cycleDownButton.transform.rotation = new(0f, 0f, -.7071f, .7071f);
        downArrow = cycleDownButton.GetComponent<SpriteRenderer>();
        downArrow.sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);

        byte currentPreset = PluginDataManager.ComboListManager.CurrentPreset;
        presetText = CreateText("PresetText_TMP", Translations.PresetText.Formatted(currentPreset), new Vector3(1.5f, -.8f, 0f), 3f,
            anchorObject);
        presetText.alignment = TextAlignmentOptions.Center;
        PassiveButton cyclePresetLeft = Instantiate(dummyButton, anchorObject.transform);
        cyclePresetLeft.Modify(CyclePresetLeft);
        cyclePresetLeft.name = "CyclePresetLeft";
        cyclePresetLeft.transform.localPosition = new Vector3(0.5f, -.8f, 0f);
        cyclePresetLeft.transform.rotation = new Quaternion(0f, 1f, 0f ,0f);
        cyclePresetLeft.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);
        PassiveButton cyclePresetRight = Instantiate(dummyButton, anchorObject.transform);
        cyclePresetRight.Modify(CyclePresetRight);
        cyclePresetRight.name = "CyclePresetRight";
        cyclePresetRight.transform.localPosition = new Vector3(2.5f, -.8f, 0f);
        cyclePresetRight.GetComponent<SpriteRenderer>().sprite = LotusAssets.LoadSprite("Presets/Arrow.png", 200f);

        SpriteRenderer dummyDivider = FindObjectOfType<LobbyInfoPane>().FindChild<SpriteRenderer>("Divider");
        SpriteRenderer divider = Instantiate(dummyDivider, anchorObject.transform);
        divider.name = "Seperator";
        divider.transform.localScale = new Vector3(1.22f, 1f, 1f);
        divider.transform.localPosition = new Vector3(0f, -.35f, 0f);
        divider.transform.rotation = new Quaternion(0f, 0f, .7071f, .7071f);
    }

    public void Open()
    {
        RefreshList();
        UpdateDisplayedCombos();
        anchorObject.SetActive(true);
    }

    public void Close()
    {
        anchorObject.SetActive(false);
    }

    public void DeleteCombo(MonoBehaviour monoCombo)
    {
        DisplayedCombo combo = (DisplayedCombo)monoCombo;
        log.Debug("deleting combo...");
        PluginDataManager.ComboListManager.RemoveCombo(allCombos[displayedCombos.IndexOf(combo) + startIndex]);
    }

    private void GoToAddComboMenu()
    {
        Close();
        comboMenu.GetArea<ChooseRoleArea>().Open();
    }

    private void CyclePresetLeft()
    {
        PluginDataManager.ComboListManager.ChangePreset(-1);
        RefreshList();
        UpdateDisplayedCombos();
    }

    private void CyclePresetRight()
    {
        PluginDataManager.ComboListManager.ChangePreset(1);
        RefreshList();
        UpdateDisplayedCombos();
    }

    private void CycleDown()
    {
        if (startIndex + 1 + MaxDisplayed > allCombos.Count) return;
        startIndex++;
        UpdateCycleArrows();
        UpdateDisplayedCombos();
    }

    private void CycleUp()
    {
        if (startIndex - 1 < 0) return;
        startIndex--;
        UpdateCycleArrows();
        UpdateDisplayedCombos();
    }

    private void RefreshList()
    {
        allCombos = PluginDataManager.ComboListManager.ListCombos;
        presetText.text = Translations.PresetText.Formatted(PluginDataManager.ComboListManager.CurrentPreset);

        startIndex = 0;
        UpdateCycleArrows();
    }

    private void UpdateDisplayedCombos()
    {
        log.Debug("updating displayed combos.");

        foreach (var combo in displayedCombos)
            if (combo != null && IsNativeObjectAlive(combo)) combo.gameObject.Destroy();

        displayedCombos = [];

        float height = 0f;
        for (int i = startIndex; i < MaxDisplayed + startIndex; i++)
        {
            if (i == allCombos.Count) break;
            RoleComboInfo comboInfo = allCombos[i];
            DisplayedCombo displayed = comboListObject.QuickComponent<DisplayedCombo>($"Combo{i}", new Vector3(0, height, 0), Vector3.one);
            displayed.Setup(comboInfo);
            displayed.gameObject.GetChildren(true).ForEach(go => go.layer = LayerMask.NameToLayer("UI"));
            displayedCombos.Add(displayed);
            height -= .65f;
        }
    }

    private void UpdateCycleArrows()
    {
        if (startIndex == 0)
        {
            Color color = upArrow.color;
            color.a = 0.5f;
            upArrow.color = color;
        } else upArrow.color = Color.white;

        if (startIndex + 1 + MaxDisplayed > allCombos.Count)
        {
            Color color = upArrow.color;
            color.a = 0.5f;
            downArrow.color = color;
        }
        else downArrow.color = Color.white;
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

    [Localized("MainArea")]
    private static class Translations
    {
        [Localized(nameof(TitleText))] public static string TitleText = "Edit Combo Menu";
        [Localized(nameof(AddComboText))] public static string AddComboText = "Add Combo";
        [Localized(nameof(CycleUpText))] public static string CycleUpText = "Scroll Up";
        [Localized(nameof(CycleDownText))] public static string CycleDownText = "Scroll Down";
        [Localized(nameof(PresetText))] public static string PresetText = "Preset {0}";
    }
}