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
	public static string InviteLine
	{
		get
		{
			if (!Hosting)
			{
				return "";
			}
			if (PortMapper.Status == PortMapper.State.Open && !string.IsNullOrEmpty(PortMapper.Code))
			{
				return "Invite code: " + PortMapper.Code;
			}
			if (PortMapper.Status == PortMapper.State.Working)
			{
				return "Opening your router for an invite code...";
			}
			if (PortMapper.FindVpnAddress(out var address, out var name))
			{
				return name + " address: " + address + PortSuffix();
			}
			string text = PortMapper.LanAddress();
			if (text != null)
			{
				return "Same network: " + text + PortSuffix() + (SteamAvailability.Available ? "  ·  Steam friends: JOIN > your name" : "");
			}
			return SteamAvailability.Available ? "Steam friends: JOIN > your name" : "";
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
	public static bool StartHosting()
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
		if (CoopConfig.OpenRouter.Value)
		{
			PortMapper.Open(relayServer.Port);
		}
		LanSaveTransfer.MarkHostWorldNotReady();
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
		PortMapper.State status = PortMapper.Status;
		if (status == _lastPortState)
		{
			return;
		}
		_lastPortState = status;
		switch (status)
		{
		case PortMapper.State.Open:
			try
			{
				GUIUtility.systemCopyBuffer = PortMapper.Code;
			}
			catch
			{
			}
			CoopNotice.Show("Invite code " + PortMapper.Code + " copied - paste it to your friends!", 15f);
			break;
		case PortMapper.State.Failed:
		{
			string text = ((PortMapper.LastError == PortMapper.Error.Cgnat) ? "Your internet provider shares your address (CGNAT), so there's no invite code." : ((PortMapper.LastError == PortMapper.Error.Refused) ? "Your router refused to open the port (UPnP off?), so there's no invite code." : "No router answered (UPnP off?), so there's no invite code."));
			if (PortMapper.FindVpnAddress(out var address, out var name) && !_toldVpn)
			{
				_toldVpn = true;
				text = text + " Friends on " + name + " can join with " + address + PortSuffix() + ".";
			}
			else
			{
				text += (SteamAvailability.Available ? " Steam friends can still join (JOIN > your name), or use Radmin VPN." : " Friends can use Radmin VPN (see the README).");
			}
			CoopNotice.Show(text, 20f);
			break;
		}
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
