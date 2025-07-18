using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VentLib.Commands;
using VentLib.Commands.Attributes;
using VentLib.Commands.Interfaces;
using VentLib.Localization.Attributes;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;

namespace Lotus.Chat.Commands;

[Localized("Commands.Poll")]
[Command(CommandFlag.LobbyOnly, "gmpoll", "gamemodepoll")]
public class GamemodePollCommand : ICommandReceiver
{
    private static bool _isPollActive;
    private static int _pollDuration = 60;

    private Dictionary<int, int> gamemodeVotes = [];
    private Dictionary<byte, int> playerVotes = [];
    private DateTime startTime = DateTime.Now;

    [Localized("PollStarted")] private static string _pollStarted =
        "A poll to change the current gamemode has started!\n\nPlease vote for your desired game mode using the command /gmpoll vote (id)\n\nThis poll will expire in {0}s";

    [Localized("PollEnded")]
    private static string _pollEnded = "The poll has ended! The winning gamemode is {0} with {1} votes.";
    [Localized("PollEndedTie")]
    private static string _pollEndedTie = "The poll has ended!\nMultiple gamemodes had the same votes, so it was randomly selected to be {0} with {1} votes.";

    [Localized("VoteMessage")] private static string _voteMessage = "You have casted your vote for {0}!";
    [Localized("NoVotes")] private static string _noVotes = "The poll is over, No votes were cast.";

    [Localized("NoPollActive")] private static string _noPollActive = "No poll is currently in progress.";
    [Localized("PollActive")] private static string _pollActive = "There is already an active poll.";
    [Localized("InvalidID")] private static string _invalidID = "Invalid gamemode ID. Here are the available ones:";
    [Localized("NoIdInCommand")] private static string _noIDInCommand = "Please provide the ID of the gamemode you wish to vote. Here are the available ones:";
    [Localized("CurrentStandings")] private static string _currentStandings = "{0}\nThere are {1} seconds left.";
    [Localized("Hint")] private static string _helperMessage = "There is currently an active poll, you can vote using <b>/gmpoll vote</b>.";

    [Localized("AvailableModesTitle")] private static string _availableModesTitle = "Available Gamemodes";
    [Localized("CurrentStandingsTitle")] private static string _currentStandingsTitle = "Current Standings";

    [Command("start")]
    private void PollStartCommand(PlayerControl source, CommandContext context)
    {
        if (_isPollActive)
        {
            ChatHandlers.NotPermitted(_pollActive).Send(source);
            return;
        }

        if (context.Args.Length > 0 && int.TryParse(context.Args[0], out int duration))
            _pollDuration = duration;

        _isPollActive = true;

        ChatHandler.Of(_pollStarted.Formatted(_pollDuration)).Send();
        StartPoll();
    }

    [Command("vote")]
    private void PollVoteCommand(PlayerControl source, CommandContext context)
    {
        if (!_isPollActive)
        {
            ChatHandlers.NotPermitted(_noPollActive).Send(source);
            return;
        }

        if (context.Args.Length == 0 || !int.TryParse(context.Args[0], out int gamemode))
        {
            ChatHandlers.InvalidCmdUsage(_noIDInCommand + "\n" + GetGamemodeOptions()).Send(source);
            return;
        }

        if (gamemode < 0 || gamemode >= ProjectLotus.GameModeManager.GetGameModes().Count())
        {
            ChatHandlers.InvalidCmdUsage(_invalidID + "\n" + GetGamemodeOptions()).Send(source);
            return;
        }

        if (playerVotes.TryGetValue(source.PlayerId, out int votedId)) gamemodeVotes[votedId]--;

        playerVotes[source.PlayerId] = gamemode;
        gamemodeVotes[gamemode] = gamemodeVotes.ContainsKey(gamemode) ? gamemodeVotes[gamemode] + 1 : 1;
        ChatHandler.Of(_voteMessage.Formatted(ProjectLotus.GameModeManager.GetGameMode(gamemode).Name)).Send(source);
    }

    [Command("view")]
    private void ViewStandsings(PlayerControl source, CommandContext context)
    {
        if (!_isPollActive)
        {
            ChatHandlers.NotPermitted(_noPollActive).Send(source);
            return;
        }

        int timeRemaining = (DateTime.Now - startTime).Seconds;
        ChatHandler.Of(_currentStandings.Formatted(GetStandingsList(), 0), _currentStandingsTitle).Send(source);
    }

    private void StartPoll()
    {
        gamemodeVotes.Clear();
        playerVotes.Clear();
        startTime = DateTime.Now;
        ChatHandler.Of(GetGamemodeOptions(), _availableModesTitle).Send();
        Async.Schedule(EndPoll, _pollDuration);
    }

    private void EndPoll()
    {
        if (gamemodeVotes.Count == 0)
        {
            ChatHandler.Of(_noVotes).Send();
            ResetPoll();
            return;
        }

        int highestVote = gamemodeVotes.Values.Max();
        var topGamemodes = gamemodeVotes
            .Where(kvp => kvp.Value == highestVote)
            .Select(kvp => kvp.Key)
            .ToList();
        bool isTie = topGamemodes.Count > 1;


        int winningGamemodeId = topGamemodes.GetRandom();
        int highestVotes = gamemodeVotes[winningGamemodeId];

        ProjectLotus.GameModeManager.SetGameMode(winningGamemodeId);
        if (isTie) ChatHandler.Of(_pollEndedTie.Formatted(ProjectLotus.GameModeManager.GetGameMode(winningGamemodeId).Name, highestVotes)).Send();
        else ChatHandler.Of(_pollEnded.Formatted(ProjectLotus.GameModeManager.GetGameMode(winningGamemodeId).Name, highestVotes)).Send();

        ResetPoll();
    }

    private string GetGamemodeOptions()
    {
        var gamemodeManager = ProjectLotus.GameModeManager;
        StringBuilder gamemodeOptions = new();
        for (int i = 0; i < gamemodeManager.GetGameModes().Count(); i++)
        {
            var gamemode = gamemodeManager.GetGameMode(i);
            gamemodeOptions.AppendLine($"{i}: {gamemode.Name}");
        }

        return gamemodeOptions.ToString();
    }

    private string GetStandingsList()
    {
        var gamemodeManager = ProjectLotus.GameModeManager;
        StringBuilder standings = new();
        foreach (var kvp in gamemodeVotes.OrderByDescending(kvp => kvp.Value))
        {
            var gamemode = gamemodeManager.GetGameMode(kvp.Key);
            standings.AppendLine($"{gamemode.Name} ({kvp.Key}): {kvp.Value}");
        }
        return standings.ToString();
    }

    private void ResetPoll()
    {
        gamemodeVotes.Clear();
        playerVotes.Clear();
        _pollDuration = 60;
        _isPollActive = false;
    }

    public void Receive(PlayerControl source, CommandContext context)
    {
        if (!_isPollActive) return;
        ChatHandler.Of(_helperMessage).Send(source);
        ChatHandler.Of(GetGamemodeOptions(), _availableModesTitle).Send(source);
    }
}