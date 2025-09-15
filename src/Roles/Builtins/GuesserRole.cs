using System;
using AmongUs.GameOptions;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.Chat;
using Lotus.Extensions;
using Lotus.Logging;
using Lotus.Managers;
using Lotus.Roles.Interactions;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Internals.Attributes;
using Lotus.Roles.Internals.Trackers;
using VentLib.Localization.Attributes;
using VentLib.Options.UI;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;
using VentLib.Utilities.Optionals;
using Lotus.GameModes.Standard;
using Lotus.API.Vanilla.Meetings;
using Lotus.Roles.Managers.Interfaces;
using System.Linq;
using Lotus.Roles.Subroles;
using Lotus.GUI.Name.Components;
using Lotus.GUI.Name.Holders;
using System.Collections.Generic;
using System.Text;
using Lotus.API;
using Lotus.Factions.Interfaces;
using Lotus.GUI;
using Lotus.GUI.Name;
using Lotus.Managers.History.Events;
using Lotus.Roles.Interfaces;
using Lotus.RPC.CustomObjects.Interfaces;
using Lotus.Utilities;
using UnityEngine;
using VentLib.Utilities.Collections;
using static Lotus.Roles.Subroles.Guesser;
using CollectionExtensions = HarmonyLib.CollectionExtensions;

namespace Lotus.Roles.Builtins;

