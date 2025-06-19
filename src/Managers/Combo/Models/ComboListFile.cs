using System;
using System.Collections.Generic;
using Lotus.GUI.Menus.ComboMenu.Objects;

namespace Lotus.Managers.Combo.Models;

public class ComboListFile
{
    public List<RoleComboInfo>? Preset1 { get; set; } = new();
    public List<RoleComboInfo>? Preset2 { get; set; } = new();
    public List<RoleComboInfo>? Preset3 { get; set; } = new();
    public List<RoleComboInfo>? Preset4 { get; set; } = new();
    public List<RoleComboInfo>? Preset5 { get; set; } = new();

    public byte CurrentPreset { get; set; } = 1;

    public List<RoleComboInfo>? GetCurrentCombos()
    {
        switch (CurrentPreset)
        {
            case 1:
                return Preset1;
            case 2:
                return Preset2;
            case 3:
                return Preset3;
            case 4:
                return Preset4;
            case 5:
                return Preset5;
            default:
                throw new ArgumentOutOfRangeException($"{CurrentPreset} is not between 1-5");
        }
    }
}