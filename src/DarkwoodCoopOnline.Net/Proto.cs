using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DarkwoodCoopOnline.Net;

// Darkwood Co-op network protocol (modelled on Moemon Emerald Kai Co-op's lobby + relay design).
//
//   * The HOST's game runs a small relay server (RelayServer). Everyone else connects to it, over TCP
//     (invite code / IP / LAN list) or over Steam P2P. Up to MaxPlayers in one game; the host is slot 0.
//   * A connection first gets ROOM_INFO (name, password or not, players, host status), then sends JOIN
//     with the room key (SHA-256 of the lowercase name + "\n" + password: the password never travels).
//   * Before the world is loaded a joiner can ask for the host's save (SAVE_REQ -> SAVE chunks) on the
//     same connection. When its world is loaded it says HELLO and from then on receives everyone's
//     STATE (cached by the server, so late joiners see everyone at once) and RELAY messages.
//   * Game packets (PacketType ...) travel inside STATE / RELAY. RELAY has a target: one slot or everyone.
//
// Frame on a stream: u32 length (little endian, = 1 + payload) then u8 type then payload.
// On Steam one message = one frame without the length.
public static class Proto
{
	// Bump whenever the wire format or a game packet changes: older and newer games refuse each other.
	public const ushort Version = 1;

	public const int DefaultPort = 27115;      // TCP, the host's relay server (and up to +6: the invite code carries which)

	public const int DiscoveryPort = 27116;    // UDP, finding games on the local network

	public const int PortTries = 7;   // (the invite code's 8th value means "through Steam")

	public const int MaxPlayers = 4;

	public const int MaxFrame = 16 * 1024 * 1024;

	public const byte Everyone = byte.MaxValue;

	public const int RoomNameMax = 24;

	public const byte KEEPALIVE = 0;  // (no payload; links send it when idle, never delivered)

	// client -> server
	public const byte C_JOIN = 1;      // u16 version, u8[32] key, str name, str stableId

	public const byte C_HELLO = 2;     // (world loaded, in game)

	public const byte C_STATE = 3;     // game packet (PlayerState)

	public const byte C_RELAY = 4;     // u8 target, game packet

	public const byte C_PING = 5;      // u32 stamp

	public const byte C_SAVE_REQ = 6;  // (send me the host's world)

	public const byte C_LEAVE = 7;

	// server -> client
	public const byte S_ROOM_INFO = 19;  // u16 version, str name, u8 locked, u8 players, u8 max, u8 hostStatus, str welcome

	public const byte S_JOINED = 20;     // u8 yourSlot, u8 max, u8 hostStatus

	public const byte S_REJECT = 21;     // u8 reason, u16 server version

	public const byte S_PLAYER = 22;     // u8 slot, u8 inGame, str name, str stableId

	public const byte S_LEAVE = 23;      // u8 slot

	public const byte S_STATE = 24;      // u8 slot, game packet

	public const byte S_RELAY = 25;      // u8 fromSlot, game packet

	public const byte S_PONG = 26;       // u32 stamp

	public const byte S_SAVE = 27;       // u32 total, u32 offset, bytes

	public const byte S_HOST_STATUS = 28; // u8 hostStatus

	public enum Reject : byte
	{
		Full = 1,
		Version = 2,
		BadPassword = 3,
		NotHosting = 4,
		SlowDown = 5,
		HostLeft = 6
	}

	public enum HostStatus : byte
	{
		Loading = 0,
		Chapter = 1,
		Prologue = 2
	}

	public static string RejectText(Reject r, ushort serverVersion)
	{
		switch (r)
		{
		case Reject.Full:
			return $"That game is full ({MaxPlayers} players).";
		case Reject.Version:
			return (serverVersion > Version) ? "The host has a NEWER version of Darkwood Co-op. Update yours." : "The host has an OLDER version of Darkwood Co-op. They need to update.";
		case Reject.BadPassword:
			return "Wrong password.";
		case Reject.NotHosting:
			return "That player isn't hosting right now.";
		case Reject.SlowDown:
			return "Too many wrong passwords - wait a few seconds.";
		case Reject.HostLeft:
			return "The host closed the game.";
		default:
			return "Refused by the host.";
		}
	}