public class GuesserRole : CustomRole, IInfoResender
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(GuesserRole));

    private int guessesPerMeeting;
    private bool followGuesserSettings;

    private byte guessingPlayer = byte.MaxValue;
    private bool skippedVote;
    private CustomRole? guessedRole;
    private int guessesThisMeeting;

    private bool currentlyGuessing;
    private bool isShapeshifterRole;
    private bool wasShiftingRole;
    private bool meetingEnded;
    private int currentPageNum;

    private RoleTypes? originalRoleType;

    private IFaction lastFaction;
    public Cooldown shiftTimer;

    [NewOnSetup(true)] private MeetingPlayerSelector voteSelector = new();
    [NewOnSetup] private List<GuesserShapeshifterObject> shapeshifterObjects = [];
    [NewOnSetup] private List<CustomRole> guessableRoles = [];

    public void ResendMessages()
    {
        GuesserMessage(Translations.HintMessage.Formatted(guessesThisMeeting)).Send(MyPlayer);
    }


    [RoleAction(LotusActionType.RoundEnd)]
    public void ResetPreppedPlayer()
    {
        voteSelector.Reset();
        guessingPlayer = byte.MaxValue;
        skippedVote = false;
        meetingEnded = false;
        currentlyGuessing = false;
        guessedRole = null;
        guessesThisMeeting = guessesPerMeeting;
        if (lastFaction != Faction) ResetGuessableRoles();
        ResendMessages();
    }

    [RoleAction(LotusActionType.Vote, priority:Priority.VeryHigh)]
    public void SelectPlayerToGuess(Optional<PlayerControl> player, MeetingDelegate _, ActionHandle handle)
    {
        if (skippedVote || guessesThisMeeting <= 0 || meetingEnded) return;
        handle.Cancel();
        if (currentlyGuessing) return;
        VoteResult result = voteSelector.CastVote(player);
        switch (result.VoteResultType)
        {
            case VoteResultType.None:
                break;
            case VoteResultType.Skipped:
                skippedVote = true;
                GuesserMessage(Translations.SkippedGuessing).Send(MyPlayer);
                break;
            case VoteResultType.Selected:
                PlayerControl? targetPlayer = Players.FindPlayerById(result.Selected);
                if (targetPlayer != null)
                {
                    CancelGuessReason reason = CanGuessPlayer(targetPlayer);
                    if (reason is CancelGuessReason.None) StartGuessingPlayer(targetPlayer);
                    else
                        GuesserMessage(reason switch
                        {
                            CancelGuessReason.RoleSpecificReason => Translations.CantGuessBecauseOfRole.Formatted(targetPlayer.name),
                            CancelGuessReason.Teammate => Translations.CantGuessTeammate.Formatted(targetPlayer.name),
                            CancelGuessReason.CanSeeRole => Translations.CantGuessKnownRole.Formatted(targetPlayer.name),
                            _ => throw new ArgumentOutOfRangeException()
                        }).Send(MyPlayer);

                }
                break;
            case VoteResultType.Confirmed:
                if (guessedRole == null)
                {
                    voteSelector.Reset();
                    SelectPlayerToGuess(player, _, handle);
                    return;
                }

                PlayerControl? guessed = Players.FindPlayerById(guessingPlayer);
                if (guessed == null) return;

                guessesThisMeeting -= 1;
                if (guessesThisMeeting <= 0) GuesserMessage(Translations.NoGuessesLeft).Send(MyPlayer);

                guessingPlayer = byte.MaxValue;
                voteSelector.Reset();

                bool successfulGuess = guessed.PrimaryRole().GetType() == guessedRole.GetType() ||
                                       guessed.GetSubroles().Any(s => s.GetType() == guessedRole.GetType());
                guessedRole = null;

                if (successfulGuess) HandleCorrectGuess(guessed, guessedRole!);
                else HandleBadGuess();
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    [RoleAction(LotusActionType.Shapeshift, priority:Priority.VeryHigh)]
    public void SelectRoleToGuess(PlayerControl target, ActionHandle handle)
    {
        if (MeetingHud.Instance) handle.Cancel(ActionHandle.CancelType.Soft);
        if (!currentlyGuessing || !isShapeshifterRole) return;

        var firstObject = shapeshifterObjects.FirstOrDefault(o => o.RealPlayer?.NetId == target.NetId || o.NetObject?.playerControl.NetId == target.NetId);
        if (firstObject == null) return;
        HandleShapeshifterChoice(firstObject);
    }

    [RoleAction(LotusActionType.Disconnect, ActionFlag.GlobalDetector)]
    public void FillDisconnects(PlayerControl target)
    {
        if (target.PlayerId == MyPlayer.PlayerId)
        {
            shiftTimer.Finish(true);
            DeleteAllShapeshifterObjects();
            return;
        }

        var firstObject = shapeshifterObjects.FirstOrDefault(o => o.RealPlayer?.NetId == target.NetId || o.NetObject?.playerControl.NetId == target.NetId);
        if (firstObject == null) return;
        shiftTimer.Finish();
    }

    [RoleAction(LotusActionType.MeetingEnd, ActionFlag.WorksAfterDeath)]
    public void CheckRevive()
    {
        shiftTimer.Finish(true);
        meetingEnded = true;
        if (wasShiftingRole)
        {
            wasShiftingRole = false;
            isShapeshifterRole = false;
            DesyncRole = originalRoleType;
            RoleTypes targetRole = RealRole;
            if (!MyPlayer.IsAlive()) targetRole = targetRole.GhostEquivalent();
            MyPlayer.RpcSetRoleDesync(targetRole, MyPlayer);
            originalRoleType = null;
        }
        DeleteAllShapeshifterObjects();
    }

    private void StartGuessingPlayer(PlayerControl targetPlayer)
    {
        GuesserMessage(Translations.PickedPlayerText.Formatted(targetPlayer.name)).Send(MyPlayer);
        guessingPlayer = targetPlayer.PlayerId;
        currentlyGuessing = true;
        isShapeshifterRole = true;
        currentPageNum = 1;

        if (!wasShiftingRole)
        {
            wasShiftingRole = true;
            if (DesyncRole.HasValue) originalRoleType = DesyncRole.Value;
            else originalRoleType = null;

            DesyncRole = RoleTypes.Shapeshifter;
        }

        ResetShapeshfiterObjects();
        MyPlayer.RpcSetRoleDesync(RoleTypes.Shapeshifter, MyPlayer);
        if (!shiftTimer.IsCoroutine)
        {
            shiftTimer.IsCoroutine = true;
            CooldownManager.SubmitCooldown(shiftTimer);
        }
        shiftTimer.StartThenRun(CancelGuessingTimerDelay, ShiftTimer);
        SendCnoRows();
    }

    private void CancelGuessingTimerDelay()
    {
        isShapeshifterRole = false;
        guessedRole = null;
        voteSelector.Reset();
        currentlyGuessing = false;
        guessingPlayer = byte.MaxValue;
        MyPlayer.RpcSetRoleDesync(RoleTypes.Crewmate, MyPlayer);
        GuesserMessage(Translations.KickedFromGuessing).Send(MyPlayer);
        DeleteAllShapeshifterObjects();
    }

    private void ResetGuessableRoles()
    {
        lastFaction = Faction;
        guessableRoles = [];

        guessableRoles = StandardRoles.Instance.AllRoles
            .Where(r => HostTurnedRoleOn(r)
                        || r.GetRoleType() is RoleType.DontShow && r.LinkedRoles().Any(HostTurnedRoleOn)
                        || r.RoleFlags.HasFlag(RoleFlag.VariationRole) && r.LinkedRoles().Any(HostTurnedRoleOn)
                        || r.RoleFlags.HasFlag(RoleFlag.TransformationRole) && r.LinkedRoles().Any(HostTurnedRoleOn))
            .Where(r =>
            {
                bool roleAllowsGuess = CanGuessRole(r);
                if (!roleAllowsGuess || !followGuesserSettings) return roleAllowsGuess;
                int setting = -1;
                RoleTypeBuilders.FirstOrOptional(b => b.predicate(r))
                    .IfPresent(rtb => setting = RoleTypeSettings[RoleTypeBuilders.IndexOf(rtb)]);
                return setting == -1 || setting == 2 ? CanGuessDictionary.GetValueOrDefault(r.GetType(), -1) == 1 : setting == 1;
            })
            .ToList();

        bool HostTurnedRoleOn(CustomRole r) => (r.Count > 0 || r.RoleFlags.HasFlag(RoleFlag.RemoveRoleMaximum)) &&
                                               (r.Chance > 0 || r.RoleFlags.HasFlag(RoleFlag.RemoveRolePercent));
    }

    private void ResetShapeshfiterObjects()
    {
        DeleteAllShapeshifterObjects();

        int playerIndex = 0;
        foreach (PlayerControl player in Players.GetAlivePlayers())
        {
            if (player.PlayerId == MyPlayer.PlayerId) continue;
            shapeshifterObjects.Add(new GuesserShapeshifterObject(MyPlayer, playerIndex, GetNameFromIndex(playerIndex), player));
            playerIndex += 1;
        }

        int leftOverPlayers = 15 - playerIndex;
        for (int i = 0; i < leftOverPlayers; i++)
        {
            // Create a NET OBJECT instead of using a player.
            shapeshifterObjects.Add(new GuesserShapeshifterObject(MyPlayer, playerIndex, GetNameFromIndex(playerIndex), null));
            playerIndex += 1;
        }

        return;

        string GetNameFromIndex(int thisIndex)
        {
            switch (thisIndex)
            {
                case 0:
                    return Color.white.Colorize(LeftArrow);
                case 1:
                    return Color.white.Colorize(Translations.PageIndex.Formatted(currentPageNum, Mathf.CeilToInt(guessableRoles.Count / 12f)));
                case 2:
                    return Color.white.Colorize(RightArrow);
                default:
                    int listIndex = thisIndex - 3;
                    listIndex = (currentPageNum - 1) * 12 + listIndex;
                    if (listIndex >= guessableRoles.Count) return NoRoleHere;
                    return guessableRoles[listIndex].ColoredRoleName();
            }
        }
    }

    private void DeleteAllShapeshifterObjects()
    {
        shapeshifterObjects.ForEach(o => o.Delete());
        shapeshifterObjects = [];
    }

    private void HandleShapeshifterChoice(GuesserShapeshifterObject shiftableObject)
    {
        switch (shiftableObject.PlayerIndex)
        {
            case 0: // left
                int startPageNum = currentPageNum;
                currentPageNum -= 1;
                if (currentPageNum < 1) currentPageNum = Mathf.CeilToInt(guessableRoles.Count / 12f);
                shiftTimer.StartThenRun(CancelGuessingTimerDelay, ShiftTimer);
                GuesserMessage(Translations.MoreTimeGiven).Send(MyPlayer);
                if (startPageNum != currentPageNum) ResetShapeshfiterObjects();
                SendCnoRows();
                break;
            case 1: // pressing page icon.
                break;
            case 2: // right
                int startPageNumRight = currentPageNum;
                currentPageNum += 1;
                int maxPages  = Mathf.CeilToInt(guessableRoles.Count / 12f);
                if (currentPageNum > maxPages) currentPageNum = 1;
                shiftTimer.StartThenRun(CancelGuessingTimerDelay, ShiftTimer);
                GuesserMessage(Translations.MoreTimeGiven).Send(MyPlayer);
                if (startPageNumRight != currentPageNum) ResetShapeshfiterObjects();
                SendCnoRows();
                break;
            default:
                int playerIndex = shiftableObject.PlayerIndex;
                if (playerIndex > 14) return; // crowded detection.
                int listIndex = playerIndex - 3;
                listIndex = (currentPageNum - 1) * 12 + listIndex;
                if (listIndex >= guessableRoles.Count) return;
                shiftTimer.Finish(true);
                DeleteAllShapeshifterObjects();
                guessedRole = guessableRoles[listIndex];
                isShapeshifterRole = false;
                currentlyGuessing = false;
                MyPlayer.RpcSetRoleDesync(RoleTypes.Crewmate, MyPlayer);
                GuesserMessage(Translations.PickedRoleText.Formatted(Players.FindPlayerById(guessingPlayer)?.name ?? "???", guessedRole.ColoredRoleName())).Send(MyPlayer);
                break;
        }
    }

    private void SendCnoRows()
    {
        if (MyPlayer.AmOwner) return;
        StringBuilder stringBuilder = new();
        stringBuilder.Append(Translations.ShifterMenuHelpText);
        stringBuilder.AppendLine();

        int lastRow = -1;
        bool firstRole = true;
        shapeshifterObjects.ForEach(obj =>
        {
            if (!obj.IsCno()) return;
            int curRow = Mathf.CeilToInt((float)(obj.PlayerIndex + 1) / 3f);
            if (curRow != lastRow)
            {
                stringBuilder.AppendLine();
                stringBuilder.Append(Translations.RowText.Formatted(curRow));
                firstRole = true;
            }
            lastRow = curRow;
            if (!firstRole) stringBuilder.Append(", ");
            stringBuilder.Append(curRow == 1 ? obj.GetText().RemoveHtmlTags() : obj.GetText());
            firstRole = false;
        });
        if (lastRow == -1) return;
        GuesserMessage(stringBuilder.ToString()).Send(MyPlayer);
    }

    protected virtual void HandleBadGuess()
    {
        GuesserMessage(Translations.GuessDeathAnnouncement.Formatted(MyPlayer.name)).Send();
        MyPlayer.InteractWith(MyPlayer, new UnblockedInteraction(
            new FatalIntent(true,
                () => new CustomDeathEvent(MyPlayer, MyPlayer, ModConstants.DeathNames.Guessed))
            , this));
    }

    protected virtual void HandleCorrectGuess(PlayerControl guessedPlayer, CustomRole guessedRole)
    {
        GuesserMessage(Translations.GuessDeathAnnouncement.Formatted(guessedPlayer.name)).Send();
        MyPlayer.InteractWith(guessedPlayer, new UnblockedInteraction(
            new FatalIntent(true,
                () => new CustomDeathEvent(guessedPlayer, MyPlayer, ModConstants.DeathNames.Guessed))
            , this));
    }

    protected virtual CancelGuessReason CanGuessPlayer(PlayerControl targetPlayer)
    {
        bool canSeeRole = false;
        RoleComponent? roleComponent = targetPlayer.NameModel().GetComponentHolder<RoleHolder>().LastOrDefault();
        if (roleComponent != null) canSeeRole = roleComponent.Viewers().Any(p => p == MyPlayer);
        return canSeeRole ? CancelGuessReason.CanSeeRole : CancelGuessReason.None;
    }
    protected virtual bool CanGuessRole(CustomRole role) => true;

    protected override GameOptionBuilder RegisterOptions(GameOptionBuilder optionStream) =>
        base.RegisterOptions(optionStream)
            .SubOption(sub => sub.KeyName("Guesses per Meeting", Guesser.Translations.Options.GuesserPerMeeting)
                .AddIntRange(1, 10, 1, 0)
                .BindInt(i => guessesPerMeeting = i)
                .Build())
            .SubOption(sub => sub.KeyName("Follow Guesser Settings", Guesser.Translations.Options.FollowGuesserSettings)
                .AddBoolean()
                .BindBool(b => followGuesserSettings = b)
                .Build());

    protected override RoleModifier Modify(RoleModifier roleModifier) => roleModifier
        .VanillaRole(RoleTypes.Crewmate);

    protected ChatHandler GuesserMessage(string message) => ChatHandler.Of(message, RoleColor.Colorize(Translations.GuesserTitle)).LeftAlign();

    private class GuesserShapeshifterObject
    {
        public GuesserShiftableObject? NetObject;
        public PlayerControl? RealPlayer;
        public int PlayerIndex;

        private PlayerControl guesser;
        private string currentName;
        private bool isCno;

        private Remote<NameComponent>? overridenName;
        private Remote<IndicatorComponent>? overridenIndicator;
        private Remote<RoleComponent>? overridenRole;
        private Remote<CounterComponent>? overridenCounter;
        private Remote<TextComponent>? overridenText;

        public GuesserShapeshifterObject(PlayerControl guesser, int index, string currentName, PlayerControl? vanillaPlayer)
        {
            PlayerIndex = index;
            this.guesser  = guesser;
            this.currentName = currentName;
            if (vanillaPlayer == null)
            {
                isCno = true;
                NetObject = new GuesserShiftableObject(currentName, new Vector2(100000, 10000), guesser.PlayerId);
                return;
            }
            RealPlayer = vanillaPlayer;

            var nameModel = vanillaPlayer.NameModel();
            overridenName = nameModel.GetComponentHolder<NameHolder>().Add(new NameComponent(new LiveString(() => currentName), GameState.InMeeting, ViewMode.Absolute, viewers:guesser));
            overridenIndicator = nameModel.GetComponentHolder<IndicatorHolder>().Add(new IndicatorComponent(new LiveString(string.Empty), GameState.InMeeting, ViewMode.Absolute, viewers: guesser));
            overridenRole = nameModel.GetComponentHolder<RoleHolder>().Add(new RoleComponent(new LiveString(string.Empty), [GameState.InMeeting], ViewMode.Absolute, viewers:guesser));
            overridenCounter = nameModel.GetComponentHolder<CounterHolder>().Add(new CounterComponent(new LiveString(string.Empty), [GameState.InMeeting], ViewMode.Absolute, viewers: guesser));
            overridenText = nameModel.GetComponentHolder<TextHolder>().Add(new TextComponent(new LiveString(string.Empty), GameState.InMeeting, ViewMode.Absolute, viewers: guesser));

            if (guesser.AmOwner) nameModel.RenderFor(guesser);
            // send at a DELAY so that guesser message doesn't clear the name.
            else Async.Schedule(() => nameModel.RenderFor(guesser, force: true), NetUtils.DeriveDelay(1f));

        }

        public void ChangeName(string newName)
        {
            currentName = newName;
            NetObject?.RpcChangeSprite(newName);
            RealPlayer?.NameModel().RenderFor(guesser);
        }

        public bool IsCno() => isCno;
        public string GetText() => currentName;

        public void Delete()
        {
            NetObject?.Despawn();
            overridenName?.Delete();
            overridenIndicator?.Delete();
            overridenCounter?.Delete();
            overridenText?.Delete();
            overridenRole?.Delete();
            if (RealPlayer != null) RealPlayer.SetChatName(RealPlayer.name);
        }
    }

    private class GuesserShiftableObject : ShiftableNetObject
    {
        public GuesserShiftableObject(string objectName, Vector2 position, byte visibleTo = byte.MaxValue) : base(
            objectName, position, visibleTo)
        {

        }

        public override void SetupOutfit()
        {
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PlayerName = Sprite;
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].ColorId = 0;
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].HatId = "";
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].SkinId = "";
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].PetId = "";
            PlayerControl.LocalPlayer.Data.Outfits[PlayerOutfitType.Default].VisorId = "";
        }
    }
}