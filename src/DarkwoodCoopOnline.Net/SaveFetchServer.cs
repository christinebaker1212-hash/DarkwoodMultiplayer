using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DarkwoodCoopOnline.Sync;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Net;

public static class SaveFetchServer
{
	public const int RequestChannel = 1;

	public const int ResponseChannel = 2;

	public const int StatusRequestChannel = 3;

	public const int StatusResponseChannel = 4;

	private static readonly IntPtr[] _statusRecvBuf = new IntPtr[8];

	private static volatile bool _statusResponsePending;

	private static volatile bool _statusResponseValue;

	private const int ChunkSize = 60000;

	private static readonly Queue<(ulong to64, byte[] pkt)> _pendingChunks = new Queue<(ulong, byte[])>();

	private const int ChunksPerTick = 4;

	private static readonly Dictionary<ulong, int> _pendingCountByPeer = new Dictionary<ulong, int>();

	private static readonly Dictionary<ulong, float> _lastProgressTime = new Dictionary<ulong, float>();

	private const float StaleTransferSeconds = 20f;

	private static bool _started;

	private static object _sessionRequest;

	private static object _sessionFailed;

	private static readonly IntPtr[] _recvBuf = new IntPtr[64];

	public static string PeerJoinStatus { get; private set; } = "";


	public static void SendStatusRequest(CSteamID to)
	{
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		_statusResponsePending = false;
		byte[] data = PacketWriter.Begin(PacketType.PrologueStatusRequest).Finish();
		SendMsg(to, data, 3);
	}

	public static bool TryGetStatusResponse(out bool isPrologue)
	{
		isPrologue = _statusResponseValue;
		if (!_statusResponsePending)
		{
			return false;
		}
		_statusResponsePending = false;
		return true;
	}

	public static void ClearPeerJoinStatus()
	{
		PeerJoinStatus = "";
	}

	public static void EnsureStarted()
	{
		if (!_started && SteamAvailability.Available)
		{
			EnsureStartedSteam();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void EnsureStartedSteam()
	{
		_started = true;
		_sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
		_sessionFailed = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t req)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity identityRemote = req.m_identityRemote;
		SteamNetworkingMessages.AcceptSessionWithUser(ref identityRemote);
		Plugin.Log.LogInfo($"[SaveFetch] sessão aceita de {identityRemote.GetSteamID().m_SteamID} (pré-jogo).");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t fail)
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		Plugin.Log.LogError($"[SaveFetch] sessão FALHOU (motivo Steam={fail.m_info.m_eEndReason}) - verifique se o " + "outro lado está com o jogo aberto e se a rede/firewall não está bloqueando a Steam.");
	}

