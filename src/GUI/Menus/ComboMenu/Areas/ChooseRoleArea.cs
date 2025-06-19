using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Lotus.Extensions;
using Lotus.GUI.Menus.ComboMenu.Objects;
using Lotus.GUI.Menus.OptionsMenu;
using Lotus.Managers;
using Lotus.Roles;
using Lotus.Roles.Internals.Enums;
using Lotus.Utilities;
using TMPro;
using UnityEngine;
using VentLib.Localization.Attributes;
using VentLib.Options.UI.Controllers.Search;
using VentLib.Utilities.Attributes;
using VentLib.Utilities.Extensions;
using Random = UnityEngine.Random;

namespace Lotus.GUI.Menus.ComboMenu.Areas;

[RegisterInIl2Cpp]
[Localized("GUI.ComboMenu")]
public class ChooseRoleArea : MonoBehaviour, IComboMenuArea
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(ChooseRoleArea));

    private const int MaxDisplayedRoles = 5;

    private GameObject anchorObject = null!;
    private TextMeshPro titleText = null!;
    private ComboMenu comboMenu = null!;

    private GameObject roleListObject = null!;

    private TextMeshPro selectedRolesText = null!;
    private TextMeshPro nothingFoundText = null!;
    private TextMeshPro searchVagueText = null!;

    private CustomRole? role1;
    private CustomRole? role2;

    private FreeChatInputField inputField = null!;
    private SpriteRenderer nextArrow = null!;

    private bool animationDebounce;
    private bool stillSearching;

    private string oldText = string.Empty;

    public void Setup(HudManager hudManager, MonoBehaviour menuBehaviour)
    {
        comboMenu = (ComboMenu)menuBehaviour;
        anchorObject = gameObject.CreateChild("CreateCombo", Vector3.zero, Vector3.one);
        roleListObject = anchorObject.CreateChild("RoleList", new Vector3(0f, 1.2f, -1f), Vector3.one);
        titleText = CreateText("TitleText_TMP", string.Empty, new Vector3(0f, 2f, 0f), 3.7f, anchorObject);
        titleText.alignment = TextAlignmentOptions.Center;

        searchVagueText = CreateText("SearchVagueText_TMP", Translations.SearchTooVague, new Vector3(0f, 0f, -1f), 3f, anchorObject);
        searchVagueText.alignment = TextAlignmentOptions.Center;
        searchVagueText.gameObject.SetActive(false);
        nothingFoundText = CreateText("NothingFoundText_TMP", Translations.NothingFound, new Vector3(0f, 0f, -1f), 3f, anchorObject);
        nothingFoundText.alignment = TextAlignmentOptions.Center;
        nothingFoundText.gameObject.SetActive(false);

        selectedRolesText = CreateText("SelectedRolesText_TMP", Translations.SelectedRoles, new Vector3(3.1f, 0f, 0f), 2.5f, anchorObject);
        selectedRolesText.alignment = TextAlignmentOptions.Center;

        TextMeshPro nextText = CreateText("NextText_TMP", Translations.NextText, new Vector3(-6.3f, -2.2f, 0f), 3f,
            anchorObject);
        nextText.alignment = TextAlignmentOptions.Right;

        TextMeshPro returnText = CreateText("ReturnText_TMP", Translations.ReturnText, new Vector3(6.23f, 2.2f, 0f), 3f,
            anchorObject);
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

        var searchBar = new SearchBar(
            () => LotusAssets.LoadSprite("searchbar.png", 300f),
            () => LotusAssets.LoadSprite("searchicon.png", 100f)
            );
        inputField = Instantiate(hudManager.Chat.freeChatField, anchorObject.transform);
        inputField.transform.localPosition = new Vector3(0f, -2.08f, -1f);
        inputField.transform.localScale = new Vector3(0.5f, 1f, 1f);
        inputField.name = "RoleSearchBar";

        inputField.textArea.characterLimit = 20;
        inputField.FindChild<TextMeshPro>("CharCounter (TMP)", true).gameObject.SetActive(false);
        inputField.FindChild<PassiveButton>("ChatSendButton", true).gameObject.SetActive(false);

        inputField.FindChild<SpriteRenderer>("Background", true).sprite = searchBar.SearchBarSprite();
        inputField.FindChild<SpriteRenderer>("Background", true).color = Color.white;
        inputField.FindChild<TextBoxTMP>("TextArea").transform.localScale = new Vector3(2, 1, 1);

        SpriteRenderer searchIcon = inputField.gameObject.QuickComponent<SpriteRenderer>("Icon",
            new Vector3(searchBar.IconLocalPosition().x, searchBar.IconLocalPosition().y, -1f),
            new Vector3(0.1f, 0.05f, 1f));
        searchIcon.sprite = searchBar.SearchIconSprite();
    }

    public void Open()
    {
        titleText.text = Translations.SelectRolesText;
        inputField.textArea.SetText(string.Empty);
        role1 = null;
        role2 = null;
        UpdateNextButton();
        UpdateSelectedText();
        anchorObject.SetActive(true);
    }
    public void Open(CustomRole? setRole1, CustomRole? setRole2 = null)
    {
        titleText.text = Translations.SelectRolesText;
        role1 = setRole1;
        role2 = setRole2;
        UpdateNextButton();
        UpdateSelectedText();
        anchorObject.SetActive(true);
    }

    public void Close()
    {
        anchorObject.SetActive(false);
    }

    public void AddRole(CustomRole role)
    {
        if (role1 == null)
            role1 = role;
        else if (role2 == null)
            role2 = role;
        else
        {
            // Override role2 anyway and regenerate current selection to prevent people from seeing 3 selected roles.
            role2 = role;
            oldText = String.Empty;
        }
        UpdateNextButton();
        UpdateSelectedText();
    }

    public void RemoveRole(CustomRole role)
    {
        if (role1 == role)
        {
            role1 = null;
            if (role2 != null)
            {
                role1 = role2;
                role2 = null;
            }
        }
        else if (role2 == role) role2 = null;
        UpdateNextButton();
        UpdateSelectedText();
        oldText = String.Empty; // Regenerate current selection.
    }

    private void FixedUpdate()
    {
        if (inputField == null) return;
        // if (inputField.Text.Length < 3) return;
        if (stillSearching) return;
        string searchedText = inputField.Text.Trim().ToLower();
        if (searchedText == oldText) return;
        stillSearching = true;
        oldText = searchedText;
        if (oldText == string.Empty)
        {
            roleListObject.transform.DestroyChildren();
            searchVagueText.gameObject.SetActive(false);
            nothingFoundText.gameObject.SetActive(false);
            stillSearching = false;
            return;
        }

        List<CustomRole> allFoundRoles = GlobalRoleManager.Instance.AllCustomRoles()
            .Where(r => (r.RoleName.ToLowerInvariant().Contains(searchedText) || r.Aliases.Contains(searchedText))
                        && r.GetRoleType() is RoleType.Normal or RoleType.Variation)
            .ToList();
        roleListObject.transform.DestroyChildren();
        if (allFoundRoles.Count > MaxDisplayedRoles)
        {
            searchVagueText.gameObject.SetActive(true);
            nothingFoundText.gameObject.SetActive(false);
            stillSearching = false;
            return;
        }

        if (!allFoundRoles.Any())
        {
            searchVagueText.gameObject.SetActive(false);
            nothingFoundText.gameObject.SetActive(true);
            stillSearching = false;
            return;
        }

        searchVagueText.gameObject.SetActive(false);
        nothingFoundText.gameObject.SetActive(false);

        float height = 0f;
        foreach (CustomRole role in allFoundRoles)
        {
            RoleAreaOption displayedRole = roleListObject.QuickComponent<RoleAreaOption>(role.EnglishRoleName, new Vector3(0, height, 0), Vector3.one);
            displayedRole.Setup(role, role1 == role || role2 == role, this);
            height -= .65f;
        }
        roleListObject.GetChildren(true).ForEach(go => go.layer = LayerMask.NameToLayer("UI"));

        stillSearching = false;
    }

    private void Return()
    {
        if (role1 == null)
        {
            Close();
            comboMenu.GetArea<MainMenuArea>().Open();
        }
        else if (role2 == null)
        {
            role1 = null;
            UpdateNextButton();
            UpdateSelectedText();
            oldText = String.Empty;
        }
        else
        {
            role2 = null;
            UpdateNextButton();
            UpdateSelectedText();
            oldText = String.Empty;
        }
    }

    private void Next()
    {
        if (animationDebounce) return;
        if (role1 == null)
        {
            animationDebounce = true;
            StartCoroutine(ShakeButton().WrapToIl2Cpp());
            return;
        }
        Close();
        if (role2 == null) comboMenu.GetArea<FinalizeArea>().OpenWithComboInfo(role1, null, ComboType.Forced);
        else comboMenu.GetArea<ChooseComboTypeArea>().OpenWithRoles(role1, role2);
    }

    private void UpdateNextButton()
    {
        if (role1 == null)
        {
            Color color = nextArrow.color;
            color.a = 0.5f;
            nextArrow.color = color;
        } else nextArrow.color = Color.white;
    }

    private void UpdateSelectedText()
    {
        string baseText = Translations.SelectedRoles;
        if (role1 != null) baseText += $"\n{role1.ColoredRoleName()}";
        if (role2 != null) baseText += $"\n{role2.ColoredRoleName()}";
        selectedRolesText.text = baseText;
    }

    private IEnumerator ShakeButton()
    {
        Vector3 currentPosition = nextArrow.transform.localPosition;
        float elapsed = 0f;
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime;
            nextArrow.transform.localPosition = currentPosition + (Vector3)Random.insideUnitCircle * 0.05f;
            yield return null;
        }
        nextArrow.transform.localPosition = currentPosition;
        animationDebounce = false;
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

    [Localized("CreateComboArea")]
    public static class Translations
    {
        [Localized(nameof(NothingFound))] public static string NothingFound = "There are no roles that have the name you inputted.\nDid you make a typo?";
        [Localized(nameof(SelectRolesText))] public static string SelectRolesText = "Select the roles for your combo,\nthen click the next button.";
        [Localized(nameof(SearchTooVague))] public static string SearchTooVague = "Your search is too vague!\nType more of the rolename.";
        [Localized(nameof(SelectedRoles))] public static string SelectedRoles = "Selected Roles:";
        [Localized(nameof(ReturnText))] public static string ReturnText = "Return";
        [Localized(nameof(NextText))] public static string NextText = "Next";
    }
}