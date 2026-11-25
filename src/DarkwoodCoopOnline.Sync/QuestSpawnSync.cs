using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class QuestSpawnSync
{
	private static bool _patched;

	private static readonly Dictionary<GameEvent, string> _lastFireContext = new Dictionary<GameEvent, string>();

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "fire", new Type[1] { typeof(GameObject) }, (Type[])null), new HarmonyMethod(typeof(QuestSpawnSync), "BeforeFire", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "doActionsAfterSpawn", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(QuestSpawnSync), "AfterSpawn", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void BeforeFire(GameEvent __instance, GameObject thisGO)
	{
		_lastFireContext[__instance] = (((Object)(object)thisGO != (Object)null) ? ((Object)thisGO).name : "");
	}

	private static void AfterSpawn(GameEvent __instance, GameObject trueTarget, Character spawnedCharacter)
	{
		_lastFireContext.TryGetValue(__instance, out var value);
		string eventKey = ((!string.IsNullOrEmpty(value)) ? value : null);
		EnemySync.RegisterQuestSpawn(spawnedCharacter, eventKey);
	}
}
