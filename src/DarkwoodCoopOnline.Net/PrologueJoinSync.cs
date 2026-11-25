using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DarkwoodCoopOnline.Sync;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Net;

public static class PrologueJoinSync
{
	private const float StatusTimeoutSeconds = 8f;

	private static bool _forceFirstPlayNextLoad;

	private static bool _patched;

	private static volatile bool _hasPendingLanStatus;

	private static volatile string _pendingLanStatusResult;

	private static bool _waitingLanStatus;

	private static ulong _pendingSteamFriendId64;

	private static ulong _pendingSteamHostId;

	private static string _pendingSteamUrl;

	private static bool _waitingSteamStatus;

	private static string _pendingHostIp;

	private static int _pendingPort;

	private static float _statusTimer;

	private static bool _pendingIsSteam;

	private static ulong _pendingSteamConnectId;

	private static bool _pendingConnect;

	private static float _pendingConnectTimer;

	public static bool JoinInProgress => _pendingConnect;

	public static void EnsurePatched()
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony("com.felipe.darkwoodcooponline").Patch((MethodBase)AccessTools.Method(typeof(WorldGenerator), "tweenLoading", (Type[])null, (Type[])null), new HarmonyMethod(typeof(PrologueJoinSync), "BeforeTweenLoading", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void BeforeTweenLoading()
	{
		if (_forceFirstPlayNextLoad)
		{
			_forceFirstPlayNextLoad = false;
			if ((Object)(object)Player.Instance != (Object)null)
			{
				Player.Instance.firstPlay = true;
				Plugin.Log.LogInfo("[PrologueJoinSync] firstPlay forçado pra true antes do tweenLoading (jogo novo forçado por este mod).");
			}
		}
	}

	// JOIN while the host is in the prologue: pick a slot here and start a new game in step with them
	public static void ChooseSlotForNewGame()
	{
		SaveSlotPickerUI.Show(WorldSync.GetSlotSummaries(), delegate(int slotId)
		{
			OnSlotChosen(slotId, isSteam: false, null, 0, 0uL);
		});
	}

	private static void OnSlotChosen(int slotId, bool isSteam, string hostIp, int port, ulong steamHostId)
	{
		Plugin.Log.LogInfo($"[PrologueJoinSync] jogador escolheu o slot {slotId} - forçando jogo novo (prólogo, sem pular).");
		SaveManager instance = Singleton<SaveManager>.Instance;
		UI instance2 = Singleton<UI>.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			LanSaveTransfer.ReportConfigError("JOIN FAILED: SaveManager/UI not available - the menu wasn't ready yet. Try again.");
			Plugin.Log.LogError("[PrologueJoinSync] SaveManager/UI indisponível - abortando.");
			return;
		}
		instance.deleteCurrentProfile(true);
		GameProfile value = (Core.currentProfile = new GameProfile(slotId, false, 0));
		if (Core.profiles != null && slotId >= 1 && slotId <= Core.profiles.Count)
		{
			Core.profiles[slotId - 1] = value;
		}
		Singleton<Controller>.Instance.skipTutorial = false;
		instance.updateFilePaths();
		_forceFirstPlayNextLoad = true;
		_pendingIsSteam = isSteam;
		_pendingHostIp = hostIp;
		_pendingPort = port;
		_pendingSteamConnectId = steamHostId;
		_pendingConnect = true;
		_pendingConnectTimer = 0f;
		LanSaveTransfer.SetChecking("Starting a new game in the prologue...");
		((MonoBehaviour)instance2).StartCoroutine(instance2.initNewGame());
		Plugin.Log.LogInfo($"[PrologueJoinSync] initNewGame() disparado (jogo novo, perfil #{slotId}) - esperando o mundo/intro carregar pra conectar a sessão.");
	}

	private static void TickPendingConnect()
	{
		if (!_pendingConnect)
		{
			return;
		}
		_pendingConnectTimer += Time.unscaledDeltaTime;
		if ((Object)(object)Player.Instance != (Object)null && Core.worldGenFinished())
		{
			_pendingConnect = false;
			Plugin.Log.LogInfo("[PrologueJoinSync] mundo novo carregado - conectando a sessão automaticamente.");
			if (JoinFlow.OnWorldLoaded())
			{
				LanSaveTransfer.SetDoneExternal();
				return;
			}
			LanSaveTransfer.ReportConfigError("JOIN FAILED: the world loaded, but the network session could not start. Try F8 manually.");
			Plugin.Log.LogError("[PrologueJoinSync] mundo carregou mas conectar a sessão falhou - ver logs de [Plugin]/BuildTransport acima pra causa exata.");
		}
		else if (_pendingConnectTimer > 120f)
		{
			_pendingConnect = false;
			LanSaveTransfer.ReportConfigError("JOIN FAILED: the world did not finish loading in 2 minutes.");
			Plugin.Log.LogError("[PrologueJoinSync] mundo não carregou em 2min - conecte manualmente (F8) se o jogo novo já estiver certo.");
		}
	}

	public static void Tick()
	{
		TickPendingConnect();
	}

	public static void Reset()
	{
		_hasPendingLanStatus = false;
		_pendingLanStatusResult = null;
		_waitingLanStatus = false;
		_waitingSteamStatus = false;
		_statusTimer = 0f;
		_pendingConnect = false;
		_pendingConnectTimer = 0f;
	}
}
