using System;

namespace DarkwoodCoopOnline.Net;

internal static class SteamAvailability
{
	private const string SteamworksAssembly = "com.rlabrecque.steamworks.net";

	private const string SteamworksProbeType = "Steamworks.SteamFriends, com.rlabrecque.steamworks.net";

	private const string SteamManagerProbeType = "SteamManager, Assembly-CSharp";

	private static bool _checked;

	private static bool _available;

	internal static string UnavailableReason { get; private set; } = "";


	internal static bool Available
	{
		get
		{
			if (!_checked)
			{
				Probe();
			}
			return _available;
		}
	}

	internal static void LogOnce()
	{
		if (!_checked)
		{
			Probe();
		}
		if (_available)
		{
			Plugin.Log.LogInfo("[SteamAvailability] Steam disponível nesta build - transporte Steam, convite por amigo e busca de save P2P habilitados.");
		}
		else
		{
			Plugin.Log.LogWarning("[SteamAvailability] Steam NÃO disponível nesta build (" + UnavailableReason + "). Provavelmente Epic Games ou GOG. O mod segue funcionando normalmente por LAN/Loopback (Radmin/Hamachi/ZeroTier/rede local); só o convite via amigo da Steam fica indisponível.");
		}
	}

	private static void Probe()
	{
		_checked = true;
		try
		{
			if (Type.GetType("Steamworks.SteamFriends, com.rlabrecque.steamworks.net", throwOnError: false) == null)
			{
				_available = false;
				UnavailableReason = "com.rlabrecque.steamworks.net.dll ausente em Darkwood_Data\\Managed";
			}
			else if (Type.GetType("SteamManager, Assembly-CSharp", throwOnError: false) == null)
			{
				_available = false;
				UnavailableReason = "SteamManager ausente no Assembly-CSharp desta build";
			}
			else
			{
				_available = true;
				UnavailableReason = "";
			}
		}
		catch (Exception ex)
		{
			_available = false;
			UnavailableReason = ex.GetType().Name + ": " + ex.Message;
		}
	}
}
