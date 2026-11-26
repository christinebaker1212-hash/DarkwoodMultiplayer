using System;
using BepInEx.Configuration;
using DarkwoodCoopOnline.Net;

namespace DarkwoodCoopOnline;

// Co-op settings (BepInEx/config/com.felipe.darkwoodcooponline.cfg). Everything here is also set from
// the HOST / JOIN screens; the last game you joined is remembered so REJOIN needs nothing typed.
public static class CoopConfig
{
	public static ConfigEntry<string> PlayerName;

	public static ConfigEntry<string> RoomName;

	public static ConfigEntry<string> RoomPassword;

	public static ConfigEntry<string> Welcome;

	public static ConfigEntry<bool> OpenRouter;

	public static ConfigEntry<string> LastAddress;

	public static ConfigEntry<string> LastRoomName;

	public static ConfigEntry<string> LastRoomKey;

	public static ConfigEntry<string> LastSteamHost;

	public static ConfigEntry<bool> ShowPing;

	public static ConfigEntry<bool> ShowNames;

	public static ConfigEntry<UnityEngine.KeyCode> ReviveButton;

	public static ConfigEntry<UnityEngine.KeyCode> SkinExportKey;

	public static void Bind(ConfigFile cfg)
	{
		PlayerName = cfg.Bind("Co-op", "PlayerName", "", "Your name above your character (empty: your Steam name, or 'Player')");
		RoomName = cfg.Bind("Co-op", "ServerName", "", "Name of the game you host (HOST A GAME)");
		RoomPassword = cfg.Bind("Co-op", "ServerPassword", "", "Password for the game you host (empty = anyone with the invite code can join)");
		Welcome = cfg.Bind("Co-op", "WelcomeNote", "", "Shown to friends when they join your game");
		OpenRouter = cfg.Bind("Co-op", "OpenRouter", true, "When hosting, ask the router to open the port (UPnP / NAT-PMP) and make an invite code");
		LastAddress = cfg.Bind("Co-op", "LastAddress", "", "Remembered: the invite code or address of the last game you joined");
		LastRoomName = cfg.Bind("Co-op", "LastServerName", "", "Remembered: the name of the last game you joined");
		LastRoomKey = cfg.Bind("Co-op", "LastServerKey", "", "Remembered: the last game's key (a hash of its name and password - the password itself isn't stored)");
		LastSteamHost = cfg.Bind("Co-op", "LastSteamHost", "", "Remembered: the Steam host of the last game you joined over Steam");
		ShowPing = cfg.Bind("Co-op", "ShowPing", true, "Show the co-op info (players, ping, invite code) at the top of the screen while the pause menu is open (or while holding Tab)");
		try
		{
			ReviveButton = cfg.Bind("Co-op", "ReviveControllerButton", (UnityEngine.KeyCode)330, "Controller button to hold next to a downed friend to revive them (besides F and the game's interact button). JoystickButton0 = A (Xbox) / Cross (PlayStation), JoystickButton1 = B / Circle, JoystickButton2 = X / Square, JoystickButton3 = Y / Triangle. None = off.");
		}
		catch (Exception ex)
		{
			// (the setting just isn't there; F and the interact button still revive)
			Plugin.Log?.LogWarning("[Startup] ReviveControllerButton setting unavailable: " + ex.Message);
			ReviveButton = null;
		}
		try
		{
			SkinExportKey = cfg.Bind("Skins", "ExportKey", (UnityEngine.KeyCode)286, "In a loaded world, saves the player character's sprite sheets (every item's animations) to the CoopSkins/export folder in the game folder, for making custom skins. F5 by default; None = off.");
		}
		catch (Exception ex2)
		{
			Plugin.Log?.LogWarning("[Startup] Skins.ExportKey setting unavailable: " + ex2.Message);
			SkinExportKey = null;
		}
		ShowNames = cfg.Bind("Co-op", "ShowNames", true, "Show other players' names above them (also: N in game, or the co-op box in the pause menu)");
	}

	public static string MyName()
	{
		string text = PlayerName?.Value?.Trim();
		if (!string.IsNullOrEmpty(text))
		{
			return Clip(text, 20);
		}
		if (SteamAvailability.Available)
		{
			try
			{
				string text2 = SteamCarrier.MyName();
				if (!string.IsNullOrEmpty(text2))
				{
					return Clip(text2, 20);
				}
			}
			catch
			{
			}
		}
		return "Player";
	}

	public static string DefaultRoomName()
	{
		string text = RoomName?.Value?.Trim();
		if (!string.IsNullOrEmpty(text))
		{
			return Clip(text, Proto.RoomNameMax);
		}
		return Clip(MyName() + "'s game", Proto.RoomNameMax);
	}

	public static string Clip(string s, int max)
	{
		if (s == null)
		{
			return "";
		}
		if (s.Length > max)
		{
			return s.Substring(0, max);
		}
		return s;
	}

	public static string ToHex(byte[] b)
	{
		return BitConverter.ToString(b).Replace("-", "").ToLowerInvariant();
	}

	public static byte[] FromHex(string s)
	{
		if (string.IsNullOrEmpty(s) || s.Length % 2 != 0)
		{
			return null;
		}
		try
		{
			byte[] array = new byte[s.Length / 2];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
			}
			return array;
		}
		catch
		{
			return null;
		}
	}
}
