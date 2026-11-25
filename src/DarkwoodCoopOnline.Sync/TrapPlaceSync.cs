using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class TrapPlaceSync
{
	private static bool _patched;

	private static string _pendingType;

	private static readonly HashSet<Trigger> _announced = new HashSet<Trigger>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(Player), "progressBarCompleted", (Type[])null, (Type[])null);
			val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(TrapPlaceSync), "Before", (Type[])null), new HarmonyMethod(typeof(TrapPlaceSync), "After", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Before(Player __instance)
	{
		_pendingType = ((__instance.placingItem && !InvItemClass.isNull(__instance.currentItem)) ? __instance.currentItem.type : null);
	}

	private static void After(Player __instance)
	{
		string pendingType = _pendingType;
		_pendingType = null;
		if (string.IsNullOrEmpty(pendingType) || !CoopRuntime.SessionActive)
		{
			return;
		}
		Trigger val = null;
		Trigger[] array = Object.FindObjectsOfType<Trigger>(true);
		foreach (Trigger val2 in array)
		{
			if ((Object)(object)val2 != (Object)null && val2.setByPlayer && !_announced.Contains(val2))
			{
				val = val2;
				break;
			}
		}
		if (!((Object)(object)val == (Object)null))
		{
			_announced.Add(val);
			Plugin.Log.LogInfo($"[TrapPlaceSync] jogador colocou '{pendingType}' no chão - anunciando pos={((Component)val).transform.position:F0}.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.TrapPlaced).Str(pendingType).Vec3(((Component)val).transform.position)
				.Quat(((Component)val).transform.rotation)
				.Finish());
		}
	}

	public static void ApplyPlaced(PacketReader r)
	{
		string text = r.Str();
		Vector3 val = r.Vec3();
		Quaternion val2 = r.Quat();
		InvItem item = Singleton<ItemsDatabase>.Instance.getItem(text, false);
		if ((Object)(object)item == (Object)null || item.item == (Object)null)
		{
			Plugin.Log.LogWarning("[TrapPlaceSync] peer colocou '" + text + "' mas não achei esse item na ItemsDatabase.");
			return;
		}
		GameObject val3 = Core.AddPrefab(item.item, val, val2, (GameObject)null, false);
		if ((Object)(object)val3 == (Object)null)
		{
			Plugin.Log.LogWarning("[TrapPlaceSync] falha ao criar '" + text + "' colocado pelo peer.");
			return;
		}
		Trigger component = val3.GetComponent<Trigger>();
		if ((Object)(object)component != (Object)null)
		{
			component.setByPlayer = true;
			_announced.Add(component);
		}
		Core.addToSaveable(val3, true, true);
		Singleton<WorldGrid>.Instance.registerToNode(val3, (WorldGrid.Cullable.Type)0);
		Plugin.Log.LogInfo($"[TrapPlaceSync] '{text}' colocado pelo peer replicado aqui, pos={val:F0}.");
	}

	public static void Reset()
	{
		_pendingType = null;
		_announced.Clear();
	}
}
