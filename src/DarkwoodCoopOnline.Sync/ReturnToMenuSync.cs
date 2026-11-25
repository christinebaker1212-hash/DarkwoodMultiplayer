using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class ReturnToMenuSync
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Core), "returnToMainMenu", (Type[])null, (Type[])null), new HarmonyMethod(typeof(ReturnToMenuSync), "Before", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Before()
	{
		MainMenuNativeButtons.Reset();
		if (CoopRuntime.Session != null || CoopConnection.Current != null)
		{
			Plugin.Log.LogInfo("[Session] back to the main menu - leaving the co-op game.");
			if (CoopRuntime.SessionActive)
			{
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.PeerLeaving).Finish());
			}
			Plugin.Instance?.StopSessionExternal();
		}
	}
}
