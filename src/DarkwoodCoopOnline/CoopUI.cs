using System;
using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

// The HOST A GAME / JOIN A GAME screens (main menu) and the small co-op status at the top of the
// screen in game. Laid out like Moemon Emerald Kai Co-op's setup screen.
public static class CoopUI
{
	private enum Panel
	{
		None,
		Host,
		Join
	}

	private static Panel _panel;

	private static Rect _rect;

	private static string _name = "";

	private static string _room = "";

	private static string _password = "";

	private static string _welcome = "";

	private static bool _openRouter = true;

	private static string _code = "";

	private static string _joinPassword = "";

	private static Vector2 _scroll;

	private static List<(ulong id, string name)> _steamFriends = new List<(ulong, string)>();

	private static float _friendsRefreshAt;

	private static GUIStyle _title;

	private static GUIStyle _small;

	private static GUIStyle _error;

	private static GUIStyle _hud;

	private static GUIStyle _hudBg;

	private static GUIStyle _window;

	public static bool IsOpen
	{
		get
		{
			if (_panel == Panel.None && !JoinFlow.Busy)
			{
				return JoinFlow.Current == JoinFlow.Step.Failed;
			}
			return true;
		}
	}

	public static GUIStyle BoxStyle(float alpha)
	{
		Texture2D val = new Texture2D(1, 1);
		val.SetPixel(0, 0, new Color(0.03f, 0.03f, 0.03f, alpha));
		val.Apply();
		GUIStyle val2 = new GUIStyle();
		val2.normal.background = val;
		return val2;
	}

	// The game's own main menu reacts to clicks under our window: hide it while a co-op screen is open
	private static GameObject _hiddenMenu;

