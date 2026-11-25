using System;
using System.Reflection;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class UiPauseSuppress
{
	private static bool _patched;

	private static int _depth;

	private static bool Suppressing => _depth > 0;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Expected O, but got Unknown
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Expected O, but got Unknown
		//IL_00b1: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Core), "pause", (Type[])null, (Type[])null), new HarmonyMethod(typeof(UiPauseSuppress), "PausePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Hook(val, typeof(Padlock), "activate", "deactivate");
			Hook(val, typeof(InteractiveItem), "open", "close");
			Hook(val, typeof(SkillPointsMenu), "open", "close");
			Hook(val, typeof(SkillSlotsMenu), "open", "close");
		}
	}

	private static void Hook(Harmony harmony, Type type, string openName, string closeName)
	{
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Expected O, but got Unknown
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Expected O, but got Unknown
		try
		{
			MethodInfo methodInfo = AccessTools.Method(type, openName, (Type[])null, (Type[])null);
			MethodInfo methodInfo2 = AccessTools.Method(type, closeName, (Type[])null, (Type[])null);
			if (methodInfo == null || methodInfo2 == null)
			{
				Plugin.Log.LogWarning("[UiPauseSuppress] não achei " + type.Name + "." + openName + "/" + closeName + " - essa UI vai continuar pausando o peer.");
			}
			else
			{
				harmony.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(UiPauseSuppress), "SuppressOnPrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
				harmony.Patch((MethodBase)methodInfo2, (HarmonyMethod)null, new HarmonyMethod(typeof(UiPauseSuppress), "SuppressOffPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[UiPauseSuppress] falha ao patchar " + type.Name + ": " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static bool PausePrefix()
	{
		return !Suppressing;
	}

	private static void SuppressOnPrefix()
	{
		_depth++;
	}

	private static void SuppressOffPostfix()
	{
		if (_depth > 0)
		{
			_depth--;
		}
	}

	public static void Reset()
	{
		_depth = 0;
	}
}
