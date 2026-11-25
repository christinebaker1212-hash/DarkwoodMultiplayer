using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class MorningRitualSync
{
	private enum NpcKind : byte
	{
		Wolf,
		Trader,
		Porter
	}

	private static bool _patched;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Expected O, but got Unknown
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00de: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected O, but got Unknown
		//IL_0113: Unknown result type (might be due to invalid IL or missing references)
		//IL_0120: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Location), "spawnWolf", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MorningRitualSync), "AfterSpawn", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Location), "spawnTrader", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MorningRitualSync), "AfterSpawn", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Location), "despawnWolf", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MorningRitualSync), "AfterDespawnWolf", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Location), "despawnTrader", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MorningRitualSync), "AfterDespawnTrader", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Location), "despawnPorter", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MorningRitualSync), "AfterDespawnPorter", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterDespawnWolf(Location __instance)
	{
		BroadcastDespawn(__instance, NpcKind.Wolf);
	}

	private static void AfterDespawnTrader(Location __instance)
	{
		BroadcastDespawn(__instance, NpcKind.Trader);
	}

	private static void AfterDespawnPorter(Location __instance)
	{
		BroadcastDespawn(__instance, NpcKind.Porter);
	}

	private static void BroadcastDespawn(Location loc, NpcKind kind)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive && !((Object)(object)loc == (Object)null))
		{
			CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.NpcDespawnedByLocation).Str(((Object)((Component)loc).gameObject).name).Byte((byte)kind)
				.Finish());
			Plugin.Log.LogInfo($"[MorningRitualSync] NPC despawnado localmente (local='{((Object)((Component)loc).gameObject).name}' tipo={kind}) - avisando o peer.");
		}
	}

	public static void ApplyDespawn(PacketReader r)
	{
		string text = r.Str();
		NpcKind npcKind = (NpcKind)r.Byte();
		Location val = null;
		Location[] array = Object.FindObjectsOfType<Location>();
		foreach (Location val2 in array)
		{
			if ((Object)(object)val2 != (Object)null && ((Object)((Component)val2).gameObject).name == text)
			{
				val = val2;
				break;
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogWarning("[MorningRitualSync] peer avisou despawn de NPC em local '" + text + "' - não achei esse local aqui.");
			return;
		}
		_applyingRemote = true;
		try
		{
			switch (npcKind)
			{
			case NpcKind.Wolf:
				val.despawnWolf();
				break;
			case NpcKind.Trader:
				val.despawnTrader();
				break;
			case NpcKind.Porter:
				val.despawnPorter();
				break;
			}
			Plugin.Log.LogInfo($"[MorningRitualSync] despawn de NPC replicado aqui (local='{text}' tipo={npcKind}).");
		}
		finally
		{
			_applyingRemote = false;
		}
	}

	private static void AfterSpawn(NPC __result)
	{
		if (!((Object)(object)__result == (Object)null))
		{
			Character component = ((Component)__result).GetComponent<Character>();
			if (!((Object)(object)component == (Object)null))
			{
				EnemySync.RegisterQuestSpawn(component);
			}
		}
	}

	public static void CatchUpExistingNpcs()
	{
		NPC[] array = SceneCache.Npcs();
		foreach (NPC val in array)
		{
			if (!((Object)(object)val == (Object)null))
			{
				Character component = ((Component)val).GetComponent<Character>();
				if (!((Object)(object)component == (Object)null) && ((CharBase)component).alive)
				{
					EnemySync.RegisterQuestSpawn(component);
				}
			}
		}
	}
}
