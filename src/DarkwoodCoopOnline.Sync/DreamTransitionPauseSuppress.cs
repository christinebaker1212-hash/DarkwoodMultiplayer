using System;
using System.Reflection;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class DreamTransitionPauseSuppress
{
	private static bool _patched;

	private static bool _suppress;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Core), "pause", (Type[])null, (Type[])null), new HarmonyMethod(typeof(DreamTransitionPauseSuppress), "PausePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(DreamTransition), "onFadedIn", (Type[])null, (Type[])null), new HarmonyMethod(typeof(DreamTransitionPauseSuppress), "SuppressOnPrefix", (Type[])null), new HarmonyMethod(typeof(DreamTransitionPauseSuppress), "SuppressOffPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool PausePrefix()
	{
		return !_suppress;
	}

	private static void SuppressOnPrefix()
	{
		_suppress = true;
	}

	private static void SuppressOffPostfix()
	{
		_suppress = false;
	}
}
