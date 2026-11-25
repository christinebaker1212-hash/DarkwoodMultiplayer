using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEngine.Video;

namespace DarkwoodCoopOnline.Sync;

public static class PrologueCutsceneHoldSync
{
	private static bool _patched;

	private static CutsceneManager _pendingCutsceneInstance;

	private static DreamTransition _pendingTransitionInstance;

	private static WorldGenerator _pendingIntroVideoInstance;

	private static VideoPlayer _pendingIntroVideoPlayer;

	private static MethodInfo _displayIntroVideoMethod;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "startScene", (Type[])null, (Type[])null), new HarmonyMethod(typeof(PrologueCutsceneHoldSync), "BeforeStartScene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(DreamTransition), "transition", (Type[])null, (Type[])null), new HarmonyMethod(typeof(PrologueCutsceneHoldSync), "BeforeDreamTransition", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "ProcessAnims", (Type[])null, (Type[])null), new HarmonyMethod(typeof(PrologueCutsceneHoldSync), "BeforeProcessAnims", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			_displayIntroVideoMethod = AccessTools.Method(typeof(WorldGenerator), "DisplayIntroVideo", new Type[1] { typeof(VideoPlayer) }, (Type[])null);
			val.Patch((MethodBase)_displayIntroVideoMethod, new HarmonyMethod(typeof(PrologueCutsceneHoldSync), "BeforeDisplayIntroVideo", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool ShouldHold()
	{
		if (!_applyingRemote)
		{
			return LobbySync.IsActive;
		}
		return false;
	}

	private static bool BeforeStartScene(CutsceneManager __instance)
	{
		if (!ShouldHold())
		{
			return true;
		}
		_pendingCutsceneInstance = __instance;
		Plugin.Log.LogInfo("[PrologueCutsceneHoldSync] cutscene do prólogo (CutsceneManager) tentou começar durante o lobby - segurando até o Start.");
		return false;
	}

	private static bool BeforeDreamTransition(DreamTransition __instance)
	{
		if (!ShouldHold())
		{
			return true;
		}
		_pendingTransitionInstance = __instance;
		Plugin.Log.LogInfo("[PrologueCutsceneHoldSync] transição de sonho (DreamTransition, ex.: vídeo do fim do prólogo) tentou começar durante o lobby - segurando até o Start.");
		return false;
	}

	private static bool BeforeDisplayIntroVideo(WorldGenerator __instance, VideoPlayer player)
	{
		Plugin.Log.LogInfo($"[PrologueCutsceneHoldSync][diag] DisplayIntroVideo chamado - SessionActive={CoopRuntime.SessionActive} LobbySync.IsActive={LobbySync.IsActive} ShouldHold={ShouldHold()}");
		if (!ShouldHold())
		{
			return true;
		}
		_pendingIntroVideoInstance = __instance;
		_pendingIntroVideoPlayer = player;
		Plugin.Log.LogInfo("[PrologueCutsceneHoldSync] vídeo de abertura do prólogo (WorldGenerator.DisplayIntroVideo) tentou começar durante o lobby - segurando até o Start.");
		return false;
	}

	private static bool BeforeProcessAnims()
	{
		return !ShouldHold();
	}

	public static void Tick()
	{
		if (!LobbySync.IsActive)
		{
			if ((Object)(object)_pendingCutsceneInstance != (Object)null)
			{
				CutsceneManager pendingCutsceneInstance = _pendingCutsceneInstance;
				_pendingCutsceneInstance = null;
				Plugin.Log.LogInfo("[PrologueCutsceneHoldSync] lobby liberado - retomando a cutscene do prólogo (CutsceneManager) agora.");
				_applyingRemote = true;
				pendingCutsceneInstance.startScene();
				_applyingRemote = false;
			}
			if ((Object)(object)_pendingTransitionInstance != (Object)null)
			{
				DreamTransition pendingTransitionInstance = _pendingTransitionInstance;
				_pendingTransitionInstance = null;
				Plugin.Log.LogInfo("[PrologueCutsceneHoldSync] lobby liberado - retomando a transição de sonho (DreamTransition) agora.");
				_applyingRemote = true;
				pendingTransitionInstance.transition();
				_applyingRemote = false;
			}
			if ((Object)(object)_pendingIntroVideoInstance != (Object)null)
			{
				WorldGenerator pendingIntroVideoInstance = _pendingIntroVideoInstance;
				VideoPlayer pendingIntroVideoPlayer = _pendingIntroVideoPlayer;
				_pendingIntroVideoInstance = null;
				_pendingIntroVideoPlayer = null;
				Plugin.Log.LogInfo("[PrologueCutsceneHoldSync] lobby liberado - retomando o vídeo de abertura do prólogo agora.");
				_applyingRemote = true;
				_displayIntroVideoMethod.Invoke(pendingIntroVideoInstance, new object[1] { pendingIntroVideoPlayer });
				_applyingRemote = false;
			}
		}
	}

	public static void Reset()
	{
		_pendingCutsceneInstance = null;
		_pendingTransitionInstance = null;
		_pendingIntroVideoInstance = null;
		_pendingIntroVideoPlayer = null;
	}
}
