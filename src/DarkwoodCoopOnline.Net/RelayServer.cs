using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DarkwoodCoopOnline.Net;

// The relay server inside the HOST's game (Moemon Co-op's mp_server.c, one room).
// Connections arrive over TCP (invite code / IP / LAN) or are handed in by the Steam acceptor.
// Routing between players happens right on the network threads; only what the host itself needs
// goes through Events to the game's main thread.
public sealed class RelayServer
{
	public sealed class Member
	{
		public int Slot = -1;

		public ILink Link;

		public string Name = "";

		public string StableId = "";

		public bool Joined;

		public bool InGame;

		public byte[] LastState;

		public int BadPasswords;

		public DateTime SlowUntil;

		public DateTime LastHeard = DateTime.UtcNow;
	}

	public enum EvKind
	{
		Joined,
		InGame,
		Left,
		State,
		Game,
		SaveRequest
	}

	public struct Ev
	{
		public EvKind Kind;

		public int Slot;

		public byte[] Payload;
	}

	private const int SaveChunk = 64 * 1024;

	private const double DeadAfterSeconds = 45.0;

	private readonly object _gate = new object();

	private readonly Member[] _slots = new Member[Proto.MaxPlayers];

	private readonly List<Member> _all = new List<Member>();

	public readonly ConcurrentQueue<Ev> Events = new ConcurrentQueue<Ev>();

	private TcpListener _listener;

	private UdpClient _discovery;

	private volatile bool _running;

	private byte[] _key;

	private volatile byte _hostStatus;

	public int Port { get; private set; }

	public string RoomName { get; private set; } = "";


	public string Welcome { get; set; } = "";


	public bool Locked => _key != null;

	public bool Running => _running;

	public Proto.HostStatus HostStatus
	{
		get
		{
			return (Proto.HostStatus)_hostStatus;
		}
		set
		{
			if (_hostStatus == (byte)value)
			{
				return;
			}
			_hostStatus = (byte)value;
			byte[] frame = new byte[2]
			{
				Proto.S_HOST_STATUS,
				(byte)value
			};
			lock (_gate)
			{
				foreach (Member item in _all)
				{
					if (item.Link != null && item.Joined && !item.InGame)
					{
						item.Link.Send(frame);
					}
				}
			}
		}
	}

	public int PlayerCount
	{
		get
		{
			lock (_gate)
			{
				int num = 0;
				Member[] slots = _slots;
				for (int i = 0; i < slots.Length; i++)
				{
					if (slots[i] != null)
					{
						num++;
					}
				}
				return num;
			}
		}
	}

