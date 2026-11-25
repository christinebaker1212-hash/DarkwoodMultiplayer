using System;
using System.Reflection;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class JournalPauseSuppress
{
	private static bool _patched;

	private static bool _suppress;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Expected O, but got Unknown
		//IL_0093: Expected O, but got Unknown
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected O, but got Unknown
		//IL_00dc: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Core), "pause", (Type[])null, (Type[])null), new HarmonyMethod(typeof(JournalPauseSuppress), "PausePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Journal), "open", (Type[])null, (Type[])null), new HarmonyMethod(typeof(JournalPauseSuppress), "SuppressOnPrefix", (Type[])null), new HarmonyMethod(typeof(JournalPauseSuppress), "SuppressOffPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Journal), "showNote", (Type[])null, (Type[])null), new HarmonyMethod(typeof(JournalPauseSuppress), "SuppressOnPrefix", (Type[])null), new HarmonyMethod(typeof(JournalPauseSuppress), "SuppressOffPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
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
