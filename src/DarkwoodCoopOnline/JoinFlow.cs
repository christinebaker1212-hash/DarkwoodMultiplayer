using System;
using System.IO;
using System.Text;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using UnityEngine;

namespace DarkwoodCoopOnline;

// JOIN A GAME, from the main menu: connect (invite code, IP, LAN list, Steam friend or REJOIN), learn the
// room, give the password if it has one, get the host's world over the same connection, load it, play.
public static class JoinFlow
{
	public enum Step
	{
		Idle,
		Connecting,
		WaitingRoom,
		NeedPassword,
		Joining,
		WaitingHost,
		Downloading,
		Loading,
		Failed
	}

	private static CoopConnection _conn;

	private static string _address = "";

	private static ulong _steamHost;

	private static byte[] _rememberedKey;

	private static string _rememberedRoom;

	private static byte[] _key;

	private static float _timer;

	private static volatile TcpLink _connectedLink;

	private static volatile string _connectError;

	private static volatile bool _connectDone;

	private static RelayClient _pendingClient;

	public static Step Current { get; private set; }

	public static string Error { get; private set; } = "";


	public static string Target { get; private set; } = "";


	public static string RoomName => _conn?.RoomName ?? "";

	public static string Welcome => _conn?.Welcome ?? "";

	public static bool Busy
	{
		get
		{
			if (Current != Step.Idle)
			{
				return Current != Step.Failed;
			}
			return false;
		}
	}

	public static string StatusText
	{
		get
		{
			switch (Current)
			{
			case Step.Connecting:
				return "Connecting to " + Target + "...";
			case Step.WaitingRoom:
				return "Connected - asking about the game...";
			case Step.NeedPassword:
				return "\"" + RoomName + "\" needs a password.";
			case Step.Joining:
				return "Joining \"" + RoomName + "\"...";
			case Step.WaitingHost:
				return "\"" + RoomName + "\": the host is still loading their world...";
			case Step.Downloading:
			{
				CoopConnection conn = _conn;
				int num = ((conn != null && conn.SaveTotal > 0) ? (conn.SaveHave * 100 / conn.SaveTotal) : 0);
				return $"Getting the host's world... {num}%";
			}
			case Step.Loading:
				return "Loading the world...";
			case Step.Failed:
				return Error;
			default:
				return "";
			}
		}
	}

	public static bool CanRejoin
	{
		get
		{
			if (string.IsNullOrEmpty(CoopConfig.LastAddress.Value))
			{
				if (!string.IsNullOrEmpty(CoopConfig.LastSteamHost.Value))
				{
					return SteamAvailability.Available;
				}
				return false;
			}
			return true;
		}
	}

	public static string RejoinLabel
	{
		get
		{
			string value = CoopConfig.LastRoomName.Value;
			if (!string.IsNullOrEmpty(value))
			{
				return value;
			}
			return CoopConfig.LastAddress.Value;
		}
	}

	private static bool CanStart()
	{
		if (CoopConnection.Current != null || CoopRuntime.Session != null)
		{
			Fail("You're already in a game - leave it first (F10).");
			return false;
		}
		if (Busy)
		{
			return false;
		}
		return true;
	}

	public static void JoinAddress(string input, byte[] rememberedKey = null, string rememberedRoom = null)
	{
		if (!CanStart())
		{
			return;
		}
		if (!InviteCode.TryResolve(input, out var host, out var port))
		{
			Fail("That's not an invite code or an address.");
			return;
		}
		Reset();
		_address = input.Trim();
		Target = ((port == Proto.DefaultPort) ? host : (host + ":" + port));
		_rememberedKey = rememberedKey;
		_rememberedRoom = rememberedRoom;
		Current = Step.Connecting;
		_timer = 0f;
		_pendingClient = new RelayClient();
		RelayClient client = _pendingClient;
		Plugin.Log.LogInfo($"[Join] connecting to {host}:{port}...");
		TcpLink.ConnectAsync(host, port, 8000, delegate(TcpLink l, byte[] f)
		{
			client.OnFrame(f);
		}, delegate
		{
			if (client.CloseReason == null)
			{
				client.CloseReason = "Lost the connection to the host.";
			}
		}, delegate(TcpLink link, string err)
		{
			_connectError = err;
			_connectedLink = link;
			_connectDone = true;
		});
	}

	public static void JoinLan(LanSearch.Game g)
	{
		JoinAddress((g.Port == Proto.DefaultPort) ? g.Host : (g.Host + ":" + g.Port));
	}

