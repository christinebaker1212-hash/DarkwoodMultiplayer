using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class WildlifeRespawnSync
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(WorldGenerator), "respawnAllEnemies", (Type[])null, (Type[])null), new HarmonyMethod(typeof(WildlifeRespawnSync), "SkipIfClient", (Type[])null), new HarmonyMethod(typeof(WildlifeRespawnSync), "AfterRespawn", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CharacterSpawner), "despawnCharacters", (Type[])null, (Type[])null), new HarmonyMethod(typeof(WildlifeRespawnSync), "BeforeDespawnCharacters", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CharacterSpawner), "despawnNocturnalCharacters", (Type[])null, (Type[])null), new HarmonyMethod(typeof(WildlifeRespawnSync), "BeforeDespawnNocturnal", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool SkipIfClient()
	{
		if (!CoopRuntime.SessionActive)
		{
			return true;
		}
		return CoopRuntime.Session.Role != NetRole.Client;
	}

	private static bool BeforeDespawnCharacters(CharacterSpawner __instance)
	{
		bool num = SkipIfClient();
		if (num)
		{
			AnnounceOwnedRemovals(__instance.spawnedCharacters);
		}
		return num;
	}

	private static void BeforeDespawnNocturnal(CharacterSpawner __instance)
	{
		AnnounceOwnedRemovals(__instance.nocturnalCharacters);
	}

	private static void AnnounceOwnedRemovals(List<GameObject> list)
	{
		if (!CoopRuntime.SessionActive || list == null)
		{
			return;
		}
		foreach (GameObject item in list)
		{
			if (!((Object)(object)item == (Object)null))
			{
				Character component = item.GetComponent<Character>();
				if ((Object)(object)component != (Object)null)
				{
					EnemySync.RegisterForcedRemoval(component);
				}
			}
		}
	}

	private static void AfterRespawn()
	{
		if (CoopRuntime.SessionActive && CoopRuntime.Session.Role == NetRole.Host)
		{
			SceneCache.ForceRescanCharacters();
			Plugin.Log.LogInfo("[WildlifeRespawnSync] respawn de vida selvagem pós-morte rodou aqui (HOST) - forcei rescan pro peer receber logo.");
		}
	}
}
