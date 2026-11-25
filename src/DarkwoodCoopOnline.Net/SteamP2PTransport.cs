using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Steamworks;

namespace DarkwoodCoopOnline.Net;

public sealed class SteamP2PTransport : ITransport
{
	private const int Channel = 0;

	private const int MaxMessagesPerPump = 64;

	private CSteamID _peer;

	private SteamNetworkingIdentity _peerIdentity;

	private bool _peerKnown;

	private Callback<SteamNetworkingMessagesSessionRequest_t> _sessionRequest;

	private readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();

	private readonly IntPtr[] _recvBuffer = new IntPtr[64];

	public bool Connected { get; private set; }

	public string LocalIdentity => "steam:" + SteamUser.GetSteamID().m_SteamID;

	public string PeerIdentity => "steam:" + _peer.m_SteamID;

	public SteamP2PTransport(ulong peerSteamId64)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		_peer = new CSteamID(peerSteamId64);
		_peerIdentity = default(SteamNetworkingIdentity);
		_peerIdentity.SetSteamID(_peer);
		_peerKnown = true;
	}

	public SteamP2PTransport()
	{
		_peerKnown = false;
	}

	public void Start(NetRole role)
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		if (!SteamManager.Initialized)
		{
			Plugin.Log.LogError("[Steam] SteamManager não inicializado - abortando transporte Steam.");
			return;
		}
		_sessionRequest = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
		Connected = true;
		string arg = (_peerKnown ? _peer.m_SteamID.ToString() : "(qualquer um - aguardando 1ª conexão)");
		Plugin.Log.LogInfo($"[Steam] pronto (SteamNetworkingMessages). Peer={arg}  (meu ID={SteamUser.GetSteamID().m_SteamID})");
	}

	private void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t req)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Unknown result type (might be due to invalid IL or missing references)
		//IL_000e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		SteamNetworkingIdentity identityRemote = req.m_identityRemote;
		CSteamID steamID = identityRemote.GetSteamID();
		if (!_peerKnown)
		{
			_peer = steamID;
			_peerIdentity = identityRemote;
			_peerKnown = true;
			SteamNetworkingMessages.AcceptSessionWithUser(ref identityRemote);
			Plugin.Log.LogInfo($"[Steam] 1ª conexão aceita - travei no peer {steamID.m_SteamID}");
		}
		else if (steamID == _peer)
		{
			SteamNetworkingMessages.AcceptSessionWithUser(ref identityRemote);
			Plugin.Log.LogInfo($"[Steam] sessão aceita de {steamID.m_SteamID}");
		}
		else
		{
			Plugin.Log.LogWarning($"[Steam] recusando sessão de ID inesperado {steamID.m_SteamID}");
		}
	}

	public void Send(byte[] data, bool reliable)
	{
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		int num = (reliable ? 8 : 5) | 0x20;
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try
		{
			SteamNetworkingIdentity peerIdentity = _peerIdentity;
			EResult val = SteamNetworkingMessages.SendMessageToUser(ref peerIdentity, gCHandle.AddrOfPinnedObject(), (uint)data.Length, num, 0);
			if ((int)val != 1)
			{
				Plugin.Log.LogWarning($"[Steam] SendMessageToUser retornou {val}");
			}
		}
		finally
		{
			gCHandle.Free();
		}
	}

	public void Pump()
	{
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		int num;
		do
		{
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(0, _recvBuffer, _recvBuffer.Length);
			for (int i = 0; i < num; i++)
			{
				SteamNetworkingMessage_t val = Marshal.PtrToStructure<SteamNetworkingMessage_t>(_recvBuffer[i]);
				CSteamID steamID = val.m_identityPeer.GetSteamID();
				if (!_peerKnown || steamID == _peer)
				{
					byte[] array = new byte[val.m_cbSize];
					Marshal.Copy(val.m_pData, array, 0, val.m_cbSize);
					_inbox.Enqueue(array);
				}
				SteamNetworkingMessage_t.Release(_recvBuffer[i]);
			}
		}
		while (num == _recvBuffer.Length);
	}

	public bool TryReceive(out byte[] data)
	{
		return _inbox.TryDequeue(out data);
	}

	public void Stop()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		Connected = false;
		try
		{
			_sessionRequest?.Dispose();
		}
		catch
		{
		}
		try
		{
			SteamNetworkingIdentity peerIdentity = _peerIdentity;
			SteamNetworkingMessages.CloseSessionWithUser(ref peerIdentity);
		}
		catch
		{
		}
	}
}