	// Starts listening on Proto.DefaultPort (or the next free of PortTries). password "" = open room.
	public bool Start(string roomName, string password, string hostName, string hostStableId, out string error)
	{
		error = null;
		RoomName = (string.IsNullOrEmpty(roomName) ? "Darkwood Co-op" : roomName.Trim());
		_key = (string.IsNullOrEmpty(password) ? null : Proto.RoomKey(RoomName, password));
		for (int i = 0; i < Proto.PortTries; i++)
		{
			try
			{
				TcpListener tcpListener = new TcpListener(IPAddress.Any, Proto.DefaultPort + i);
				tcpListener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, optionValue: false);
				tcpListener.Start(16);
				_listener = tcpListener;
				Port = Proto.DefaultPort + i;
				break;
			}
			catch (Exception ex)
			{
				error = ex.Message;
			}
		}
		if (_listener == null)
		{
			error = $"no free port between {Proto.DefaultPort} and {Proto.DefaultPort + Proto.PortTries - 1} ({error})";
			return false;
		}
		error = null;
		_slots[0] = new Member
		{
			Slot = 0,
			Name = hostName,
			StableId = hostStableId,
			Joined = true,
			InGame = true
		};
		_running = true;
		new Thread(AcceptLoop)
		{
			IsBackground = true,
			Name = "CoopRelayAccept"
		}.Start();
		try
		{
			_discovery = new UdpClient();
			_discovery.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, optionValue: true);
			_discovery.Client.Bind(new IPEndPoint(IPAddress.Any, Proto.DiscoveryPort));
			new Thread(DiscoveryLoop)
			{
				IsBackground = true,
				Name = "CoopLanDiscovery"
			}.Start();
		}
		catch (Exception ex2)
		{
			Plugin.Log?.LogWarning("[Relay] LAN discovery off (UDP " + Proto.DiscoveryPort + ": " + ex2.Message + ") - friends can still join with the code or IP.");
			_discovery = null;
		}
		Plugin.Log?.LogInfo($"[Relay] hosting \"{RoomName}\" on TCP {Port} (password: {(Locked ? "yes" : "no")}).");
		return true;
	}

	public void Stop()
	{
		if (!_running)
		{
			return;
		}
		_running = false;
		try
		{
			_listener?.Stop();
		}
		catch
		{
		}
		try
		{
			_discovery?.Close();
		}
		catch
		{
		}
		byte[] frame = new MsgWriter(Proto.S_REJECT).U8((byte)Proto.Reject.HostLeft).U16(Proto.Version).Done();
		List<Member> list;
		lock (_gate)
		{
			list = new List<Member>(_all);
			_all.Clear();
			Array.Clear(_slots, 0, _slots.Length);
		}
		foreach (Member item in list)
		{
			try
			{
				item.Link.Send(frame);
				if (item.Link is TcpLink tcpLink)
				{
					ThreadPool.QueueUserWorkItem(delegate
					{
						tcpLink.CloseAfterFlush(500);
					});
				}
				else
				{
					item.Link.Close();
				}
			}
			catch
			{
			}
		}
		Plugin.Log?.LogInfo("[Relay] stopped.");
	}

	private void AcceptLoop()
	{
		while (_running)
		{
			Socket socket;
			try
			{
				socket = _listener.AcceptSocket();
			}
			catch
			{
				if (!_running)
				{
					break;
				}
				Thread.Sleep(50);
				continue;
			}
			Member member = new Member();
			TcpLink tcpLink = new TcpLink(socket, delegate(TcpLink l, byte[] f)
			{
				Handle((Member)l.Tag, f);
			}, delegate(TcpLink l)
			{
				Drop((Member)l.Tag);
			})
			{
				Tag = member
			};
			member.Link = tcpLink;
			lock (_gate)
			{
				_all.Add(member);
			}
			tcpLink.Start();
			tcpLink.Send(RoomInfoFrame());
			Plugin.Log?.LogInfo("[Relay] connection from " + tcpLink.Address);
		}
	}

	// For links that don't come from the TCP listener (Steam). Frames then go to Handle(member, frame).
	public Member AddLink(ILink link)
	{
		Member member = new Member
		{
			Link = link
		};
		lock (_gate)
		{
			_all.Add(member);
		}
		link.Send(RoomInfoFrame());
		Plugin.Log?.LogInfo("[Relay] connection from " + link.Address);
		return member;
	}

	private byte[] RoomInfoFrame()
	{
		return new MsgWriter(Proto.S_ROOM_INFO).U16(Proto.Version).Str(RoomName).U8((byte)(Locked ? 1u : 0u))
			.U8((byte)PlayerCount)
			.U8(Proto.MaxPlayers)
			.U8(_hostStatus)
			.Str(Welcome)
			.Done();
	}

	public void Handle(Member m, byte[] f)
	{
		if (f.Length == 0 || !_running)
		{
			return;
		}
		m.LastHeard = DateTime.UtcNow;
		switch (f[0])
		{
		case Proto.C_JOIN:
			HandleJoin(m, f);
			break;
		case Proto.C_HELLO:
			HandleHello(m);
			break;
		case Proto.C_STATE:
		{
			if (!m.InGame || !IsCurrent(m))
			{
				break;
			}
			byte[] array3 = new byte[f.Length - 1];
			Buffer.BlockCopy(f, 1, array3, 0, array3.Length);
			m.LastState = array3;
			byte[] frame2 = MsgWriter.Wrap(Proto.S_STATE, (byte)m.Slot, array3);
			lock (_gate)
			{
				for (int j = 1; j < _slots.Length; j++)
				{
					Member member2 = _slots[j];
					if (member2 != null && member2 != m && member2.InGame)
					{
						member2.Link.Send(frame2, droppable: true, 4096 + m.Slot);
					}
				}
			}
			Events.Enqueue(new Ev
			{
				Kind = EvKind.State,
				Slot = m.Slot,
				Payload = array3
			});
			break;
		}
		case Proto.C_RELAY:
		{
			if (!m.InGame || f.Length < 4 || !IsCurrent(m))
			{
				break;
			}
			byte b = f[1];
			bool flag = (f[2] & 1) != 0;
			byte[] array = new byte[f.Length - 3];
			Buffer.BlockCopy(f, 3, array, 0, array.Length);
			byte[] frame = MsgWriter.Wrap(Proto.S_RELAY, (byte)m.Slot, array);
			int coalesceKey = (flag ? (65536 + (m.Slot << 8) + array[0]) : 0);
			lock (_gate)
			{
				for (int i = 1; i < _slots.Length; i++)
				{
					Member member = _slots[i];
					if (member != null && member != m && member.InGame && (b == byte.MaxValue || b == i))
					{
						member.Link.Send(frame, flag, coalesceKey);
					}
				}
			}
			if (b == byte.MaxValue || b == 0)
			{
				Events.Enqueue(new Ev
				{
					Kind = EvKind.Game,
					Slot = m.Slot,
					Payload = array
				});
			}
			break;
		}
		case Proto.C_PING:
		{
			byte[] array2 = (byte[])f.Clone();
			array2[0] = Proto.S_PONG;
			m.Link.Send(array2);
			break;
		}
		case Proto.C_SAVE_REQ:
			if (m.Joined)
			{
				Events.Enqueue(new Ev
				{
					Kind = EvKind.SaveRequest,
					Slot = m.Slot
				});
			}
			break;
		case Proto.C_LEAVE:
			m.Link.Close();
			break;
		}
	}

	// still the member in its slot (not replaced by a reconnect, not dropped)
	private bool IsCurrent(Member m)
	{
		lock (_gate)
		{
			return m.Slot > 0 && m.Slot < _slots.Length && _slots[m.Slot] == m;
		}
	}

	private void HandleJoin(Member m, byte[] f)
	{
		if (m.Joined)
		{
			return;
		}
		MsgReader msgReader = new MsgReader(f);
		ushort num;
		byte[] b;
		string text;
		string text2;
		try
		{
			num = msgReader.U16();
			b = msgReader.Bytes(32);
			text = msgReader.Str();
			text2 = msgReader.Str();
		}
		catch
		{
			m.Link.Close();
			return;
		}
		if (num != Proto.Version)
		{
			RejectAndClose(m, Proto.Reject.Version);
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		if (m.SlowUntil > utcNow)
		{
			Reject(m, Proto.Reject.SlowDown);
			return;
		}
		if (_key != null && !Proto.KeysEqual(_key, b))
		{
			if (++m.BadPasswords >= 5)
			{
				m.BadPasswords = 0;
				m.SlowUntil = utcNow.AddSeconds(10.0);
			}
			Reject(m, Proto.Reject.BadPassword);
			return;
		}
		Member member = null;
		List<byte[]> list = new List<byte[]>();
		int num2 = -1;
		lock (_gate)
		{
			// the same player coming back while their old connection hasn't timed out yet: replace it
			for (int i = 1; i < _slots.Length; i++)
			{
				if (_slots[i] != null && !string.IsNullOrEmpty(text2) && _slots[i].StableId == text2)
				{
					member = _slots[i];
				}
			}
			if (member != null)
			{
				RemoveLocked(member);
			}
			for (int j = 1; j < _slots.Length; j++)
			{
				if (_slots[j] == null)
				{
					num2 = j;
					break;
				}
			}
			if (num2 >= 0)
			{
			m.Slot = num2;
			m.Name = (string.IsNullOrEmpty(text) ? ("Player " + (num2 + 1)) : text);
			m.StableId = text2;
			m.Joined = true;
			_slots[num2] = m;
			m.Link.Send(new MsgWriter(Proto.S_JOINED).U8((byte)num2).U8(Proto.MaxPlayers).U8(_hostStatus)
				.Done());
			for (int k = 0; k < _slots.Length; k++)
			{
				Member member2 = _slots[k];
				if (member2 != null && member2 != m)
				{
					m.Link.Send(PlayerFrame(member2));
				}
			}
			byte[] frame = PlayerFrame(m);
			for (int l = 1; l < _slots.Length; l++)
			{
				Member member3 = _slots[l];
				if (member3 != null && member3 != m && member3.Joined)
				{
					member3.Link.Send(frame);
				}
			}
			}
		}
		if (member != null)
		{
			// the old connection of a player who reconnected: the host forgets that slot first
			Events.Enqueue(new Ev
			{
				Kind = EvKind.Left,
				Slot = member.Slot
			});
			try
			{
				member.Link.Close();
			}
			catch
			{
			}
		}
		if (num2 < 0)
		{
			RejectAndClose(m, Proto.Reject.Full);
			return;
		}
		Events.Enqueue(new Ev
		{
			Kind = EvKind.Joined,
			Slot = num2,
			Payload = Encoding.UTF8.GetBytes(m.Name + "\n" + m.StableId)
		});
		Plugin.Log?.LogInfo($"[Relay] {m.Link.Address} joined as '{m.Name}' (slot {num2}, {PlayerCount}/{Proto.MaxPlayers}).");
	}

	private void HandleHello(Member m)
	{
		if (!m.Joined || m.InGame)
		{
			return;
		}
		lock (_gate)
		{
			m.InGame = true;
			for (int i = 0; i < _slots.Length; i++)
			{
				Member member = _slots[i];
				if (member != null && member != m && member.InGame && member.LastState != null)
				{
					m.Link.Send(MsgWriter.Wrap(Proto.S_STATE, (byte)i, member.LastState));
				}
			}
			byte[] frame = PlayerFrame(m);
			for (int j = 1; j < _slots.Length; j++)
			{
				Member member2 = _slots[j];
				if (member2 != null && member2 != m && member2.Joined)
				{
					member2.Link.Send(frame);
				}
			}
		}
		Events.Enqueue(new Ev
		{
			Kind = EvKind.InGame,
			Slot = m.Slot
		});
		Plugin.Log?.LogInfo($"[Relay] '{m.Name}' (slot {m.Slot}) is in the world.");
	}

	private static byte[] PlayerFrame(Member p)
	{
		return new MsgWriter(Proto.S_PLAYER).U8((byte)p.Slot).U8((byte)(p.InGame ? 1u : 0u)).Str(p.Name)
			.Str(p.StableId)
			.Done();
	}

	private void Reject(Member m, Proto.Reject why)
	{
		m.Link.Send(new MsgWriter(Proto.S_REJECT).U8((byte)why).U16(Proto.Version).Done());
	}

	private void RejectAndClose(Member m, Proto.Reject why)
	{
		Reject(m, why);
		if (m.Link is TcpLink l)
		{
			ThreadPool.QueueUserWorkItem(delegate
			{
				l.CloseAfterFlush(1000);
			});
		}
		else
		{
			m.Link.Close();
		}
	}

	// Link closed (or timed out)
	public void Drop(Member m)
	{
		bool flag;
		lock (_gate)
		{
			flag = RemoveLocked(m);
		}
		if (flag)
		{
			Events.Enqueue(new Ev
			{
				Kind = EvKind.Left,
				Slot = m.Slot
			});
			Plugin.Log?.LogInfo($"[Relay] '{m.Name}' (slot {m.Slot}) left ({PlayerCount}/{Proto.MaxPlayers}).");
		}
	}

	private bool RemoveLocked(Member m)
	{
		_all.Remove(m);
		if (m.Slot <= 0 || _slots[m.Slot] != m)
		{
			return false;
		}
		_slots[m.Slot] = null;
		byte[] frame = new byte[2]
		{
			Proto.S_LEAVE,
			(byte)m.Slot
		};
		for (int i = 1; i < _slots.Length; i++)
		{
			_slots[i]?.Link.Send(frame);
		}
		return true;
	}

	// ---- the host's own traffic (main thread) ----

	public void SendGame(int target, byte[] packet, bool droppable)
	{
		byte[] frame = MsgWriter.Wrap(Proto.S_RELAY, 0, packet);
		int coalesceKey = (droppable ? (65536 + packet[0]) : 0);
		lock (_gate)
		{
			for (int i = 1; i < _slots.Length; i++)
			{
				Member member = _slots[i];
				if (member != null && member.InGame && (target < 0 || target == i))
				{
					member.Link.Send(frame, droppable, coalesceKey);
				}
			}
		}
	}

	public void SendState(byte[] packet)
	{
		Member member = _slots[0];
		if (member != null)
		{
			member.LastState = packet;
		}
		byte[] frame = MsgWriter.Wrap(Proto.S_STATE, 0, packet);
		lock (_gate)
		{
			for (int i = 1; i < _slots.Length; i++)
			{
				Member member2 = _slots[i];
				if (member2 != null && member2.InGame)
				{
					member2.Link.Send(frame, droppable: true, 4096);
				}
			}
		}
	}

	// The host's world save, in chunks, to a player who is still at the main menu
	public void SendSave(int slot, byte[] blob)
	{
		Member member;
		lock (_gate)
		{
			member = ((slot > 0 && slot < _slots.Length) ? _slots[slot] : null);
		}
		if (member == null)
		{
			return;
		}
		int num = 0;
		do
		{
			int num2 = Math.Min(SaveChunk, blob.Length - num);
			member.Link.Send(new MsgWriter(Proto.S_SAVE).U32((uint)blob.Length).U32((uint)num).Raw(blob, num, num2)
				.Done());
			num += num2;
		}
		while (num < blob.Length);
	}

	public string NameOf(int slot)
	{
		lock (_gate)
		{
			return (slot >= 0 && slot < _slots.Length) ? _slots[slot]?.Name : null;
		}
	}

	public string StableIdOf(int slot)
	{
		lock (_gate)
		{
			return (slot >= 0 && slot < _slots.Length) ? _slots[slot]?.StableId : null;
		}
	}

	public int QueuedBytesOf(int slot)
	{
		lock (_gate)
		{
			Member member = ((slot > 0 && slot < _slots.Length) ? _slots[slot] : null);
			return (member != null) ? member.Link.QueuedBytes : 0;
		}
	}

	// Main thread, once a frame: drops connections that went silent
	public void Maintain()
	{
		if (!_running)
		{
			return;
		}
		DateTime utcNow = DateTime.UtcNow;
		List<Member> list = null;
		lock (_gate)
		{
			foreach (Member item in _all)
			{
				DateTime dateTime = item.Link.LastReceived;
				if ((utcNow - dateTime).TotalSeconds > DeadAfterSeconds)
				{
					(list ?? (list = new List<Member>())).Add(item);
				}
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (Member item2 in list)
		{
			Plugin.Log?.LogWarning("[Relay] " + item2.Link.Address + " stopped answering - dropping.");
			item2.Link.Close();
			Drop(item2);
		}
	}

	// LAN discovery. Probe "DWCOOP?" u16 version -> answer "DWCOOP!" u16 version u16 port u8 players u8 max u8 locked str name
	private void DiscoveryLoop()
	{
		IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
		while (_running)
		{
			byte[] array;
			try
			{
				array = _discovery.Receive(ref remoteEP);
			}
			catch
			{
				if (!_running)
				{
					break;
				}
				continue;
			}
			if (array.Length < 7 || Encoding.ASCII.GetString(array, 0, 7) != "DWCOOP?")
			{
				continue;
			}
			MsgWriter msgWriter = new MsgWriter((byte)'D');
			msgWriter.Raw(Encoding.ASCII.GetBytes("WCOOP!")).U16(Proto.Version).U16((ushort)Port)
				.U8((byte)PlayerCount)
				.U8(Proto.MaxPlayers)
				.U8((byte)(Locked ? 1u : 0u))
				.Str(RoomName);
			byte[] array2 = msgWriter.Done();
			try
			{
				_discovery.Send(array2, array2.Length, remoteEP);
			}
			catch
			{
			}
		}
	}
}
