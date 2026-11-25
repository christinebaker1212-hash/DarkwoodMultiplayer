using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Steamworks;

namespace DarkwoodCoopOnline.Net;

// The co-op protocol over Steam P2P (SteamNetworkingMessages, relayed by Valve: no ports, no IPs).
// One message = one frame. Everything Steam lives in this file behind NoInlining methods, so builds of
// the game without Steam (GOG / Epic) never load Steamworks types. Call only when SteamAvailability.Available.
public static class SteamCarrier
{
	public sealed class SteamLink : ILink
	{
		private struct Out
		{
			public byte[] Data;

			public bool Droppable;

			public int Key;

			public long Seq;
		}

		public readonly ulong Peer;

		private readonly ConcurrentQueue<Out> _out = new ConcurrentQueue<Out>();

		private readonly ConcurrentDictionary<int, long> _latest = new ConcurrentDictionary<int, long>();

		private long _seq;

		private int _queued;

		private volatile bool _closed;

		public Action<SteamLink, byte[]> OnFrame;

		public Action<SteamLink> OnClosed;

		public DateTime LastHeard = DateTime.UtcNow;

		public DateTime LastKeepAlive;

		public object Tag;

		public string Address => "steam:" + Peer;

		public bool Closed => _closed;

		public int QueuedBytes => _queued;

		public SteamLink(ulong peer)
		{
			Peer = peer;
		}

		public void Send(byte[] frame, bool droppable = false, int coalesceKey = 0)
		{
			if (!_closed)
			{
				if (droppable && _queued > 192 * 1024)
				{
					return;
				}
				long num = System.Threading.Interlocked.Increment(ref _seq);
				if (droppable && coalesceKey != 0)
				{
					_latest[coalesceKey] = num;
				}
				System.Threading.Interlocked.Add(ref _queued, frame.Length);
				_out.Enqueue(new Out
				{
					Data = frame,
					Droppable = droppable,
					Key = (droppable ? coalesceKey : 0),
					Seq = num
				});
			}
		}

		// main thread
		internal void Flush()
		{
			Out result;
			while (_out.TryDequeue(out result))
			{
				System.Threading.Interlocked.Add(ref _queued, -result.Data.Length);
				if (!_closed && (result.Key == 0 || !_latest.TryGetValue(result.Key, out var value) || value == result.Seq))
				{
					SendNow(Peer, result.Data, !result.Droppable);
				}
			}
		}

		public void Close()
		{
			if (!_closed)
			{
				_closed = true;
				lock (Links)
				{
					if (Links.TryGetValue(Peer, out var value) && value == this)
					{
						Links.Remove(Peer);
					}
				}
				try
				{
					CloseSession(Peer);
				}
				catch
				{
				}
				OnClosed?.Invoke(this);
			}
		}
	}

	private const int Channel = 3;

	public const string ConnectPrefix = "+dwcoop ";

	private static readonly Dictionary<ulong, SteamLink> Links = new Dictionary<ulong, SteamLink>();

	private static readonly IntPtr[] RecvBuf = new IntPtr[64];

	private static object _sessionRequestCb;

	private static object _sessionFailedCb;

	private static object _joinRequestCb;

	// Host side: a new Steam user sent us a first frame (password etc. is checked by the relay server)
	public static Func<ulong, SteamLink> AcceptIncoming;

	// Someone clicked "Join game" on us in their Steam friends list / accepted an invite
	public static Action<ulong> OnJoinRequested;

	public static bool Started { get; private set; }

