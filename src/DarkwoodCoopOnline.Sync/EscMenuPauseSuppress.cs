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
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		//IL_0092: Expected O, but got Unknown
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
