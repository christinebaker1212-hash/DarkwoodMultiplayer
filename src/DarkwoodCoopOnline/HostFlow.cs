using System;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

// HOST A GAME: once the host's world is loaded, start the relay server (friends join with the invite
// code, the LAN list, a VPN address or Steam), open the router and show the invite code.
public static class HostFlow
{
	private static PortMapper.State _lastPortState;

	private static bool _toldVpn;

	public static bool Hosting
	{
		get
		{
			CoopConnection current = CoopConnection.Current;
			if (current != null && current.IsHost)
			{
				return current.Alive;
			}
			return false;
		}
	}

	// One line for the lobby / status: how friends get in
	private static float _hostingSince;

	private static string _announcedCode = "";

	// The code friends type in JOIN. Like Moemon Kai Co-op: the router is opened and the code carries the
	// home's address. If the router can't be opened, the code carries the host's Steam account instead
	// and friends connect through Steam's relay - it works behind any router, nothing to set up.
	public static string Code
	{
		get
		{
			if (!Hosting)
			{
				return "";
			}
			if (PortMapper.Status == PortMapper.State.Open && !string.IsNullOrEmpty(PortMapper.Code))
			{
				return PortMapper.Code;
			}
			bool flag = PortMapper.Status == PortMapper.State.Failed || PortMapper.Status == PortMapper.State.Idle || Time.unscaledTime - _hostingSince > 15f;
			if (flag && SteamCode != null)
			{
				return SteamCode;
			}
			return "";
		}
	}

	public static bool CodeIsSteam
	{
		get
		{
			string code = Code;
			if (code.Length > 0)
			{
				return code == SteamCode;
			}
			return false;
		}
	}

	private static string _steamCode;

	// The Steam code as a spare when the main code is the router one (e.g. Windows Firewall blocks it)
	public static string BackupSteamCode
	{
		get
		{
			if (!Hosting || CodeIsSteam)
			{
				return null;
			}
			return SteamCode;
		}
	}

	private static string SteamCode
	{
		get
		{
			if (_steamCode == null && SteamAvailability.Available && SteamCarrier.Started)
			{
				try
				{
					_steamCode = InviteCode.MakeSteam(SteamCarrier.MyId());
				}
				catch
				{
					_steamCode = null;
				}
			}
			return _steamCode;
		}
	}

	// One line for the lobby / status: how friends get in
	public static string InviteLine
	{
		get
		{
			if (!Hosting)
			{
				return "";
			}
			string code = Code;
			if (code.Length > 0)
			{
				return "Invite code: " + code;
			}
			if (PortMapper.Status == PortMapper.State.Working)
			{
				return "Making your invite code...";
			}
			// no router and no Steam (a non-Steam copy of the game): addresses friends can still use
			RefreshAddresses();
			if (_lanAddress != null)
			{
				return "Same network: " + _lanAddress + PortSuffix() + ((_vpnAddress != null) ? ("  ·  " + _vpnName + ": " + _vpnAddress + PortSuffix()) : "");
			}
			return (_vpnAddress != null) ? (_vpnName + ": " + _vpnAddress + PortSuffix()) : "";
		}
	}

	// network adapters are slow to list: look them up every few seconds, not every frame
	private static float _addressesAt = float.MinValue;

	private static string _vpnAddress;

	private static string _vpnName;

	private static string _lanAddress;

	private static void RefreshAddresses()
	{
		if (!(Time.unscaledTime - _addressesAt < 5f))
		{
			_addressesAt = Time.unscaledTime;
			_vpnAddress = (PortMapper.FindVpnAddress(out var address, out var name) ? address : null);
			_vpnName = name;
			_lanAddress = PortMapper.LanAddress();
		}
	}

	private static string PortSuffix()
	{
		int num = CoopConnection.Current?.Server?.Port ?? Proto.DefaultPort;
		if (num != Proto.DefaultPort)
		{
			return ":" + num;
		}
		return "";
	}

