using System.Collections.Generic;
using AmongUs.GameOptions;
using Lotus.API.Stats;
using Lotus.Factions;
using Lotus.Roles.RoleGroups.Stock;
using UnityEngine;

namespace Lotus.Roles.RoleGroups.Vanilla;

public class Crewmate : TaskRoleBase
{
    protected override RoleModifier Modify(RoleModifier roleModifier) =>
        roleModifier
            .VanillaRole(RoleTypes.Crewmate)
            .Faction(FactionInstances.Crewmates)
            .RoleColor(ModConstants.Palette.CrewmateColor)
            .CanVent(false);

    public override List<Statistic> Statistics() => new() { VanillaStatistics.TasksComplete };
}

