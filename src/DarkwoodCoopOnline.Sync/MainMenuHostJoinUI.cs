using DarkwoodCoopOnline.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

// The main menu's HOST / JOIN entries (native buttons from MainMenuNativeButtons, or this fallback box)
// open the co-op screens (CoopUI). After HOST A GAME the player picks a save; once that world is loaded
// the game starts hosting (HostFlow).
public static class MainMenuHostJoinUI
{
	private static bool _pendingHost;

	private static GUIStyle _boxStyle;

	private static GUIStyle _titleStyle;

	public static void Tick()
	{
		if (_pendingHost)
		{
			if (Core.mainMenu && IsTopLevelMenuOpen())
			{
				_pendingHost = false;
			}
			else if ((Object)(object)Player.Instance != (Object)null && Core.worldGenFinished())
			{
				_pendingHost = false;
				Plugin.Log.LogInfo("[MainMenuHostJoinUI] world loaded - starting to host.");
				HostFlow.StartHosting(worldJustLoaded: true);
			}
		}
	}

	private static bool IsTopLevelMenuOpen()
	{
		MainMenu instance = Singleton<MainMenu>.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.Menu0 != (Object)null)
		{
			return instance.Menu0.activeSelf;
		}
		return false;
	}

	public static void TriggerHost()
	{
		CoopUI.OpenHost();
	}

	public static void TriggerJoin()
	{
		CoopUI.OpenJoin();
	}

	// HOST A GAME > START HOSTING: pick the save, then host once it's loaded
	public static void BeginHostAfterProfilePick()
	{
		_pendingHost = true;
		Singleton<MainMenu>.Instance.displayProfilesMenu();
	}

	public static void Draw()
	{
		if (Core.mainMenu && IsTopLevelMenuOpen() && !MainMenuNativeButtons.Created && !CoopUI.IsOpen)
		{
			if (_boxStyle == null)
			{
				_boxStyle = CoopUI.BoxStyle(0.55f);
				_titleStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 14,
					fontStyle = (FontStyle)1,
					alignment = (TextAnchor)4
				};
				_titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.85f);
			}
			float num = 220f;
			float num2 = (float)Screen.width - num - 16f;
			float num3 = 16f;
			GUI.Box(new Rect(num2, num3, num, 96f), GUIContent.none, _boxStyle);
			GUI.Label(new Rect(num2, num3 + 4f, num, 20f), $"Darkwood Co-op ({Proto.MaxPlayers} players)", _titleStyle);
			if (GUI.Button(new Rect(num2 + 10f, num3 + 28f, num - 20f, 26f), "HOST A GAME"))
			{
				TriggerHost();
			}
			if (GUI.Button(new Rect(num2 + 10f, num3 + 60f, num - 20f, 26f), "JOIN A GAME"))
			{
				TriggerJoin();
			}
		}
	}

	public static void Reset()
	{
		_pendingHost = false;
	}
}
