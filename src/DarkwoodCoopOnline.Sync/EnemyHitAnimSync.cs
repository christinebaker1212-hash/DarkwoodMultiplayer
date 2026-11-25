using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class EnemyHitAnimSync
{
	private static bool _patched;

	private static List<(string id, string clip)> _pending;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(Character), "getHit", new Type[9]
			{
				typeof(float),
				typeof(Transform),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, (Type[])null);
			val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(EnemyHitAnimSync), "Prefix", (Type[])null), new HarmonyMethod(typeof(EnemyHitAnimSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Prefix(Character __instance, out float __state)
	{
		__state = ((CharBase)__instance).health;
	}

	private static void Postfix(Character __instance, float __state)
	{
		if ((Object)(object)__instance == (Object)null || __state - ((CharBase)__instance).health <= 0.01f || string.IsNullOrEmpty(__instance.currentHitAnim))
		{
			return;
		}
		string text = EnemySync.FindOwnedId(__instance);
		if (!string.IsNullOrEmpty(text))
		{
			Plugin.Log.LogInfo("[EnemyHitAnimSync] '" + ((Object)((Component)__instance).gameObject).name + "' (id=" + text + ") levou dano de verdade " + $"({__state:F1}->{((CharBase)__instance).health:F1}) - mandando reação '{__instance.currentHitAnim}' pro peer.");
			if (_pending == null)
			{
				_pending = new List<(string, string)>();
			}
			_pending.Add((text, __instance.currentHitAnim));
		}
	}

	public static byte[] PollOutgoing()
	{
		if (_pending == null || _pending.Count == 0)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemyHitAnim).UShort((ushort)_pending.Count);
		foreach (var (v, v2) in _pending)
		{
			packetWriter.Str(v).Str(v2);
		}
		_pending = null;
		return packetWriter.Finish();
	}

	public static void ApplyIncoming(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			string text2 = r.Str();
			if (!string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(text2))
			{
				EnemySync.PlayHitReactionOnPuppet(text, text2);
			}
		}
	}

	public static void Reset()
	{
		_pending = null;
	}
}
