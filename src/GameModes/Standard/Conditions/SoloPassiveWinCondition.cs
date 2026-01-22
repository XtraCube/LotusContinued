using System.Collections.Generic;
using System.Linq;
using Lotus.Factions.Neutrals;
using Lotus.Victory.Conditions;
using Lotus.Extensions;
using Lotus.API.Player;

namespace Lotus.GameModes.Standard.Conditions;
public class SoloPassiveWinCondition : IWinCondition
{
    public bool IsConditionMet(out List<PlayerControl> winners)
    {
        winners = null!;
        List<PlayerControl> allPlayers = Players.GetAllPlayers().ToList();
        if (allPlayers.Count != 1) return false;

        PlayerControl lastPlayer = allPlayers[0];
        winners = new List<PlayerControl> { lastPlayer };
        return lastPlayer.PrimaryRole().Faction is INeutralFaction;
    }

    public WinReason GetWinReason() => new(ReasonType.FactionLastStanding);
}