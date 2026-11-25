using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class InSightSync
{
	private static bool _patched;

	private static readonly List<(string key, bool inSight)> _pending = new List<(string, bool)>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(InSightOfPlayer), "doInSight", (Type[])null, (Type[])null), new HarmonyMethod(typeof(InSightSync), "BeforeDoInSight", (Type[])null), new HarmonyMethod(typeof(InSightSync), "AfterDoInSight", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(InSightOfPlayer), "doOutOfSight", (Type[])null, (Type[])null), new HarmonyMethod(typeof(InSightSync), "BeforeDoOutOfSight", (Type[])null), new HarmonyMethod(typeof(InSightSync), "AfterDoOutOfSight", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void BeforeDoInSight(InSightOfPlayer __instance, out bool __state)
	{
		__state = __instance.firedInSight;
	}

	private static void AfterDoInSight(InSightOfPlayer __instance, bool __state)
	{
		if (CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null) && !__state && __instance.firedInSight)
		{
			_pending.Add((KeyOf(__instance), true));
		}
	}

	private static void BeforeDoOutOfSight(InSightOfPlayer __instance, out bool __state)
	{
		__state = __instance.firedOutOfSight;
	}

	private static void AfterDoOutOfSight(InSightOfPlayer __instance, bool __state)
	{
		if (CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null) && !__state && __instance.firedOutOfSight)
		{
			_pending.Add((KeyOf(__instance), false));
		}
	}

	public static byte[] PollNew()
	{
		if (_pending.Count == 0)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.InSightTriggered).UShort((ushort)_pending.Count);
		foreach (var (text, flag) in _pending)
		{
			Plugin.Log.LogInfo("[InSightSync] '" + text + "' " + (flag ? "entrou" : "saiu") + " de vista de verdade - avisando o peer.");
			packetWriter.Str(text).Bool(flag);
		}
		_pending.Clear();
		return packetWriter.Finish();
	}

	public static void ApplyRemote(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			bool flag = r.Bool();
			InSightOfPlayer val = FindByKey(text);
			if ((Object)(object)val == (Object)null)
			{
				Plugin.Log.LogWarning($"[InSightSync] não achei '{text}' pra aplicar (inSight={flag}) - ignorando.");
			}
			else if (flag)
			{
				Replay(val.onInSightTriggers, (EventTrigger.Type)500, val.onInSightOfPlayer);
			}
			else
			{
				Replay(val.onOutOfSightTriggers, (EventTrigger.Type)501, val.onOutOfSightOfPlayer);
			}
		}
	}

	private static void Replay(List<EventTriggers> triggers, EventTrigger.Type type, SightDelegate del)
	{
		if (del != null)
		{
			del.Invoke();
		}
		if (triggers == null)
		{
			return;
		}
		foreach (EventTriggers trigger in triggers)
		{
			if ((Object)(object)trigger != (Object)null)
			{
				Core.sendTriggerInfo(((Component)trigger).gameObject, type, false);
			}
		}
	}

	private static InSightOfPlayer FindByKey(string key)
	{
		InSightOfPlayer[] array = SceneCache.InSightObjects();
		foreach (InSightOfPlayer val in array)
		{
			if ((Object)(object)val != (Object)null && KeyOf(val) == key)
			{
				return val;
			}
		}
		return null;
	}

	private static string KeyOf(InSightOfPlayer i)
	{
		return $"{((Object)i).name}@{Mathf.RoundToInt(((Component)i).transform.position.x)}_{Mathf.RoundToInt(((Component)i).transform.position.z)}";
	}

	public static void Reset()
	{
		_pending.Clear();
	}
}
