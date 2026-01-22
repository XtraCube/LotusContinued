using System.Collections;
using System.Linq;
using AmongUs.GameOptions;
using HarmonyLib;
using Lotus.API.Odyssey;
using Lotus.API.Player;
using Lotus.API.Reactive;
using Lotus.API.Reactive.HookEvents;
using Lotus.Extensions;
using Lotus.GUI.Name.Interfaces;
using Lotus.Roles;
using Lotus.Roles.Interfaces;
using Lotus.Roles.Internals;
using Lotus.Roles.Internals.Enums;
using Lotus.Roles.Operations;
using Lotus.RPC;
using Lotus.Server;
using Lotus.Options;
using UnityEngine;
using VentLib.Utilities;
using VentLib.Utilities.Debug.Profiling;
using VentLib.Utilities.Extensions;
using static VentLib.Utilities.Debug.Profiling.Profilers;
using VentLib.Networking.RPC;
using Lotus.GameModes.Standard;
using System.Collections.Generic;
using System;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Lotus.GameModes;

namespace Lotus.Patches.Intro;


[HarmonyPatch]
class IntroDestroyPatch
{
    private static readonly StandardLogger log = LoggerFactory.GetLogger<StandardLogger>(typeof(IntroDestroyPatch));

    private static MethodBase? GetStateMachineMoveNext<T>(string methodName)
    {
        var typeName = typeof(T).FullName;
        var showRoleStateMachine =
            typeof(T)
                .GetNestedTypes()
                .FirstOrDefault(x=>x.Name.Contains(methodName));

        if (showRoleStateMachine == null)
        {
            log.High($"Failed to find {methodName} state machine for {typeName}");
            return null;
        }

        var moveNext = AccessTools.Method(showRoleStateMachine, "MoveNext");
        if (moveNext == null)
        {
            log.High($"Failed to find MoveNext method for {typeName}.{methodName}");
            return null;
        }

        log.Info($"Found {methodName}.MoveNext");
        return moveNext;
    }

    private static MethodBase TargetMethod()
    {
        var onDestroy = AccessTools.Method(typeof(IntroCutscene), nameof(IntroCutscene.OnDestroy));
        if (onDestroy != null) return onDestroy;

        return GetStateMachineMoveNext<IntroCutscene>("CoBegin")!;
    }

    public static void Postfix(Il2CppObjectBase __instance)
    {
        IntroCutscene? introCutscene;
        if (!__instance.TryCast<IntroCutscene>(out introCutscene))
        {
            var state = AccessTools.Property(__instance.GetType(), "__1__state").GetValue(__instance);
            if (state is -1)
            {
                var introCutsceneField = AccessTools.Property(__instance.GetType(), "__4__this");
                if (introCutsceneField != null)
                {
                    introCutscene = introCutsceneField.GetValue(__instance) as IntroCutscene;
                }
            }
        }

        if (introCutscene != null)
        {
            ActualPostfix(introCutscene);
        }
        else
        {
            log.Warn("Failed to cast IntroCutscene in IntroDestroyPatch Postfix", "IntroCutscene");
        }
    }

    public static void ActualPostfix(IntroCutscene __instance)
    {
        Profiler.Sample destroySample = Global.Sampler.Sampled();
        if (!AmongUsClient.Instance.AmHost)
        {
            Game.State = GameState.Roaming;
            return;
        }

        string pet = GeneralOptions.MiscellaneousOptions.AssignedPet;
        while (pet == "Random") pet = ModConstants.Pets.Values.ToList().GetRandom();
        log.Trace("Intro Scene Ending", "IntroCutscene");

        Profiler.Sample fullSample = Global.Sampler.Sampled("Setup ALL Players");
        IEnumerable<PlayerControl> players = Players.GetPlayers();
        players.ForEach((p, i) =>
        {
            Profiler.Sample executeSample = Global.Sampler.Sampled("Execution Pregame Setup");
            Async.Execute(PreGameSetup(p, pet));
            p.RpcResetAbilityCooldown();
            executeSample.Stop();
        });
        // Async.Schedule(() => Players.GetPlayers().ForEach(p => Async.Execute(ReverseEngineeredRPC.UnshiftButtonTrigger(p))), NetUtils.DeriveDelay(2f));
        fullSample.Stop();
        Game.State = GameState.Roaming;
        Game.MatchData.StartTime = DateTime.Now;

        Profiler.Sample propSample = Global.Sampler.Sampled("Propagation Sample");
        RoleOperations.Current.TriggerForAll(LotusActionType.RoundStart, null, true);
        propSample.Stop();

        Hooks.GameStateHooks.RoundStartHook.Propagate(new GameStateHookEvent(Game.MatchData, ProjectLotus.GameModeManager.CurrentGameMode));
        destroySample.Stop();
    }

    public static IEnumerator PreGameSetup(PlayerControl player, string pet)
    {
        if (player == null) yield break;

        Game.MatchData.RegenerateFrozenPlayers(player);

        if (player.GetVanillaRole().IsImpostor() && Game.CurrentGameMode is StandardGameMode)
        {
            float cooldown = GeneralOptions.GameplayOptions.GetFirstKillCooldown(player);
            log.Trace($"Fixing First Kill Cooldown for {player.name} (Cooldown={cooldown}s)", "Fix First Kill Cooldown");
            player.SetKillCooldown(cooldown);
        }

        if (GeneralOptions.MayhemOptions.UseRandomSpawn) Game.RandomSpawn.Spawn(player);

        // if (!ProjectLotus.AdvancedRoleAssignment) player.RpcSetRoleDesync(RoleTypes.Shapeshifter, -3);
        yield return new WaitForSeconds(0.15f);
        if (player == null) yield break;

        NetworkedPlayerInfo playerData = player.Data;
        if (playerData == null) yield break;

        CustomRole role = player.PrimaryRole();
        if (role is not ITaskHolderRole taskHolder || !taskHolder.TasksApplyToTotal())
        {
            log.Trace($"Clearing Tasks For: {player.name}", "SyncTasks");
            playerData.Tasks?.Clear();
        }

        bool hasPet = player.cosmetics?.CurrentPet?.Data?.ProductId != "pet_EmptyPet";
        if (hasPet) log.Trace($"Player: {player.name} has pet: {player.cosmetics?.CurrentPet?.Data?.ProductId}. Skipping assigning pet: {pet}.", "PetAssignment");
        else if (player.AmOwner) player.SetPet(pet);
        else playerData.DefaultOutfit.PetId = pet;

        playerData.PlayerName = player.name;

        Players.SendPlayerData(playerData, autoSetName: false);
        yield return new WaitForSeconds(NetUtils.DeriveDelay(0.05f));
        if (player == null) yield break;

        if (!hasPet) player.CRpcShapeshift(player, false);

        INameModel nameModel = player.NameModel();
        if (role.desyncedIntroText != null)
        {
            role.desyncedIntroText.Delete();
            role.desyncedIntroText = null;
        }

        Players.GetPlayers().ForEach(p => nameModel.RenderFor(p, GameState.Roaming, force: true));
        player.SyncAll();
        if (Game.CurrentGameMode.GameFlags().HasFlag(GameModeFlags.AllowChatDuringGame)) ReverseEngineeredRPC.EnableChatForPlayer(player);
    }
}