	// SHA-256 of "<lowercase room name>\n<password>". Rooms without a password accept any key.
	public static byte[] RoomKey(string roomName, string password)
	{
		using SHA256 sha = SHA256.Create();
		return sha.ComputeHash(Encoding.UTF8.GetBytes((roomName ?? "").Trim().ToLowerInvariant() + "\n" + (password ?? "")));
	}

	public static bool KeysEqual(byte[] a, byte[] b)
	{
		if (a == null || b == null || a.Length != b.Length)
		{
			return false;
		}
		int num = 0;
		for (int i = 0; i < a.Length; i++)
		{
			num |= a[i] ^ b[i];
		}
		return num == 0;
	}
}

// Little-endian message builder. Frame(type) reserves the type byte at [0].
public sealed class MsgWriter
{
	private readonly MemoryStream _ms = new MemoryStream(64);

	private readonly BinaryWriter _bw;

	public MsgWriter(byte type)
	{
		_bw = new BinaryWriter(_ms);
		_bw.Write(type);
	}

	public MsgWriter U8(byte v)
	{
		_bw.Write(v);
		return this;
	}

	public MsgWriter U16(ushort v)
	{
		_bw.Write(v);
		return this;
	}

	public MsgWriter U32(uint v)
	{
		_bw.Write(v);
		return this;
	}

	public MsgWriter Raw(byte[] v)
	{
		_bw.Write(v);
		return this;
	}

	public MsgWriter Raw(byte[] v, int offset, int count)
	{
		_bw.Write(v, offset, count);
		return this;
	}

	public MsgWriter Str(string s)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(s ?? "");
		int num = Math.Min(bytes.Length, 1024);
		_bw.Write((ushort)num);
		_bw.Write(bytes, 0, num);
		return this;
	}

	public byte[] Done()
	{
		_bw.Flush();
		return _ms.ToArray();
	}

	// [type][prefix byte][payload] without an intermediate copy of payload
	public static byte[] Wrap(byte type, byte prefix, byte[] payload)
	{
		byte[] array = new byte[payload.Length + 2];
		array[0] = type;
		array[1] = prefix;
		Buffer.BlockCopy(payload, 0, array, 2, payload.Length);
		return array;
	}

	public static byte[] Wrap(byte type, byte[] payload)
	{
		byte[] array = new byte[payload.Length + 1];
		array[0] = type;
		Buffer.BlockCopy(payload, 0, array, 1, payload.Length);
		return array;
	}
}

public sealed class MsgReader
{
	private readonly byte[] _d;

	private int _p;

	public MsgReader(byte[] frame, int start = 1)
	{
		_d = frame;
		_p = start;
	}

	public int Remaining => _d.Length - _p;

	public int Position => _p;

	public byte U8()
	{
		return _d[_p++];
	}

	public ushort U16()
	{
		ushort result = (ushort)(_d[_p] | (_d[_p + 1] << 8));
		_p += 2;
		return result;
	}

	public uint U32()
	{
		uint result = (uint)(_d[_p] | (_d[_p + 1] << 8) | (_d[_p + 2] << 16) | (_d[_p + 3] << 24));
		_p += 4;
		return result;
	}

	public byte[] Bytes(int n)
	{
		byte[] array = new byte[n];
		Buffer.BlockCopy(_d, _p, array, 0, n);
		_p += n;
		return array;
	}

	public byte[] Rest()
	{
		return Bytes(Remaining);
	}

	public string Str()
	{
		int num = U16();
		string @string = Encoding.UTF8.GetString(_d, _p, num);
		_p += num;
		return @string;
	}
}
