using System;
using System.Collections.Generic;
using System.Text;

namespace DarkwoodCoopOnline.Net;

// This PC's place in a co-op game: either the host (owns the RelayServer, slot 0) or a player connected
// to one (RelayClient). Lives from HOST/JOIN until the game ends, across scene loads. Turns network
// frames into events for the game's main thread (CoopSession) and keeps the roster.
public sealed class CoopConnection
{
	public enum EvKind
	{
		PeerJoined,
		PeerInGame,
		PeerLeft,
		State,
		Game,
		SaveRequest,
		Disconnected
	}

	public struct Ev
	{
		public EvKind Kind;

		public int Slot;

		public byte[] Payload;
	}

	public sealed class Peer
	{
		public int Slot;

		public string Name = "";

		public string StableId = "";

		public bool InGame;
	}

	// The connection in use (null = not online)
	public static CoopConnection Current;

	private readonly Queue<Ev> _events = new Queue<Ev>();

	public readonly Peer[] Roster = new Peer[Proto.MaxPlayers];

	public bool IsHost { get; private set; }

	public RelayServer Server { get; private set; }

	public RelayClient Client { get; private set; }

	public int MySlot { get; private set; } = -1;


	public string TransportName { get; private set; } = "";


	// what a joining player learns before playing
	public bool GotRoomInfo { get; private set; }

	public string RoomName { get; private set; } = "";


	public string Welcome { get; private set; } = "";


	public bool RoomLocked { get; private set; }

	public int RoomPlayers { get; private set; }

	public bool Joined { get; private set; }

	public Proto.HostStatus HostStatus { get; private set; }

	public Proto.Reject? Rejected { get; private set; }

	public string RejectText { get; private set; }

	public int SaveTotal { get; private set; }

	public int SaveHave { get; private set; }

	public byte[] SaveBlob { get; private set; }

	public bool SentHello { get; private set; }

	public bool Closed { get; private set; }

	public string DisconnectText { get; private set; }

	public bool Alive
	{
		get
		{
			if (Closed)
			{
				return false;
			}
			if (!IsHost)
			{
				return Client?.Connected ?? false;
			}
			return Server?.Running ?? false;
		}
	}

	public int RttMs
	{
		get
		{
			if (!IsHost)
			{
				return Client?.RttMs ?? (-1);
			}
			return 0;
		}
	}

	public static CoopConnection ForHost(RelayServer server, string hostName, string hostStableId, string transportName)
	{
		CoopConnection coopConnection = new CoopConnection
		{
			IsHost = true,
			Server = server,
			MySlot = 0,
			Joined = true,
			GotRoomInfo = true,
			RoomName = server.RoomName,
			TransportName = transportName
		};
		coopConnection.Roster[0] = new Peer
		{
			Slot = 0,
			Name = hostName,
			StableId = hostStableId,
			InGame = true
		};
		return coopConnection;
	}

	public static CoopConnection ForClient(RelayClient client, string transportName)
	{
		return new CoopConnection
		{
			IsHost = false,
			Client = client,
			TransportName = transportName
		};
	}

	public void Join(string password, string myName, string myStableId)
	{
		Rejected = null;
		RejectText = null;
		Client?.Join(Proto.RoomKey(RoomName, password), myName, myStableId);
	}

	public void RequestSave()
	{
		SaveBlob = null;
		SaveHave = 0;
		SaveTotal = 0;
		Client?.RequestSave();
	}

	// The world is loaded: start receiving the game
	public void Hello()
	{
		if (!IsHost && !SentHello)
		{
			SentHello = true;
			Client?.Hello();
		}
	}

	public void SendGame(int target, byte[] packet, bool reliable)
	{
		if (Closed || packet == null || packet.Length == 0)
		{
			return;
		}
		if (IsHost)
		{
			Server?.SendGame(target, packet, !reliable);
		}
		else
		{
			Client?.SendGame(target, packet, !reliable);
		}
	}

	public void SendState(byte[] packet)
	{
		if (!Closed && packet != null)
		{
			if (IsHost)
			{
				Server?.SendState(packet);
			}
			else
			{
				Client?.SendState(packet);
			}
		}
	}

	public string NameOf(int slot)
	{
		if (slot < 0 || slot >= Roster.Length)
		{
			return null;
		}
		return Roster[slot]?.Name;
	}

	public string StableIdOf(int slot)
	{
		if (slot < 0 || slot >= Roster.Length)
		{
			return null;
		}
		return Roster[slot]?.StableId;
	}

	public int PlayerCount
	{
		get
		{
			int num = 0;
			Peer[] roster = Roster;
			for (int i = 0; i < roster.Length; i++)
			{
				if (roster[i] != null)
				{
					num++;
				}
			}
			return num;
		}
	}

	public bool Poll(out Ev ev)
	{
		if (_events.Count > 0)
		{
			ev = _events.Dequeue();
			return true;
		}
		ev = default(Ev);
		return false;
	}

	private void Push(EvKind kind, int slot, byte[] payload = null)
	{
		_events.Enqueue(new Ev
		{
			Kind = kind,
			Slot = slot,
			Payload = payload
		});
	}

	// Main thread, every frame (also at the main menu while joining)
	public void Tick(float dt)
	{
		if (Closed)
		{
			return;
		}
		if (IsHost)
		{
			TickHost();
		}
		else
		{
			TickClient(dt);
		}
	}