	// Called when the host's world has loaded (after picking a save from HOST A GAME)
	// worldJustLoaded: called right as the world finished loading (its last step, WorldGenerator.activatePlayer,
	// may still be ahead; saves are served once it ran). From F7 mid-game the world is long ready.
	public static bool StartHosting(bool worldJustLoaded = false)
	{
		if (CoopConnection.Current != null)
		{
			Plugin.Log.LogWarning("[Host] already online.");
			return true;
		}
		string text = CoopConfig.MyName();
		string text2 = StableIdentity.Resolve();
		RelayServer relayServer = new RelayServer
		{
			Welcome = CoopConfig.Clip(CoopConfig.Welcome.Value, 120)
		};
		if (!relayServer.Start(CoopConfig.DefaultRoomName(), CoopConfig.RoomPassword.Value, text, text2, out var error))
		{
			CoopNotice.Show("Couldn't start hosting: " + error, 12f);
			Plugin.Log.LogError("[Host] " + error);
			return false;
		}
		relayServer.HostStatus = (((Object)(object)Player.Instance != (Object)null && Player.Instance.firstPlay) ? Proto.HostStatus.Prologue : Proto.HostStatus.Chapter);
		CoopConnection coopConnection = (CoopConnection.Current = CoopConnection.ForHost(relayServer, text, text2, "host"));
		if (SteamAvailability.Available)
		{
			try
			{
				StartSteamHosting(relayServer);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[Host] Steam joining unavailable: " + ex.Message);
			}
		}
		_lastPortState = PortMapper.State.Idle;
		_toldVpn = false;
		_hostingSince = Time.unscaledTime;
		_announcedCode = "";
		if (CoopConfig.OpenRouter.Value)
		{
			PortMapper.Open(relayServer.Port);
		}
		if (worldJustLoaded)
		{
			LanSaveTransfer.MarkHostWorldNotReady();
		}
		CoopSession session = new CoopSession(coopConnection, NetRole.Host);
		Plugin.Instance.AdoptSession(session);
		LobbySync.Begin(isHost: true);
		session.Start();
		CoopNotice.Show($"Hosting \"{relayServer.RoomName}\" - up to {Proto.MaxPlayers} players", 8f);
		return true;
	}

	private static void StartSteamHosting(RelayServer server)
	{
		SteamCarrier.Start();
		if (!SteamCarrier.Started)
		{
			return;
		}
		SteamCarrier.AcceptIncoming = delegate(ulong id)
		{
			SteamCarrier.SteamLink steamLink = new SteamCarrier.SteamLink(id);
			RelayServer.Member member = server.AddLink(steamLink);
			steamLink.OnFrame = delegate(SteamCarrier.SteamLink l, byte[] f)
			{
				member.LastHeard = DateTime.UtcNow;
				server.Handle(member, f);
			};
			steamLink.OnClosed = delegate
			{
				server.Drop(member);
			};
			return steamLink;
		};
		SteamCarrier.SetHosting(hosting: true);
	}

	public static void Tick()
	{
		if (!Hosting)
		{
			return;
		}
		// what a joiner gets: the prologue (start alongside), the world (download it), or "wait" mid-load
		RelayServer server = CoopConnection.Current.Server;
		if ((Object)(object)Player.Instance == (Object)null || !Core.worldGenFinished())
		{
			server.HostStatus = Proto.HostStatus.Loading;
		}
		else
		{
			server.HostStatus = (Player.Instance.firstPlay ? Proto.HostStatus.Prologue : Proto.HostStatus.Chapter);
		}
		string code = Code;
		if (code.Length > 0 && code != _announcedCode)
		{
			_announcedCode = code;
			try
			{
				GUIUtility.systemCopyBuffer = code;
			}
			catch
			{
			}
			CoopNotice.Show("Invite code " + code + " copied - paste it to your friends!", 15f);
			Plugin.Log.LogInfo("[Host] invite code " + code + (CodeIsSteam ? " (through Steam: the router couldn't be opened)" : (" (router opened, " + PortMapper.PublicAddress + ":" + PortMapper.ExternalPort + ")")));
		}
		PortMapper.State status = PortMapper.Status;
		if (status == _lastPortState)
		{
			return;
		}
		_lastPortState = status;
		if (status == PortMapper.State.Failed && SteamCode == null && !_toldVpn)
		{
			// only for copies of the game without Steam: Steam players always get a working code
			_toldVpn = true;
			string text = ((PortMapper.LastError == PortMapper.Error.Cgnat) ? "Your internet provider shares your address (CGNAT)" : "Your router didn't open the port (UPnP off?)");
			CoopNotice.Show(text + ", so friends outside your home can't reach you without Steam. Friends at home: JOIN > Games on your network.", 20f);
		}
	}

	public static void OnPlayerCountChanged()
	{
		if (SteamAvailability.Available && SteamCarrier.Started)
		{
			try
			{
				SteamCarrier.SetHosting(Hosting && (CoopConnection.Current?.PlayerCount ?? 0) < Proto.MaxPlayers);
			}
			catch
			{
			}
		}
	}

	public static void Stop()
	{
		PortMapper.Close();
		if (SteamAvailability.Available && SteamCarrier.Started)
		{
			try
			{
				SteamCarrier.SetHosting(hosting: false);
			}
			catch
			{
			}
			SteamCarrier.StopAll();
		}
	}
}
