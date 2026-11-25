using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DarkwoodCoopOnline.Sync;
using HarmonyLib;
using Steamworks;
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

	public static void EnsurePatched()
	{
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Expected O, but got Unknown
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

	public static void BeginLanJoin(string hostIp, int port)
	{
		string text = $"http://{hostIp}:{port}/status";
		Plugin.Log.LogInfo("[PrologueJoinSync] consultando " + text + " antes de decidir o fluxo de JOIN...");
		LanSaveTransfer.SetChecking("LAN: checking host status (" + hostIp + ")...");
		SaveRelay.DownloadAsync(text, delegate(byte[] bytes)
		{
			_pendingLanStatusResult = ((bytes != null) ? Encoding.UTF8.GetString(bytes) : null);
			_hasPendingLanStatus = true;
		});
		_pendingHostIp = hostIp;
		_pendingPort = port;
		_statusTimer = 0f;
		_waitingLanStatus = true;
	}

	private static void TickLanStatus()
	{
		if (!_waitingLanStatus)
		{
			return;
		}
		if (!_hasPendingLanStatus)
		{
			_statusTimer += Time.unscaledDeltaTime;
			if (_statusTimer > 8f)
			{
				_waitingLanStatus = false;
				Plugin.Log.LogInfo("[PrologueJoinSync] /status não respondeu a tempo - seguindo com o fluxo normal (download do mundo).");
				LanSaveTransfer.BeginClientFetch(_pendingHostIp, _pendingPort);
			}
			return;
		}
		_waitingLanStatus = false;
		string pendingLanStatusResult = _pendingLanStatusResult;
		_hasPendingLanStatus = false;
		_pendingLanStatusResult = null;
		if (pendingLanStatusResult == "prologue")
		{
			Plugin.Log.LogInfo("[PrologueJoinSync] host (LAN) está no prólogo - pedindo pro jogador escolher onde salvar LOCALMENTE (sem baixar mundo).");
			string hostIp = _pendingHostIp;
			int port = _pendingPort;
			SaveSlotPickerUI.Show(WorldSync.GetSlotSummaries(), delegate(int slotId)
			{
				OnSlotChosen(slotId, isSteam: false, hostIp, port, 0uL);
			});
		}
		else
		{
			Plugin.Log.LogInfo("[PrologueJoinSync] host (LAN) não está no prólogo (status='" + pendingLanStatusResult + "') - seguindo com o fluxo normal (download do mundo).");
			LanSaveTransfer.BeginClientFetch(_pendingHostIp, _pendingPort);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool TryJoinFriendSteam(CSteamID friendId)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		if (!AutoJoin.ParseConnect(SteamFriends.GetFriendRichPresence(friendId, "connect"), out var hostId, out var url))
		{
			Plugin.Log.LogWarning($"[PrologueJoinSync] tentei entrar no amigo {friendId} mas ele não tem Rich Presence 'connect' publicada (não está hospedando agora?).");
			return false;
		}
		SaveFetchServer.EnsureStarted();
		if (SteamManager.Initialized)
		{
			SteamNetworking.AllowP2PPacketRelay(true);
		}
		_pendingSteamFriendId64 = friendId.m_SteamID;
		_pendingSteamHostId = hostId;
		_pendingSteamUrl = url;
		_statusTimer = 0f;
		_waitingSteamStatus = true;
		SaveFetchServer.SendStatusRequest(friendId);
		Plugin.Log.LogInfo($"[PrologueJoinSync] pergunta de status (P2P) enviada pro host {hostId} antes de decidir o fluxo de JOIN...");
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void TickSteamStatus()
	{
		//IL_00a7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		if (!_waitingSteamStatus)
		{
			return;
		}
		if (SaveFetchServer.TryGetStatusResponse(out var isPrologue))
		{
			_waitingSteamStatus = false;
			if (isPrologue)
			{
				Plugin.Log.LogInfo("[PrologueJoinSync] host (Steam) está no prólogo - pedindo pro jogador escolher onde salvar LOCALMENTE (sem baixar mundo).");
				ulong hostId = _pendingSteamHostId;
				SaveSlotPickerUI.Show(WorldSync.GetSlotSummaries(), delegate(int slotId)
				{
					OnSlotChosen(slotId, isSteam: true, null, 0, hostId);
				});
			}
			else
			{
				Plugin.Log.LogInfo("[PrologueJoinSync] host (Steam) não está no prólogo - seguindo com o fluxo normal (download do mundo).");
				AutoJoin.TryJoinFriend(new CSteamID(_pendingSteamFriendId64));
			}
		}
		else
		{
			_statusTimer += Time.unscaledDeltaTime;
			if (_statusTimer > 8f)
			{
				_waitingSteamStatus = false;
				Plugin.Log.LogInfo("[PrologueJoinSync] pergunta de status (P2P) não respondeu a tempo - seguindo com o fluxo normal (download do mundo).");
				AutoJoin.TryJoinFriend(new CSteamID(_pendingSteamFriendId64));
			}
		}
	}

	private static void OnSlotChosen(int slotId, bool isSteam, string hostIp, int port, ulong steamHostId)
	{
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Expected O, but got Unknown
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
			if ((Object)(object)Plugin.Instance != (Object)null && (_pendingIsSteam ? Plugin.Instance.StartClientSessionWith(_pendingSteamConnectId) : Plugin.Instance.StartClientSessionLan()))
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
		TickLanStatus();
		if (_waitingSteamStatus)
		{
			TickSteamStatus();
		}
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