	public static void Start()
	{
		if (!Started)
		{
			Started = true;
			StartImpl();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void StartImpl()
	{
		if (!SteamManager.Initialized)
		{
			Started = false;
			return;
		}
		try
		{
			SteamNetworking.AllowP2PPacketRelay(true);
		}
		catch
		{
		}
		_sessionRequestCb = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(OnSessionRequest);
		_sessionFailedCb = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(OnSessionFailed);
		_joinRequestCb = Callback<GameRichPresenceJoinRequested_t>.Create(OnRichPresenceJoin);
		Plugin.Log.LogInfo("[Steam] co-op over Steam ready (my id " + SteamUser.GetSteamID().m_SteamID + ").");
	}

	public static SteamLink Connect(ulong peer)
	{
		lock (Links)
		{
			if (Links.TryGetValue(peer, out var value))
			{
				value.Close();
			}
			SteamLink steamLink = new SteamLink(peer);
			Links[peer] = steamLink;
			return steamLink;
		}
	}

	private static void OnSessionRequest(SteamNetworkingMessagesSessionRequest_t req)
	{
		SteamNetworkingIdentity identityRemote = req.m_identityRemote;
		ulong steamID = identityRemote.GetSteamID().m_SteamID;
		bool flag;
		lock (Links)
		{
			flag = Links.ContainsKey(steamID) || AcceptIncoming != null;
		}
		if (flag)
		{
			SteamNetworkingMessages.AcceptSessionWithUser(ref identityRemote);
		}
	}

	private static void OnSessionFailed(SteamNetworkingMessagesSessionFailed_t f)
	{
		Plugin.Log.LogWarning("[Steam] session failed (reason " + f.m_info.m_eEndReason + ").");
	}

	private static void OnRichPresenceJoin(GameRichPresenceJoinRequested_t cb)
	{
		if (TryParseConnect(cb.m_rgchConnect, out var host))
		{
			OnJoinRequested?.Invoke(host);
		}
		else
		{
			Plugin.Log.LogWarning("[Steam] join request with an unknown connect string '" + cb.m_rgchConnect + "' (an older version of the mod?).");
		}
	}

	public static bool TryParseConnect(string s, out ulong host)
	{
		host = 0uL;
		if (s == null || !s.StartsWith(ConnectPrefix, StringComparison.Ordinal))
		{
			return false;
		}
		return ulong.TryParse(s.Substring(ConnectPrefix.Length).Trim(), out host) && host != 0;
	}

	// Main thread, every frame
	public static void Pump()
	{
		if (Started)
		{
			PumpImpl();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void PumpImpl()
	{
		int num;
		do
		{
			num = SteamNetworkingMessages.ReceiveMessagesOnChannel(Channel, RecvBuf, RecvBuf.Length);
			for (int i = 0; i < num; i++)
			{
				try
				{
					SteamNetworkingMessage_t steamNetworkingMessage_t = Marshal.PtrToStructure<SteamNetworkingMessage_t>(RecvBuf[i]);
					ulong steamID = steamNetworkingMessage_t.m_identityPeer.GetSteamID().m_SteamID;
					byte[] array = new byte[steamNetworkingMessage_t.m_cbSize];
					Marshal.Copy(steamNetworkingMessage_t.m_pData, array, 0, steamNetworkingMessage_t.m_cbSize);
					SteamLink value;
					lock (Links)
					{
						Links.TryGetValue(steamID, out value);
					}
					if (value == null && AcceptIncoming != null)
					{
						value = AcceptIncoming(steamID);
						if (value != null)
						{
							lock (Links)
							{
								Links[steamID] = value;
							}
						}
					}
					if (value != null && !value.Closed && array.Length != 0)
					{
						value.LastHeard = DateTime.UtcNow;
						if (array[0] != Proto.KEEPALIVE)
						{
							value.OnFrame?.Invoke(value, array);
						}
					}
				}
				finally
				{
					SteamNetworkingMessage_t.Release(RecvBuf[i]);
				}
			}
		}
		while (num == RecvBuf.Length);
		List<SteamLink> list;
		lock (Links)
		{
			list = new List<SteamLink>(Links.Values);
		}
		DateTime utcNow = DateTime.UtcNow;
		foreach (SteamLink item in list)
		{
			item.Flush();
			// keep-alive so the other side (and its 45 s timeout) knows we're here while a world loads
			if (!item.Closed && (utcNow - item.LastKeepAlive).TotalSeconds >= 2.0)
			{
				item.LastKeepAlive = utcNow;
				SendNow(item.Peer, new byte[1] { Proto.KEEPALIVE }, reliable: false);
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void SendNow(ulong peer, byte[] data, bool reliable)
	{
		SteamNetworkingIdentity steamNetworkingIdentity = default(SteamNetworkingIdentity);
		steamNetworkingIdentity.SetSteamID(new CSteamID(peer));
		// Reliable|NoNagle or Unreliable|NoNagle, both with AutoRestartBrokenSession
		int num = (reliable ? 9 : 1) | 0x20;
		GCHandle gCHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
		try
		{
			SteamNetworkingMessages.SendMessageToUser(ref steamNetworkingIdentity, gCHandle.AddrOfPinnedObject(), (uint)data.Length, num, Channel);
		}
		finally
		{
			gCHandle.Free();
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void CloseSession(ulong peer)
	{
		SteamNetworkingIdentity steamNetworkingIdentity = default(SteamNetworkingIdentity);
		steamNetworkingIdentity.SetSteamID(new CSteamID(peer));
		SteamNetworkingMessages.CloseSessionWithUser(ref steamNetworkingIdentity);
	}

	// ---- friends / rich presence ----

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static ulong MyId()
	{
		return SteamUser.GetSteamID().m_SteamID;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static string MyName()
	{
		return SteamFriends.GetPersonaName();
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static string NameOf(ulong id)
	{
		return SteamFriends.GetFriendPersonaName(new CSteamID(id));
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void SetHosting(bool hosting)
	{
		try
		{
			SteamFriends.SetRichPresence("connect", hosting ? (ConnectPrefix + MyId()) : null);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[Steam] rich presence: " + ex.Message);
		}
	}

	// Steam friends in Darkwood who are hosting a co-op game right now
	[MethodImpl(MethodImplOptions.NoInlining)]
	public static List<(ulong id, string name)> FriendsHosting()
	{
		List<(ulong, string)> list = new List<(ulong, string)>();
		try
		{
			int friendCount = SteamFriends.GetFriendCount((EFriendFlags)4);
			FriendGameInfo_t friendGameInfo_t = default(FriendGameInfo_t);
			for (int i = 0; i < friendCount; i++)
			{
				CSteamID friendByIndex = SteamFriends.GetFriendByIndex(i, (EFriendFlags)4);
				if (SteamFriends.GetFriendGamePlayed(friendByIndex, ref friendGameInfo_t) && friendGameInfo_t.m_gameID.AppID().m_AppId == 274520)
				{
					SteamFriends.RequestFriendRichPresence(friendByIndex);
					if (TryParseConnect(SteamFriends.GetFriendRichPresence(friendByIndex, "connect"), out var host))
					{
						list.Add((host, SteamFriends.GetFriendPersonaName(friendByIndex)));
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[Steam] friends list: " + ex.Message);
		}
		return list;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	public static void OpenFriendsOverlay()
	{
		SteamFriends.ActivateGameOverlay("friends");
	}

	public static void StopAll()
	{
		List<SteamLink> list;
		lock (Links)
		{
			list = new List<SteamLink>(Links.Values);
		}
		foreach (SteamLink item in list)
		{
			item.Close();
		}
		AcceptIncoming = null;
	}
}
