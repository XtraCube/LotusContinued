extern alias JBAnnotations;
using System;
using System.Collections.Generic;
using System.Linq;
using JBAnnotations::JetBrains.Annotations;
using Lotus.Roles;
using Lotus.Roles.RoleGroups.CTF;

namespace Lotus.GameModes.CTF;

public class CTFRoles : RoleHolder
{
    public override List<Action> FinishedCallbacks() => Callbacks;

    public static List<Action> Callbacks { get; set; } = new List<Action>();

    public static CTFRoles Instance = null!;

    public StaticRoles Static;

    public CTFRoles()
    {
        Instance = this;

        AllRoles = new List<CustomRole>();
        Static = new StaticRoles();

        AllRoles.AddRange(Static.GetType()
            .GetFields()
            .Select(f => (CustomRole)f.GetValue(Static)!)
            .ToList());

        // solidify every role to finish them off
        AllRoles.ForEach(r => r.Solidify());
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public class StaticRoles
    {
        public Striker Striker = new();
    }
}