	public static void Tick()
	{
		if (_started)
		{
			TickSteam();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void TickSteam()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0153: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_0163: Unknown result type (might be due to invalid IL or missing references)
		//IL_016d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0177: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_0201: Unknown result type (might be due to invalid IL or missing references)
		//IL_0205: Invalid comparison between Unknown and I4
		//IL_0212: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Invalid comparison between Unknown and I4
		//IL_0221: Unknown result type (might be due to invalid IL or missing references)
		if (!SteamManager.Initialized)
		{
			return;
		}
		int num;
		do
		{
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(1, _recvBuf, _recvBuf.Length);
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
				if (array.Length != 0 && array[0] == 80)
				{
					HandleRequest(steamID);
				}
			}
		}
		while (num == _recvBuf.Length);
		do
		{
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(3, _statusRecvBuf, _statusRecvBuf.Length);
			for (int j = 0; j < num; j++)
			{
				SteamNetworkingMessage_t val2 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_statusRecvBuf[j]);
				CSteamID steamID2 = val2.m_identityPeer.GetSteamID();
				SteamNetworkingMessage_t.Release(_statusRecvBuf[j]);
				bool v = (Object)(object)Player.Instance != (Object)null && Player.Instance.firstPlay;
				byte[] data = PacketWriter.Begin(PacketType.PrologueStatusResponse).Bool(v).Finish();
				SendMsg(steamID2, data, 4);
			}
		}
		while (num == _statusRecvBuf.Length);
		do
		{
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(4, _statusRecvBuf, _statusRecvBuf.Length);
			for (int k = 0; k < num; k++)
			{
				SteamNetworkingMessage_t val3 = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_statusRecvBuf[k]);
				byte[] array2 = new byte[val3.m_cbSize];
				if (val3.m_cbSize > 0)
				{
					Marshal.Copy(val3.m_pData, array2, 0, val3.m_cbSize);
				}
				SteamNetworkingMessage_t.Release(_statusRecvBuf[k]);
				if (array2.Length >= 2 && array2[0] == 136)
				{
					_statusResponseValue = array2[1] != 0;
					_statusResponsePending = true;
				}
			}
		}
		while (num == _statusRecvBuf.Length);
		for (int l = 0; l < 4; l++)
		{
			if (_pendingChunks.Count <= 0)
			{
				break;
			}
			(ulong to64, byte[] pkt) tuple = _pendingChunks.Peek();
			ulong item = tuple.to64;
			byte[] item2 = tuple.pkt;
			EResult val4 = SendMsg(new CSteamID(item), item2, 2);
			if ((int)val4 != 25)
			{
				_pendingChunks.Dequeue();
				if ((int)val4 != 1)
				{
					Plugin.Log.LogWarning($"[SaveFetch] SendMessageToUser devolveu {val4} - fatia pulada.");
				}
				if (_pendingCountByPeer.TryGetValue(item, out var value))
				{
					_pendingCountByPeer[item] = Math.Max(0, value - 1);
				}
				_lastProgressTime[item] = Time.time;
				continue;
			}
			break;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void HandleRequest(CSteamID from)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		int num;
		int num2;
		if (_pendingCountByPeer.TryGetValue(from.m_SteamID, out var value))
		{
			num = ((value > 0) ? 1 : 0);
			if (num != 0 && _lastProgressTime.TryGetValue(from.m_SteamID, out var value2))
			{
				num2 = ((Time.time - value2 > 20f) ? 1 : 0);
				goto IL_0043;
			}
		}
		else
		{
			num = 0;
		}
		num2 = 0;
		goto IL_0043;
		IL_0043:
		bool flag = (byte)num2 != 0;
		if (num != 0 && !flag)
		{
			Plugin.Log.LogInfo($"[SaveFetch] pedido duplicado de {from.m_SteamID} ignorado " + $"(envio já em andamento, {value} pacote(s) restantes).");
			return;
		}
		if (flag)
		{
			Plugin.Log.LogWarning($"[SaveFetch] envio anterior pra {from.m_SteamID} ficou {20f:F0}s+ " + "sem progresso (conexão provavelmente caiu) - descartando e recomeçando do zero.");
			PurgePeer(from.m_SteamID);
		}
		Plugin.Log.LogInfo($"[SaveFetch] pedido de save recebido de {from.m_SteamID} - respondendo.");
		string friendPersonaName = SteamFriends.GetFriendPersonaName(from);
		PeerJoinStatus = ((string.IsNullOrEmpty(friendPersonaName) || friendPersonaName == "[unknown]") ? from.m_SteamID.ToString() : friendPersonaName) + " connecting - downloading world save...";
		EnqueueChunked(from, WorldSync.SerializeWorld());
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static EResult SendMsg(CSteamID to, byte[] data, int channel)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity val = default(SteamNetworkingIdentity);
		val.SetSteamID(to);
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try
		{
			int num = 40;
			return SteamNetworkingMessages.SendMessageToUser(ref val, gCHandle.AddrOfPinnedObject(), (uint)data.Length, num, channel);
		}
		finally
		{
			gCHandle.Free();
		}
	}

	private static void PurgePeer(ulong peerId)
	{
		int count = _pendingChunks.Count;
		for (int i = 0; i < count; i++)
		{
			(ulong, byte[]) item = _pendingChunks.Dequeue();
			if (item.Item1 != peerId)
			{
				_pendingChunks.Enqueue(item);
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void EnqueueChunked(CSteamID to, byte[] blob)
	{
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		int num = (blob.Length + 60000 - 1) / 60000;
		if (num == 0)
		{
			num = 1;
		}
		Plugin.Log.LogInfo($"[SaveFetch] enfileirando {blob.Length} bytes em {num} pacote(s) ({4}/tick).");
		_pendingCountByPeer[to.m_SteamID] = num;
		for (int i = 0; i < num; i++)
		{
			int num2 = i * 60000;
			int num3 = Math.Min(60000, blob.Length - num2);
			byte[] array = new byte[Math.Max(num3, 0)];
			if (num3 > 0)
			{
				Array.Copy(blob, num2, array, 0, num3);
			}
			byte[] item = PacketWriter.Begin(PacketType.SaveFetchResponse).UShort((ushort)i).UShort((ushort)num)
				.Bytes(array)
				.Finish();
			_pendingChunks.Enqueue((to.m_SteamID, item));
		}
	}
}
