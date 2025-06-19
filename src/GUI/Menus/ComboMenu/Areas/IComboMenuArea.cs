using UnityEngine;
using VentLib.Localization.Attributes;

namespace Lotus.GUI.Menus.ComboMenu.Areas;


public interface IComboMenuArea
{
    public void Setup(HudManager hudManager, MonoBehaviour menuBehaviour);

    public void Open();

    public void Close();
}