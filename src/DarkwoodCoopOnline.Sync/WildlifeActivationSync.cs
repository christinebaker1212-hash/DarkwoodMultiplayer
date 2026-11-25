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