	public static void JoinSteam(ulong hostId, string name, byte[] rememberedKey = null, string rememberedRoom = null)
	{
		if (!CanStart())
		{
			return;
		}
		Reset();
		SteamCarrier.Start();
		if (!SteamCarrier.Started)
		{
			Fail("Steam isn't running.");
			return;
		}
		_steamHost = hostId;
		Target = name ?? ("Steam " + hostId);
		_rememberedKey = rememberedKey;
		_rememberedRoom = rememberedRoom;
		RelayClient client = new RelayClient();
		SteamCarrier.SteamLink steamLink = SteamCarrier.Connect(hostId);
		steamLink.OnFrame = delegate(SteamCarrier.SteamLink l, byte[] f)
		{
			client.OnFrame(f);
		};
		steamLink.OnClosed = delegate
		{
			if (client.CloseReason == null)
			{
				client.CloseReason = "Lost the connection to the host.";
			}
		};
		client.Attach(steamLink);
		// the first message opens the Steam session; the host answers with the room info
		steamLink.Send(new byte[1]);
		Begin(client, "Steam");
		Plugin.Log.LogInfo($"[Join] connecting over Steam to {hostId}...");
	}

	public static void Rejoin()
	{
		byte[] rememberedKey = CoopConfig.FromHex(CoopConfig.LastRoomKey.Value);
		string value = CoopConfig.LastRoomName.Value;
		if (!string.IsNullOrEmpty(CoopConfig.LastSteamHost.Value) && ulong.TryParse(CoopConfig.LastSteamHost.Value, out var result) && SteamAvailability.Available)
		{
			JoinSteam(result, value, rememberedKey, value);
		}
		else if (!string.IsNullOrEmpty(CoopConfig.LastAddress.Value))
		{
			JoinAddress(CoopConfig.LastAddress.Value, rememberedKey, value);
		}
	}

	private static void Begin(RelayClient client, string transport)
	{
		_conn = CoopConnection.ForClient(client, transport);
		CoopConnection.Current = _conn;
		Current = Step.WaitingRoom;
		_timer = 0f;
	}

	public static void SubmitPassword(string password)
	{
		if (Current == Step.NeedPassword && _conn != null)
		{
			_key = Proto.RoomKey(_conn.RoomName, password ?? "");
			_conn.Client.Join(_key, CoopConfig.MyName(), StableIdentity.Resolve());
			Current = Step.Joining;
			_timer = 0f;
		}
	}

	public static void Cancel()
	{
		if (_conn != null && CoopRuntime.Session == null)
		{
			_conn.Close();
		}
		if (CoopConnection.Current == _conn && CoopRuntime.Session == null)
		{
			CoopConnection.Current = null;
		}
		Reset();
		Current = Step.Idle;
	}

	private static void Reset()
	{
		_conn = null;
		_address = "";
		_steamHost = 0uL;
		_key = null;
		_rememberedKey = null;
		_rememberedRoom = null;
		_connectedLink = null;
		_connectError = null;
		_connectDone = false;
		_pendingClient = null;
		Error = "";
		Target = "";
	}

	private static void Fail(string why)
	{
		Plugin.Log.LogWarning("[Join] " + why);
		if (_conn != null && CoopRuntime.Session == null)
		{
			_conn.Close();
			if (CoopConnection.Current == _conn)
			{
				CoopConnection.Current = null;
			}
		}
		_conn = null;
		Error = why;
		Current = Step.Failed;
	}

