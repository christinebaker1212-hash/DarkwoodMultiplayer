using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DarkwoodCoopOnline.Net;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class AutoJoin
{
	private enum State
	{
		Idle,
		Fetching,
		Loading,
		ChoosingSlot,
		WaitingSpawn,
		Done,
		Failed
	}

	private static readonly IntPtr[] _recvBuf = new IntPtr[64];

	private const string ConnectPrefix = "coopjoin_";

	private const int ChunkTimeoutSeconds = 180;

	private static State _state = State.Idle;

	private static ulong _hostId;

	private static string _saveUrl;

	private static object _joinCb;

	private static bool _usingHttp;

	private const float P2PBeforeHttpFallbackSeconds = 60f;

	private static bool _httpReachabilityCheckStarted;

	private static volatile bool _httpReachabilityCheckDone;

	private static volatile bool _httpReachable;

	private static bool _httpFallbackDeclined;

	private static byte[][] _chunks;

	private static int _received;

	private static int _totalExpected;

	private static float _stateTimer;

	private static float _reqTimer;

	private static volatile bool _httpDownloadStarted;

	private static volatile bool _httpDownloadDone;

	private static volatile byte[] _httpDownloadResult;

	private static volatile bool _hostUploadInProgress;

	private static volatile bool _hostUploadReady;

	private static volatile string _hostUploadUrl;

	private static ulong _hostUploadMyId;

	private static bool _hostInviteReady;

	private const float DoneMessageSeconds = 4f;

	private static float _doneTimer;

	private static byte[] _pendingBlob;

	private static string _pendingHostIdentity;

	public static string StatusText { get; private set; } = "no invite yet";


	public static bool IsActive
	{
		get
		{
			if (_state == State.Idle && !_hostUploadInProgress)
			{
				return _hostInviteReady;
			}
			return true;
		}
	}

	public static bool IsFailed => _state == State.Failed;

	public static void SetHostRichPresence(string transportMode)
	{
		if (!SteamAvailability.Available)
		{
			if (transportMode.Equals("Steam", StringComparison.OrdinalIgnoreCase))
			{
				Plugin.Log.LogWarning("[AutoJoin] HOST em modo Steam, mas esta build do jogo não tem Steam (" + SteamAvailability.UnavailableReason + ") - convite não pode ser publicado; use LAN.");
			}
		}
		else
		{
			SetHostRichPresenceSteam(transportMode);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void SetHostRichPresenceSteam(string transportMode)
	{
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		if (!transportMode.Equals("Steam", StringComparison.OrdinalIgnoreCase))
		{
			Plugin.Log.LogWarning("[AutoJoin] HOST em modo '" + transportMode + "' (não Steam) - convite Steam NÃO será publicado de propósito (loopback é só pra teste na mesma máquina).");
		}
		else if (!SteamManager.Initialized)
		{
			Plugin.Log.LogWarning("[AutoJoin] HOST pedido mas Steam não está inicializada - convite não pode ser publicado (Steam client está aberto e logado?).");
		}
		else if (!_hostUploadInProgress)
		{
			_hostUploadInProgress = true;
			_hostUploadReady = false;
			_hostUploadUrl = null;
			_hostInviteReady = false;
			_hostUploadMyId = SteamUser.GetSteamID().m_SteamID;
			Plugin.Log.LogInfo("[AutoJoin] subindo o save pro relay HTTP antes de publicar o convite...");
			StatusText = "publishing invite - uploading save (wait before telling your friend)... 0%";
			SaveRelay.UploadAsync(WorldSync.SerializeWorld(), delegate(string url)
			{
				_hostUploadUrl = url;
				_hostUploadReady = true;
			}, delegate(float progress)
			{
				StatusText = $"publishing invite - uploading save... {progress:P0}";
			});
		}
	}

	public static void ClearHostRichPresence()
	{
		_hostUploadInProgress = false;
		_hostInviteReady = false;
		if (SteamAvailability.Available)
		{
			ClearHostRichPresenceSteam();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ClearHostRichPresenceSteam()
	{
		if (SteamManager.Initialized)
		{
			SteamFriends.SetRichPresence("connect", (string)null);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void TickHostUpload()
	{
		if (_hostUploadInProgress && _hostUploadReady)
		{
			_hostUploadInProgress = false;
			if (string.IsNullOrEmpty(_hostUploadUrl))
			{
				Plugin.Log.LogWarning("[AutoJoin] upload do save pro relay HTTP falhou - convite Steam vai cair no fallback por P2P (mais lento).");
				SteamFriends.SetRichPresence("connect", "coopjoin_" + _hostUploadMyId);
				StatusText = "invite published (upload failed, falling back to slower P2P) - your friend can try joining now.";
				_hostInviteReady = true;
			}
			else
			{
				SteamFriends.SetRichPresence("connect", "coopjoin_" + _hostUploadMyId + "|" + _hostUploadUrl);
				Plugin.Log.LogInfo($"[AutoJoin] Rich Presence 'connect' publicada com link HTTP do save (host {_hostUploadMyId}) - amigos podem entrar.");
				StatusText = "invite published - your friend can join now.";
				_hostInviteReady = true;
			}
		}
	}

	public static void CheckStartup()
	{
		if (SteamAvailability.Available)
		{
			CheckStartupSteam();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void CheckStartupSteam()
	{
		string[] commandLineArgs = Environment.GetCommandLineArgs();
		for (int i = 0; i < commandLineArgs.Length; i++)
		{
			if (ParseConnect(commandLineArgs[i], out var hostId, out var url))
			{
				Begin(hostId, url);
				break;
			}
		}
		if (SteamManager.Initialized)
		{
			_joinCb = Callback<GameRichPresenceJoinRequested_t>.Create(OnJoinRequested);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void OnJoinRequested(GameRichPresenceJoinRequested_t cb)
	{
		if (ParseConnect(cb.m_rgchConnect, out var hostId, out var url))
		{
			Begin(hostId, url);
			return;
		}
		StatusText = "FAILED: invite received but connect string not recognized ('" + cb.m_rgchConnect + "').";
		Plugin.Log.LogWarning("[AutoJoin] invite received but connect string not recognized: '" + cb.m_rgchConnect + "'.");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static bool TryJoinFriend(CSteamID friendId)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		if (ParseConnect(SteamFriends.GetFriendRichPresence(friendId, "connect"), out var hostId, out var url))
		{
			Begin(hostId, url);
			return true;
		}
		Plugin.Log.LogWarning($"[AutoJoin] tentei entrar no amigo {friendId} mas ele não tem Rich Presence 'connect' publicada (não está hospedando agora?).");
		return false;
	}

	internal static bool ParseConnect(string s, out ulong hostId, out string url)
	{
		hostId = 0uL;
		url = null;
		if (string.IsNullOrEmpty(s))
		{
			return false;
		}
		int num = s.IndexOf("coopjoin_", StringComparison.OrdinalIgnoreCase);
		if (num < 0)
		{
			return false;
		}
		string text = s.Substring(num + "coopjoin_".Length).Trim();
		int num2 = text.IndexOf('|');
		string text2 = ((num2 >= 0) ? text.Substring(0, num2) : text);
		int i;
		for (i = 0; i < text2.Length && char.IsDigit(text2[i]); i++)
		{
		}
		if (i == 0 || !ulong.TryParse(text2.Substring(0, i), out hostId))
		{
			return false;
		}
		if (num2 >= 0)
		{
			string text3 = text.Substring(num2 + 1).Trim();
			if (text3.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				url = text3;
			}
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Begin(ulong hostId, string url)
	{
		if (_state == State.Idle)
		{
			_hostId = hostId;
			_saveUrl = url;
			_usingHttp = false;
			_httpReachabilityCheckStarted = false;
			_httpReachabilityCheckDone = false;
			_httpReachable = false;
			_httpFallbackDeclined = false;
			_state = State.Fetching;
			_stateTimer = 0f;
			_reqTimer = 999f;
			_chunks = null;
			_received = 0;
			_totalExpected = -1;
			_httpDownloadStarted = false;
			_httpDownloadDone = false;
			_httpDownloadResult = null;
			StatusText = $"invite received from host {hostId} - requesting save via P2P...";
			Plugin.Log.LogInfo($"[AutoJoin] invite from host {hostId} - trying P2P first" + (string.IsNullOrEmpty(url) ? " (sem link HTTP pra fallback)." : " (link HTTP disponível como fallback se o P2P não progredir)."));
			if (SteamManager.Initialized)
			{
				SteamNetworking.AllowP2PPacketRelay(true);
			}
		}
	}

	public static void Tick()
	{
		if (SteamAvailability.Available)
		{
			TickHostUpload();
			switch (_state)
			{
			case State.Fetching:
				TickFetching();
				break;
			case State.WaitingSpawn:
				TickWaitingSpawn();
				break;
			case State.Done:
				TickDone();
				break;
			case State.Loading:
			case State.ChoosingSlot:
				break;
			}
		}
	}

	private static void TickDone()
	{
		_doneTimer += Time.unscaledDeltaTime;
		if (!(_doneTimer < 4f))
		{
			_doneTimer = 0f;
			_state = State.Idle;
			StatusText = "no invite yet";
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void TickFetching()
	{
		//IL_019b: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0207: Unknown result type (might be due to invalid IL or missing references)
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0219: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Unknown result type (might be due to invalid IL or missing references)
		//IL_022b: Unknown result type (might be due to invalid IL or missing references)
		if ((!_usingHttp && !SteamManager.Initialized) || (Object)(object)Singleton<UI>.Instance == (Object)null || (Object)(object)Singleton<SaveManager>.Instance == (Object)null)
		{
			return;
		}
		_stateTimer += Time.unscaledDeltaTime;
		if (_stateTimer > 180f)
		{
			StatusText = "FAILED: timed out waiting for the host's save (is the host hosting, F7?).";
			Plugin.Log.LogError("[AutoJoin] timed out waiting for the host's save - is the host's game open and hosting (F7)?");
			_state = State.Failed;
			return;
		}
		if (_usingHttp)
		{
			TickHttpFetch();
			return;
		}
		if (_totalExpected < 0 && !_httpFallbackDeclined && _stateTimer > 60f && !string.IsNullOrEmpty(_saveUrl))
		{
			if (!_httpReachabilityCheckStarted)
			{
				_httpReachabilityCheckStarted = true;
				Plugin.Log.LogInfo($"[AutoJoin] P2P não progrediu em {60f:F0}s - testando se o HTTP está alcançável antes de trocar...");
				StatusText = $"P2P slow after {60f:F0}s - testing HTTP availability...";
				SaveRelay.IsReachableAsync(_saveUrl, delegate(bool ok)
				{
					_httpReachable = ok;
					_httpReachabilityCheckDone = true;
				});
				return;
			}
			if (!_httpReachabilityCheckDone)
			{
				return;
			}
			if (_httpReachable)
			{
				Plugin.Log.LogInfo("[AutoJoin] HTTP alcançável - trocando pro fallback HTTP agora.");
				_usingHttp = true;
				_stateTimer = 0f;
				return;
			}
			Plugin.Log.LogWarning("[AutoJoin] HTTP não está alcançável agora - mantendo no P2P (sem tentar de novo).");
			_httpFallbackDeclined = true;
		}
		_reqTimer += Time.unscaledDeltaTime;
		if (_totalExpected < 0 && _reqTimer > 2f)
		{
			_reqTimer = 0f;
			byte[] data = new byte[1] { 80 };
			SendMsg(new CSteamID(_hostId), data, 1);
			StatusText = $"requesting save from host {_hostId} (attempt at {_stateTimer:F0}s)...";
			Plugin.Log.LogInfo("[AutoJoin] save request sent to host, waiting...");
		}
		int num;
		do
		{
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, _recvBuf, _recvBuf.Length);
			for (int i = 0; i < num; i++)
			{
				SteamNetworkingMessage_t val = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_recvBuf[i]);
				CSteamID steamID = val.m_identityPeer.GetSteamID();
				byte[] array = new byte[val.m_cbSize];
				if (val.m_cbSize > 0)
				{
					Marshal.Copy(val.m_pData, array, 0, val.m_cbSize);
				}
				SteamNetworkingMessage_t.Release(_recvBuf[i]);
				if (steamID.m_SteamID == _hostId && array.Length != 0 && array[0] == 81)
				{
					PacketReader packetReader = new PacketReader(array);
					int num2 = packetReader.UShort();
					int num3 = packetReader.UShort();
					byte[] array2 = packetReader.Bytes();
					if (_chunks == null)
					{
						_chunks = new byte[num3][];
						_totalExpected = num3;
					}
					if (num2 < num3 && _chunks[num2] == null)
					{
						_chunks[num2] = array2;
						_received++;
					}
				}
			}
		}
		while (num == _recvBuf.Length);
		if (_totalExpected > 0)
		{
			StatusText = $"downloading save: {_received}/{_totalExpected} packets ({_received * 100 / _totalExpected}%)...";
		}
		if (_totalExpected <= 0 || _received < _totalExpected)
		{
			return;
		}
		byte[] blob;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			byte[][] chunks = _chunks;
			foreach (byte[] array3 in chunks)
			{
				memoryStream.Write(array3, 0, array3.Length);
			}
			blob = memoryStream.ToArray();
		}
		ApplyAndLoad(blob);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void SendMsg(CSteamID to, byte[] data, int channel)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity val = default(SteamNetworkingIdentity);
		val.SetSteamID(to);
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try
		{
			int num = 40;
			SteamNetworkingMessages.SendMessageToUser(ref val, gCHandle.AddrOfPinnedObject(), (uint)data.Length, num, channel);
		}
		finally
		{
			gCHandle.Free();
		}
	}

	private static void TickHttpFetch()
	{
		if (!_httpDownloadStarted)
		{
			_httpDownloadStarted = true;
			StatusText = "downloading save via HTTP... 0%";
			SaveRelay.DownloadAsync(_saveUrl, delegate(byte[] data)
			{
				_httpDownloadResult = data;
				_httpDownloadDone = true;
			}, delegate(float progress)
			{
				StatusText = $"downloading save via HTTP... {progress:P0}";
			});
		}
		else if (_httpDownloadDone)
		{
			if (_httpDownloadResult == null || _httpDownloadResult.Length == 0)
			{
				Plugin.Log.LogError("[AutoJoin] fallback HTTP também falhou (depois do P2P já não ter progredido).");
				StatusText = "FAILED: both P2P and HTTP download failed (is the host hosting, F7?).";
				_state = State.Failed;
			}
			else
			{
				ApplyAndLoad(_httpDownloadResult);
			}
		}
	}

	private static void ApplyAndLoad(byte[] blob)
	{
		string text = "steam:" + _hostId;
		int mappedSlotForHost = WorldSync.GetMappedSlotForHost(text);
		string text2 = WorldSync.PeekWorldId(blob);
		string mappedWorldIdForHost = WorldSync.GetMappedWorldIdForHost(text);
		bool flag = text2 == null || mappedWorldIdForHost == null || text2 == mappedWorldIdForHost;
		if (mappedSlotForHost >= 1 && flag)
		{
			WriteAndLoad(blob, text, -1);
			return;
		}
		if (mappedSlotForHost >= 1 && !flag)
		{
			Plugin.Log.LogInfo("[AutoJoin] host '" + text + "' mandou um mundo diferente do que já tínhamos mapeado - pedindo pro jogador escolher o slot de novo.");
		}
		_pendingBlob = blob;
		_pendingHostIdentity = text;
		_state = State.ChoosingSlot;
		StatusText = $"save complete ({blob.Length} bytes) - choose where to save it.";
		Plugin.Log.LogInfo("[AutoJoin] host novo ('" + text + "') — pedindo pro jogador escolher o slot.");
		SaveSlotPickerUI.Show(WorldSync.GetSlotSummaries(), OnSlotChosen);
	}

	private static void OnSlotChosen(int slotId)
	{
		byte[] pendingBlob = _pendingBlob;
		string pendingHostIdentity = _pendingHostIdentity;
		_pendingBlob = null;
		_pendingHostIdentity = null;
		WriteAndLoad(pendingBlob, pendingHostIdentity, slotId);
	}

	private static void WriteAndLoad(byte[] blob, string hostIdentity, int explicitSlot)
	{
		StatusText = $"save complete ({blob.Length} bytes) - writing to the 1_4Save folder...";
		Plugin.Log.LogInfo($"[AutoJoin] save complete ({blob.Length} bytes) - writing and loading.");
		int slot = WorldSync.DeserializeWorld(blob, hostIdentity, explicitSlot);
		if (slot < 0)
		{
			StatusText = "FAILED: error writing the save to the 1_4Save folder - check the log for '[World][M2.2] falha'.";
			_state = State.Failed;
			return;
		}
		SaveManager instance = Singleton<SaveManager>.Instance;
		UI instance2 = Singleton<UI>.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			StatusText = "FAILED: SaveManager/UI not available (was the menu not up yet?).";
			Plugin.Log.LogError("[AutoJoin] SaveManager/UI not available at the menu - can't auto-load.");
			_state = State.Failed;
			return;
		}
		MainMenu.SaveState val = instance.loadGameProfiles();
		if (val != null && val.profiles != null)
		{
			Core.profiles = val.profiles;
		}
		GameProfile val2 = Core.profiles?.Find((GameProfile p) => p != null && p.id == slot);
		if (val2 == null)
		{
			StatusText = $"FAILED: profile #{slot} was written but didn't show up in the list.";
			Plugin.Log.LogError($"[AutoJoin] profile #{slot} did not show up in the list after writing - aborting auto-load.");
			_state = State.Failed;
			return;
		}
		Core.currentProfile = val2;
		instance.updateFilePaths();
		_state = State.WaitingSpawn;
		_stateTimer = 0f;
		StatusText = $"save written to profile #{slot} - loading the world...";
		((MonoBehaviour)instance2).StartCoroutine(instance2.initLoadGame());
		Plugin.Log.LogInfo($"[AutoJoin] initLoadGame() fired for profile #{slot} - waiting for the world to load.");
	}

	private static void TickWaitingSpawn()
	{
		_stateTimer += Time.unscaledDeltaTime;
		if ((Object)(object)Player.Instance != (Object)null && Core.worldGenFinished())
		{
			StatusText = "world loaded - connecting as client...";
			Plugin.Log.LogInfo("[AutoJoin] world loaded - connecting as client automatically.");
			Plugin.Instance?.StartClientSessionWith(_hostId);
			StatusText = "CONNECTED successfully.";
			_state = State.Done;
			_doneTimer = 0f;
		}
		else if (_stateTimer > 120f)
		{
			StatusText = "FAILED: world did not load in 2min - connect manually with F8.";
			Plugin.Log.LogError("[AutoJoin] world did not signal 'loaded' within 2min - giving up on auto-connect (connect manually with F8).");
			_state = State.Failed;
		}
	}

	public static void Reset()
	{
		if (_state == State.Fetching || _state == State.Loading || _state == State.ChoosingSlot)
		{
			_state = State.Idle;
		}
		_pendingBlob = null;
		_pendingHostIdentity = null;
		SaveSlotPickerUI.Reset();
	}
}
