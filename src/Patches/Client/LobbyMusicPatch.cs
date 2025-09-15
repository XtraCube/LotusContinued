using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using LibCpp2IL;
using Lotus.API.Odyssey;
using Lotus.Logging;
using Lotus.Options;
using UnityEngine;
using VentLib.Utilities;
using VentLib.Utilities.Extensions;

namespace Lotus.Patches.Client;

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
class LobbyMusicPatch
{
    public static Dictionary<Options.Client.SoundOptions.SoundTypes, Action<LobbyBehaviour>> MusicDictionary = new()
    {
        // {Options.Client.SoundOptions.SoundTypes.MyTheme, (LobbyBehaviour menu) => menu.MapTheme = myTheme},
    };
    public static Dictionary<Options.Client.SoundOptions.SoundTypes, Action<LobbyBehaviour>> PostfixMusicDictionary = new() {
        {Options.Client.SoundOptions.SoundTypes.Off, (LobbyBehaviour _) => Async.Schedule(() => SoundManager.Instance.StopNamedSound("MapTheme"), 2f)},
    };
    public static void Prefix(LobbyBehaviour __instance)
    {
        if (Game.State is GameState.Roaming or GameState.InMeeting) return; // fixes this patch running when a CNO spawns.
        MusicDictionary.GetOrCompute(ClientOptions.SoundOptions.CurrentSoundType, () => _ => { })(__instance);
    }
    public static void Postfix(LobbyBehaviour __instance)
    {
        if (Game.State is GameState.Roaming or GameState.InMeeting) return; // fixes this patch running when a CNO spawns.
        DevLogger.Log(ClientOptions.SoundOptions.CurrentSoundType.ToString());
        PostfixMusicDictionary.GetOrCompute(ClientOptions.SoundOptions.CurrentSoundType, () => _ => { })(__instance);
    }
}