	private static void HideNativeMenu()
	{
		try
		{
			MainMenu instance = Singleton<MainMenu>.Instance;
			if ((Object)(object)instance != (Object)null && (Object)(object)instance.Menu0 != (Object)null && instance.Menu0.activeSelf)
			{
				instance.Menu0.SetActive(false);
				_hiddenMenu = instance.Menu0;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[CoopUI] couldn't hide the main menu: " + ex.Message);
		}
	}

	private static void RestoreNativeMenu()
	{
		GameObject hiddenMenu = _hiddenMenu;
		_hiddenMenu = null;
		if ((Object)(object)hiddenMenu != (Object)null && Core.mainMenu)
		{
			hiddenMenu.SetActive(true);
		}
	}

	public static void OpenHost()
	{
		HideNativeMenu();
		_name = CoopConfig.MyName();
		_room = CoopConfig.DefaultRoomName();
		_password = CoopConfig.RoomPassword.Value;
		_welcome = CoopConfig.Welcome.Value;
		_openRouter = CoopConfig.OpenRouter.Value;
		_panel = Panel.Host;
	}

	public static void OpenJoin()
	{
		HideNativeMenu();
		_name = CoopConfig.MyName();
		_code = CoopConfig.LastAddress.Value;
		_joinPassword = "";
		_panel = Panel.Join;
		LanSearch.Start();
		_friendsRefreshAt = 0f;
	}

	public static void Close()
	{
		_panel = Panel.None;
		LanSearch.Stop();
		RestoreNativeMenu();
	}

	private static void SaveName()
	{
		CoopConfig.PlayerName.Value = CoopConfig.Clip((_name ?? "").Trim(), 20);
	}

	private static void EnsureStyles()
	{
		if (_title == null)
		{
			_window = new GUIStyle(GUI.skin.window);
			_window.normal.background = BoxStyle(0.93f).normal.background;
			_window.onNormal.background = _window.normal.background;
			_window.normal.textColor = new Color(0.9f, 0.86f, 0.75f);
			_window.fontSize = 18;
			_window.fontStyle = (FontStyle)1;
			_window.padding = new RectOffset(16, 16, 34, 14);
			_title = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				fontStyle = (FontStyle)1
			};
			_title.normal.textColor = new Color(0.9f, 0.86f, 0.75f);
			_small = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				wordWrap = true
			};
			_small.normal.textColor = new Color(0.72f, 0.72f, 0.68f);
			_error = new GUIStyle(_small)
			{
				fontSize = 13
			};
			_error.normal.textColor = new Color(1f, 0.5f, 0.42f);
			_hud = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				alignment = (TextAnchor)4
			};
			_hud.normal.textColor = new Color(0.92f, 0.9f, 0.82f);
			_hudBg = BoxStyle(0.55f);
		}
	}

	public static void Tick()
	{
		// left the main menu: the screens are done (nothing to restore)
		if (_panel != Panel.None && !Core.mainMenu)
		{
			_panel = Panel.None;
			_hiddenMenu = null;
			LanSearch.Stop();
		}
		// joining reached the save-slot choice / loading: that list lives in the native menu, show it again
		if ((Object)(object)_hiddenMenu != (Object)null && (JoinFlow.Current == JoinFlow.Step.Loading || SaveSlotPickerUI.Active || CoopMenuList.Active))
		{
			Close();
		}
		if (_panel != Panel.None && Input.GetKeyDown((KeyCode)27))
		{
			Close();
		}
		if (_panel == Panel.Join && SteamAvailability.Available && Time.unscaledTime >= _friendsRefreshAt)
		{
			_friendsRefreshAt = Time.unscaledTime + 3f;
			try
			{
				_steamFriends = SteamCarrier.FriendsHosting();
			}
			catch
			{
				_steamFriends = new List<(ulong, string)>();
			}
		}
	}

	public static void Draw()
	{
		EnsureStyles();
		if (JoinFlow.Current == JoinFlow.Step.Loading && (CoopMenuList.Active || SaveSlotPickerUI.Active))
		{
			return;
		}
		if (JoinFlow.Busy || JoinFlow.Current == JoinFlow.Step.Failed)
		{
			_rect = Centered(460f, 220f);
			_rect = GUILayout.Window(918280, _rect, DrawProgress, "JOIN A GAME", _window, Array.Empty<GUILayoutOption>());
		}
		else if (_panel == Panel.Host)
		{
			_rect = Centered(480f, 420f);
			_rect = GUILayout.Window(918281, _rect, DrawHost, "HOST A GAME", _window, Array.Empty<GUILayoutOption>());
		}
		else if (_panel == Panel.Join)
		{
			_rect = Centered(480f, 480f);
			_rect = GUILayout.Window(918282, _rect, DrawJoin, "JOIN A GAME", _window, Array.Empty<GUILayoutOption>());
		}
	}

	private static Rect Centered(float w, float h)
	{
		return new Rect(((float)Screen.width - w) * 0.5f, ((float)Screen.height - h) * 0.5f, w, h);
	}

	private static string Field(string label, string value, int max)
	{
		GUILayout.Label(label, _small, Array.Empty<GUILayoutOption>());
		string text = GUILayout.TextField(value ?? "", max, Array.Empty<GUILayoutOption>());
		GUILayout.Space(4f);
		return text;
	}

	private static void DrawHost(int id)
	{
		_name = Field("Your name", _name, 20);
		_room = Field("Server name", _room, Proto.RoomNameMax);
		_password = Field("Password (optional - leave empty to let anyone with the code in)", _password, 64);
		_welcome = Field("Welcome note (optional, friends see it when they join)", _welcome, 120);
		_openRouter = GUILayout.Toggle(_openRouter, " Open my router automatically and make an invite code", Array.Empty<GUILayoutOption>());
		GUILayout.Space(6f);
		GUILayout.Label($"Up to {Proto.MaxPlayers} players. After START HOSTING pick the save to play; friends can join any time, even after you start.", _small, Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("START HOSTING", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(34f) }))
		{
			SaveName();
			CoopConfig.RoomName.Value = CoopConfig.Clip((_room ?? "").Trim(), Proto.RoomNameMax);
			CoopConfig.RoomPassword.Value = _password ?? "";
			CoopConfig.Welcome.Value = _welcome ?? "";
			CoopConfig.OpenRouter.Value = _openRouter;
			Close();
			MainMenuHostJoinUI.BeginHostAfterProfilePick();
		}
		if (GUILayout.Button("BACK", (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(90f),
			GUILayout.Height(34f)
		}))
		{
			Close();
		}
		GUILayout.EndHorizontal();
		GUI.DragWindow();
	}

	private static void DrawJoin(int id)
	{
		_scroll = GUILayout.BeginScrollView(_scroll, Array.Empty<GUILayoutOption>());
		if (JoinFlow.CanRejoin && GUILayout.Button("REJOIN  " + JoinFlow.RejoinLabel, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(32f) }))
		{
			SaveName();
			JoinFlow.Rejoin();
		}
		GUILayout.Space(6f);
		GUILayout.Label("ENTER INVITE CODE OR IP", _title, Array.Empty<GUILayoutOption>());
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		_code = GUILayout.TextField(_code ?? "", 64, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(26f) });
		bool flag = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
		if (GUILayout.Button("JOIN", (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(80f),
			GUILayout.Height(26f)
		}) || (flag && !string.IsNullOrEmpty(_code)))
		{
			SaveName();
			JoinFlow.JoinAddress(_code);
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("The code your friend got when they started hosting (like K7QM-3XPA). An IP address or ip:port works too.", _small, Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.Label("GAMES ON YOUR NETWORK", _title, Array.Empty<GUILayoutOption>());
		List<LanSearch.Game> games = LanSearch.Games;
		if (games.Count == 0)
		{
			GUILayout.Label("Searching...", _small, Array.Empty<GUILayoutOption>());
		}
		foreach (LanSearch.Game item in games)
		{
			string text = $"{item.Name}   ({item.Players}/{item.Max}){(item.Locked ? "   [password]" : "")}{((item.Version != Proto.Version) ? "   [other version]" : "")}";
			if (GUILayout.Button(text, Array.Empty<GUILayoutOption>()))
			{
				SaveName();
				JoinFlow.JoinLan(item);
			}
		}
		if (SteamAvailability.Available)
		{
			GUILayout.Space(8f);
			GUILayout.Label("STEAM FRIENDS HOSTING", _title, Array.Empty<GUILayoutOption>());
			if (_steamFriends.Count == 0)
			{
				GUILayout.Label("No friends hosting right now.", _small, Array.Empty<GUILayoutOption>());
			}
			foreach (var (hostId, text2) in _steamFriends)
			{
				if (GUILayout.Button(text2, Array.Empty<GUILayoutOption>()))
				{
					SaveName();
					JoinFlow.JoinSteam(hostId, text2);
				}
			}
			if (GUILayout.Button("Open Steam friends list", Array.Empty<GUILayoutOption>()))
			{
				try
				{
					SteamCarrier.OpenFriendsOverlay();
				}
				catch
				{
				}
			}
		}
		GUILayout.Space(8f);
		_name = Field("Your name", _name, 20);
		GUILayout.EndScrollView();
		if (GUILayout.Button("BACK", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(30f) }))
		{
			Close();
		}
		GUI.DragWindow();
	}

	private static void DrawProgress(int id)
	{
		if (!string.IsNullOrEmpty(JoinFlow.RoomName))
		{
			GUILayout.Label(JoinFlow.RoomName, _title, Array.Empty<GUILayoutOption>());
		}
		if (JoinFlow.Current == JoinFlow.Step.Failed)
		{
			GUILayout.Label(JoinFlow.Error, _error, Array.Empty<GUILayoutOption>());
			GUILayout.Space(10f);
			if (GUILayout.Button("OK", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(30f) }))
			{
				JoinFlow.Dismiss();
			}
			GUI.DragWindow();
			return;
		}
		GUILayout.Label(JoinFlow.StatusText, _small, Array.Empty<GUILayoutOption>());
		if (JoinFlow.Current == JoinFlow.Step.NeedPassword)
		{
			if (!string.IsNullOrEmpty(JoinFlow.Error))
			{
				GUILayout.Label(JoinFlow.Error, _error, Array.Empty<GUILayoutOption>());
			}
			GUILayout.Label("Password", _small, Array.Empty<GUILayoutOption>());
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			_joinPassword = GUILayout.PasswordField(_joinPassword ?? "", '*', 64, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(26f) });
			bool flag = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
			if (GUILayout.Button("OK", (GUILayoutOption[])(object)new GUILayoutOption[2]
			{
				GUILayout.Width(70f),
				GUILayout.Height(26f)
			}) || flag)
			{
				JoinFlow.SubmitPassword(_joinPassword);
				_joinPassword = "";
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.Space(10f);
		if (JoinFlow.Current != JoinFlow.Step.Loading && GUILayout.Button("CANCEL", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Height(30f) }))
		{
			JoinFlow.Cancel();
		}
		GUI.DragWindow();
	}

	// The in-game pause (ESC) menu is the game's MainMenu object switched on while in a world
	private static bool _lastPauseOpen;

	public static bool PauseMenuOpen
	{
		get
		{
			bool flag = false;
			try
			{
				MainMenu instance = Singleton<MainMenu>.Instance;
				flag = !Core.mainMenu && (Object)(object)Player.Instance != (Object)null && (Object)(object)instance != (Object)null && ((Behaviour)instance).isActiveAndEnabled;
			}
			catch
			{
			}
			if (flag != _lastPauseOpen)
			{
				_lastPauseOpen = flag;
				Plugin.Log.LogInfo("[Session] pause menu " + (flag ? "opened" : "closed"));
			}
			return flag;
		}
	}

	private static Rect _pauseRect = new Rect(16f, 60f, 300f, 10f);

	// Co-op options in the pause menu (left side): names, invite code, leave
	public static void DrawPauseBox(CoopSession session)
	{
		EnsureStyles();
		_pauseRect = GUILayout.Window(918283, _pauseRect, delegate
		{
			GUILayout.Label($"{session.PlayersInGame}/{Proto.MaxPlayers} players" + ((session.Role == NetRole.Client && session.Net.RttMs >= 0) ? $"  ·  ping {session.Net.RttMs} ms" : ""), _small, Array.Empty<GUILayoutOption>());
			if (GUILayout.Button(PeerNameTagSync.Enabled ? "Player names: ON" : "Player names: OFF", Array.Empty<GUILayoutOption>()))
			{
				PeerNameTagSync.Enabled = !PeerNameTagSync.Enabled;
			}
			if (session.Role == NetRole.Host && !string.IsNullOrEmpty(HostFlow.Code) && GUILayout.Button("Copy invite code " + HostFlow.Code, Array.Empty<GUILayoutOption>()))
			{
				GUIUtility.systemCopyBuffer = HostFlow.Code;
				CoopNotice.Show("Invite code copied");
			}
			if (GUILayout.Button((session.Role == NetRole.Host) ? "Stop hosting" : "Leave the co-op game", Array.Empty<GUILayoutOption>()))
			{
				Plugin.Instance?.StopSessionExternal();
			}
			GUILayout.Label("Tab: co-op info any time  ·  N: names  ·  hold F next to a downed friend to revive", _small, Array.Empty<GUILayoutOption>());
		}, "CO-OP", _window, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(300f) });
	}

	// In game: who's here and how to invite more (top of the screen)
	public static void DrawHud(CoopSession session)
	{
		if (session == null || !CoopConfig.ShowPing.Value)
		{
			return;
		}
		EnsureStyles();
		CoopConnection net = session.Net;
		List<string> list = new List<string>();
		for (int i = 0; i < Proto.MaxPlayers; i++)
		{
			CoopConnection.Peer peer = net.Roster[i];
			if (i == net.MySlot)
			{
				list.Add(CoopConfig.MyName() + " (you)");
			}
			else if (peer != null)
			{
				list.Add(peer.Name + (peer.InGame ? "" : " (loading)"));
			}
		}
		string text = ((session.Role == NetRole.Host) ? "HOST" : "CO-OP") + $"  ·  {session.PlayersInGame}/{Proto.MaxPlayers}  ·  " + string.Join(", ", list.ToArray());
		if (session.Role == NetRole.Client && net.RttMs >= 0)
		{
			text += $"  ·  ping {net.RttMs} ms";
		}
		string inviteLine = HostFlow.InviteLine;
		if (session.Role == NetRole.Host && !string.IsNullOrEmpty(inviteLine) && session.PlayersInGame < Proto.MaxPlayers)
		{
			text = text + "\n" + inviteLine;
		}
		float num = Mathf.Min((float)Screen.width - 40f, 760f);
		float num2 = (text.Contains("\n") ? 42f : 24f);
		Rect rect = new Rect(((float)Screen.width - num) * 0.5f, 4f, num, num2);
		GUI.Box(rect, GUIContent.none, _hudBg);
		GUI.Label(rect, text, _hud);
	}
}
