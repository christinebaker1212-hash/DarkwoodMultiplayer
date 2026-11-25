using System;
using System.Reflection;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class EscMenuPauseSuppress
{
	private static bool _patched;

	private static bool _suppress;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Core), "pause", (Type[])null, (Type[])null), new HarmonyMethod(typeof(EscMenuPauseSuppress), "PausePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(MainMenu), "OnEnable", (Type[])null, (Type[])null), new HarmonyMethod(typeof(EscMenuPauseSuppress), "OnEnablePrefix", (Type[])null), new HarmonyMethod(typeof(EscMenuPauseSuppress), "OnEnablePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool PausePrefix()
	{
		return !_suppress;
	}

	private static void OnEnablePrefix()
	{
		_suppress = true;
	}

	private static void OnEnablePostfix()
	{
		_suppress = false;
	}
}
