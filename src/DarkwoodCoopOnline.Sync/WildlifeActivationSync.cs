using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class WildlifeActivationSync
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(WorldGrid.Cullable), "show", (Type[])null, (Type[])null);
			val.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(WildlifeActivationSync), "AfterShow", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterShow(WorldGrid.Cullable __instance)
	{
		//IL_000c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Invalid comparison between Unknown and I4
		if (CoopRuntime.SessionActive && __instance != null && (int)__instance.type == 200 && !((Object)(object)__instance.GO == (Object)null))
		{
			Character component = __instance.GO.GetComponent<Character>();
			if (!((Object)(object)component == (Object)null))
			{
				EnemySync.RegisterQuestSpawn(component);
			}
		}
	}
}
