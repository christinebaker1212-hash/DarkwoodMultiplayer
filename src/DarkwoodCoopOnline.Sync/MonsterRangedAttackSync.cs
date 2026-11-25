using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class MonsterRangedAttackSync
{
	private static bool _patched;

	private static readonly List<(ThrownItem item, string ownedId, string prefabName)> _pending = new List<(ThrownItem, string, string)>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(ThrownItem), "Awake", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MonsterRangedAttackSync), "AwakePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(ThrownItem), "onCollide", new Type[2]
			{
				typeof(Collider),
				typeof(Vector3)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MonsterRangedAttackSync), "CollidePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AwakePostfix(ThrownItem __instance)
	{
		if (!CoopRuntime.SessionActive || (Object)(object)__instance == (Object)null)
		{
			return;
		}
		InvItemClass val = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.currentItem : null);
		if (!InvItemClass.isNull(val) && (Object)(object)val.baseClass != (Object)null && val.baseClass.isThrowable)
		{
			return;
		}
		Transform objectThatSpawnedMe = __instance.objectThatSpawnedMe;
		if ((Object)(object)objectThatSpawnedMe == (Object)null)
		{
			return;
		}
		Character component = ((Component)objectThatSpawnedMe).GetComponent<Character>();
		if ((Object)(object)component == (Object)null)
		{
			return;
		}
		string text = EnemySync.ResolveOwnedId(component);
		if (text != null)
		{
			string text2 = ((Object)((Component)__instance).gameObject).name;
			if (text2.EndsWith("(Clone)"))
			{
				text2 = text2.Substring(0, text2.Length - 7);
			}
			_pending.Add((__instance, text, text2));
		}
	}

	private static void CollidePostfix(ThrownItem __instance)
	{
		if ((Object)(object)__instance == (Object)null)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < _pending.Count; i++)
		{
			if (_pending[i].item == __instance)
			{
				num = i;
				break;
			}
		}
		if (num >= 0)
		{
			var (val, text, text2) = _pending[num];
			_pending.RemoveAt(num);
			if (CoopRuntime.SessionActive)
			{
				Vector3 position = ((Component)val).transform.position;
				Quaternion rotation = ((Component)val).transform.rotation;
				Plugin.Log.LogInfo($"[MonsterRangedAttackSync] projétil de '{text}' (prefab='{text2}') colidiu - anunciando pos={position:F0}.");
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.MonsterProjectileLanded).Str(text).Str(text2)
					.Vec3(position)
					.Quat(rotation)
					.Finish());
			}
		}
	}

	public static void ApplyLanded(PacketReader r)
	{
		string text = r.Str();
		string text2 = r.Str();
		Vector3 val = r.Vec3();
		Quaternion val2 = r.Quat();
		Character val3 = EnemySync.ResolvePuppetById(text);
		if ((Object)(object)val3 == (Object)null)
		{
			Plugin.Log.LogWarning("[MonsterRangedAttackSync] peer anunciou projétil de '" + text + "' mas não achei o puppet correspondente.");
			return;
		}
		Object val4 = null;
		foreach (Character.SensorType sensorType in val3.sensorTypes)
		{
			if (sensorType.sensor != (Object)null && sensorType.sensor.name == text2)
			{
				val4 = sensorType.sensor;
				break;
			}
		}
		if (val4 == (Object)null)
		{
			Plugin.Log.LogWarning("[MonsterRangedAttackSync] não achei sensor '" + text2 + "' na lista de '" + text + "'.");
			return;
		}
		GameObject val5 = Core.AddPrefab(val4, val, val2, (GameObject)null, true);
		ThrownItem val6 = (((Object)(object)val5 != (Object)null) ? val5.GetComponent<ThrownItem>() : null);
		if ((Object)(object)val6 == (Object)null)
		{
			Plugin.Log.LogWarning("[MonsterRangedAttackSync] criei '" + text2 + "' mas sem componente ThrownItem.");
			return;
		}
		val6.thrown = true;
		val6.onCollide((Collider)null, val);
		Plugin.Log.LogInfo($"[MonsterRangedAttackSync] projétil '{text2}' de '{text}' replicado e assentado aqui, pos={val:F0}.");
	}

	public static void Reset()
	{
		_pending.Clear();
	}
}