	public static void Tick(float dt)
	{
		if (Current == Step.Idle || Current == Step.Failed)
		{
			return;
		}
		_timer += dt;
		if (Current == Step.Connecting)
		{
			if (!_connectDone)
			{
				return;
			}
			if (_connectedLink == null)
			{
				Fail("Couldn't reach " + Target + " (" + _connectError + "). Check the code, and that the host's game is running and hosting.");
				return;
			}
			_pendingClient.Attach(_connectedLink);
			Begin(_pendingClient, "TCP");
		}
		if (_conn == null)
		{
			return;
		}
		if (CoopRuntime.Session == null)
		{
			_conn.Tick(dt);
		}
		if (_conn.Closed || !_conn.Alive)
		{
			Fail(_conn.RejectText ?? _conn.DisconnectText ?? "Lost the connection to the host.");
			return;
		}
		switch (Current)
		{
		case Step.WaitingRoom:
			if (_conn.GotRoomInfo)
			{
				if (_rememberedKey != null && string.Equals(_rememberedRoom, _conn.RoomName, StringComparison.OrdinalIgnoreCase))
				{
					_key = _rememberedKey;
				}
				else if (!_conn.RoomLocked)
				{
					_key = Proto.RoomKey(_conn.RoomName, "");
				}
				if (_key == null)
				{
					Current = Step.NeedPassword;
					break;
				}
				_conn.Client.Join(_key, CoopConfig.MyName(), StableIdentity.Resolve());
				Current = Step.Joining;
				_timer = 0f;
			}
			else if (_timer > 12f)
			{
				Fail("No answer from " + Target + " - is that a Darkwood Co-op game?");
			}
			break;
		case Step.Joining:
			if (_conn.Rejected.HasValue)
			{
				if (_conn.Rejected.Value == Proto.Reject.BadPassword || _conn.Rejected.Value == Proto.Reject.SlowDown)
				{
					Error = _conn.RejectText;
					Current = Step.NeedPassword;
				}
				else
				{
					Fail(_conn.RejectText);
				}
			}
			else if (_conn.Joined)
			{
				Remember();
				Current = Step.WaitingHost;
				_timer = 0f;
			}
			else if (_timer > 15f)
			{
				Fail("The host didn't answer.");
			}
			break;
		case Step.WaitingHost:
			if (_conn.HostStatus == Proto.HostStatus.Prologue)
			{
				Plugin.Log.LogInfo("[Join] the host is in the prologue - starting a new game here, in step with them.");
				Current = Step.Loading;
				PrologueJoinSync.ChooseSlotForNewGame();
			}
			else if (_conn.HostStatus == Proto.HostStatus.Chapter)
			{
				_conn.RequestSave();
				Current = Step.Downloading;
				_timer = 0f;
			}
			break;
		case Step.Downloading:
			if (_conn.SaveComplete)
			{
				ApplySave(_conn.SaveBlob);
			}
			else if (_timer > 120f && _conn.SaveHave == 0)
			{
				Fail("The host never sent their world.");
			}
			break;
		case Step.Loading:
			if (LanSaveTransfer.IsFailed)
			{
				Fail(LanSaveTransfer.StatusText);
			}
			else if (_timer > 240f)
			{
				Fail("The world took too long to load.");
			}
			break;
		case Step.NeedPassword:
			break;
		}
	}

	private static void Remember()
	{
		CoopConfig.LastRoomName.Value = _conn.RoomName;
		CoopConfig.LastRoomKey.Value = CoopConfig.ToHex(_key);
		if (_steamHost != 0)
		{
			CoopConfig.LastSteamHost.Value = _steamHost.ToString();
			CoopConfig.LastAddress.Value = "";
		}
		else
		{
			CoopConfig.LastAddress.Value = _address;
			CoopConfig.LastSteamHost.Value = "";
		}
	}

	private static void ApplySave(byte[] payload)
	{
		string text;
		byte[] blob;
		try
		{
			using MemoryStream memoryStream = new MemoryStream(payload);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			int count = binaryReader.ReadInt32();
			text = Encoding.UTF8.GetString(binaryReader.ReadBytes(count));
			blob = binaryReader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));
		}
		catch (Exception ex)
		{
			Fail("The host's world arrived damaged (" + ex.Message + ").");
			return;
		}
		Plugin.Log.LogInfo($"[Join] got the host's world ({payload.Length} bytes) - writing and loading.");
		Current = Step.Loading;
		LanSaveTransfer.ApplyAndLoadForJoin(blob, text);
	}

	// The world finished loading (LanSaveTransfer / PrologueJoinSync call this): start playing
	public static bool OnWorldLoaded()
	{
		if (_conn == null || !_conn.Alive)
		{
			Fail("The connection was lost while the world loaded.");
			return false;
		}
		CoopSession coopSession = new CoopSession(_conn, NetRole.Client);
		Plugin.Instance.AdoptSession(coopSession);
		LobbySync.Begin(isHost: false);
		coopSession.Start();
		if (!string.IsNullOrEmpty(_conn.Welcome))
		{
			CoopNotice.Show(_conn.Welcome.Replace("|", "  "), 10f);
		}
		CoopNotice.Show($"Joined \"{_conn.RoomName}\" ({coopSession.PlayersInGame}/{Proto.MaxPlayers} players)");
		Reset();
		Current = Step.Idle;
		return true;
	}

	public static void Dismiss()
	{
		if (Current == Step.Failed)
		{
			Current = Step.Idle;
			Error = "";
		}
	}
}
