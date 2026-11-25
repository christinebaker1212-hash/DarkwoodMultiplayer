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
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Core), "returnToMainMenu", (Type[])null, (Type[])null), new HarmonyMethod(typeof(ReturnToMenuSync), "Before", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Before()
	{
		MainMenuNativeButtons.Reset();
		if (CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo("[ReturnToMenuSync] voltando pro menu principal - avisando o peer antes de encerrar a sessão.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.PeerLeaving).Finish());
			Plugin.Instance?.StopSessionExternal();
		}
	}
}