	private void TickHost()
	{
		Server.Maintain();
		RelayServer.Ev result;
		while (Server.Events.TryDequeue(out result))
		{
			switch (result.Kind)
			{
			case RelayServer.EvKind.Joined:
			{
				string[] array = Encoding.UTF8.GetString(result.Payload).Split(new char[1] { '\n' }, 2);
				Roster[result.Slot] = new Peer
				{
					Slot = result.Slot,
					Name = array[0],
					StableId = ((array.Length > 1) ? array[1] : "")
				};
				Push(EvKind.PeerJoined, result.Slot);
				break;
			}
			case RelayServer.EvKind.InGame:
				if (Roster[result.Slot] != null)
				{
					Roster[result.Slot].InGame = true;
				}
				Push(EvKind.PeerInGame, result.Slot);
				break;
			case RelayServer.EvKind.Left:
				Roster[result.Slot] = null;
				Push(EvKind.PeerLeft, result.Slot);
				break;
			case RelayServer.EvKind.State:
				Push(EvKind.State, result.Slot, result.Payload);
				break;
			case RelayServer.EvKind.Game:
				Push(EvKind.Game, result.Slot, result.Payload);
				break;
			case RelayServer.EvKind.SaveRequest:
				Push(EvKind.SaveRequest, result.Slot);
				break;
			}
		}
	}

	private void TickClient(float dt)
	{
		Client.Tick(dt);
		byte[] result;
		while (Client.Inbox.TryDequeue(out result))
		{
			try
			{
				HandleClientFrame(result);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning($"[Net] bad frame 0x{result[0]:X2}: {ex.Message}");
			}
		}
		if (!Client.Connected && !Closed)
		{
			Closed = true;
			if (DisconnectText == null)
			{
				DisconnectText = Client.CloseReason ?? "Lost the connection to the host.";
			}
			Push(EvKind.Disconnected, 0);
		}
	}

	private void HandleClientFrame(byte[] f)
	{
		MsgReader msgReader = new MsgReader(f);
		switch (f[0])
		{
		case Proto.S_ROOM_INFO:
		{
			msgReader.U16();
			RoomName = msgReader.Str();
			RoomLocked = msgReader.U8() != 0;
			RoomPlayers = msgReader.U8();
			msgReader.U8();
			HostStatus = (Proto.HostStatus)msgReader.U8();
			Welcome = msgReader.Str();
			GotRoomInfo = true;
			break;
		}
		case Proto.S_JOINED:
			MySlot = msgReader.U8();
			msgReader.U8();
			HostStatus = (Proto.HostStatus)msgReader.U8();
			Joined = true;
			Rejected = null;
			Plugin.Log.LogInfo($"[Net] joined \"{RoomName}\" as slot {MySlot}.");
			break;
		case Proto.S_REJECT:
		{
			Proto.Reject reject = (Proto.Reject)msgReader.U8();
			ushort serverVersion = msgReader.U16();
			Rejected = reject;
			RejectText = Proto.RejectText(reject, serverVersion);
			if (reject == Proto.Reject.HostLeft)
			{
				DisconnectText = RejectText;
			}
			Plugin.Log.LogWarning("[Net] host says: " + RejectText);
			break;
		}
		case Proto.S_HOST_STATUS:
			HostStatus = (Proto.HostStatus)msgReader.U8();
			break;
		case Proto.S_PLAYER:
		{
			int num = msgReader.U8();
			bool flag = msgReader.U8() != 0;
			string name = msgReader.Str();
			string stableId = msgReader.Str();
			if (num < 0 || num >= Roster.Length)
			{
				break;
			}
			bool flag2 = Roster[num] == null;
			bool flag3 = Roster[num] != null && Roster[num].InGame;
			Roster[num] = new Peer
			{
				Slot = num,
				Name = name,
				StableId = stableId,
				InGame = flag
			};
			if (num != MySlot)
			{
				if (flag2)
				{
					Push(EvKind.PeerJoined, num);
				}
				if (flag && !flag3)
				{
					Push(EvKind.PeerInGame, num);
				}
			}
			break;
		}
		case Proto.S_LEAVE:
		{
			int num2 = msgReader.U8();
			if (num2 >= 0 && num2 < Roster.Length && Roster[num2] != null)
			{
				Roster[num2] = null;
				Push(EvKind.PeerLeft, num2);
			}
			break;
		}
		case Proto.S_STATE:
		{
			int slot2 = msgReader.U8();
			Push(EvKind.State, slot2, msgReader.Rest());
			break;
		}
		case Proto.S_RELAY:
		{
			int slot = msgReader.U8();
			Push(EvKind.Game, slot, msgReader.Rest());
			break;
		}
		case Proto.S_SAVE:
		{
			int num3 = (int)msgReader.U32();
			int num4 = (int)msgReader.U32();
			byte[] array = msgReader.Rest();
			if (SaveBlob == null || SaveBlob.Length != num3)
			{
				SaveBlob = new byte[num3];
				SaveHave = 0;
			}
			SaveTotal = num3;
			Buffer.BlockCopy(array, 0, SaveBlob, num4, array.Length);
			SaveHave += array.Length;
			break;
		}
		}
	}

	public bool SaveComplete
	{
		get
		{
			if (SaveBlob != null && SaveTotal > 0)
			{
				return SaveHave >= SaveTotal;
			}
			return false;
		}
	}

	public void Close()
	{
		if (Closed && IsHost == false && Client?.Link == null)
		{
			return;
		}
		Closed = true;
		if (IsHost)
		{
			Server?.Stop();
		}
		else
		{
			Client?.Leave();
		}
		if (Current == this)
		{
			Current = null;
		}
	}
}
