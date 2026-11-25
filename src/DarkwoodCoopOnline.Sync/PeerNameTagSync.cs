using System.Runtime.CompilerServices;
using DarkwoodCoopOnline.Net;
using Steamworks;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PeerNameTagSync
{
	private static bool _enabled = true;

	private static string _cachedName;

	private static string _cachedForIdentity;

	private static GUIStyle _style;

	public static bool Enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			if (_enabled != value)
			{
				_enabled = value;
				Plugin.Log.LogInfo("[PeerNameTagSync] nome do peer em cima do personagem " + (_enabled ? "LIGADO" : "desligado") + ".");
			}
		}
	}

	public static void Tick()
	{
		if (Input.GetKeyDown((KeyCode)110))
		{
			Enabled = !Enabled;
		}
	}

	public static void Draw(CoopSession session, RemotePlayerPuppet peerPuppet)
	{
		if (!_enabled || session == null || (Object)(object)peerPuppet == (Object)null || !peerPuppet.HasState)
		{
			return;
		}
		Camera val = (((Object)(object)Singleton<CamMain>.Instance != (Object)null) ? ((Component)Singleton<CamMain>.Instance).GetComponent<Camera>() : null);
		if (!((Object)(object)val == (Object)null))
		{
			Vector3 val2 = val.WorldToScreenPoint(((Component)peerPuppet).transform.position + Vector3.up * 1.4f);
			if (!(val2.z <= 0f))
			{
				EnsureStyle();
				string text = (string.IsNullOrEmpty(peerPuppet.PeerName) ? ResolveName(session.PeerIdentity) : peerPuppet.PeerName);
				if (DownedSync.IsPeerDowned(peerPuppet.Slot))
				{
					text += "  (DOWN)";
				}
				float num = 220f;
				float num2 = 24f;
				GUI.Label(new Rect(val2.x - num * 0.5f, (float)Screen.height - val2.y - num2, num, num2), text, _style);
			}
		}
	}

	private static string ResolveName(string identity)
	{
		if (string.IsNullOrEmpty(identity))
		{
			return "Peer";
		}
		if (identity == _cachedForIdentity && !string.IsNullOrEmpty(_cachedName))
		{
			return _cachedName;
		}
		string text = "Peer";
		if (identity.StartsWith("steam:") && SteamAvailability.Available && ulong.TryParse(identity.Substring(6), out var result))
		{
			string text2 = ResolveSteamPersonaName(result);
			if (!string.IsNullOrEmpty(text2) && text2 != "[unknown]")
			{
				text = text2;
			}
		}
		_cachedForIdentity = identity;
		_cachedName = text;
		return text;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string ResolveSteamPersonaName(ulong id64)
	{
		if (!SteamManager.Initialized)
		{
			return null;
		}
		return SteamFriends.GetFriendPersonaName(new CSteamID(id64));
	}

	private static void EnsureStyle()
	{
		if (_style == null)
		{
			GUIStyle val = new GUIStyle
			{
				fontSize = 14,
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			val.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
			_style = val;
		}
	}

	public static void Reset()
	{
		_cachedName = null;
		_cachedForIdentity = null;
	}
}
