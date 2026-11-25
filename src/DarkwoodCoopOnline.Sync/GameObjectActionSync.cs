using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class GameObjectActionSync
{
	private const float MatchRadius = 400f;

	private static bool _patched;

	private static bool _applyingRemote;

	private static readonly HashSet<string> _announced = new HashSet<string>();

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "fire", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(GameObjectActionSync), "AfterFire", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterFire(GameEvent __instance, GameObject thisGO)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Invalid comparison between Unknown and I4
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Invalid comparison between Unknown and I4
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Invalid comparison between Unknown and I4
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Invalid comparison between Unknown and I4
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Invalid comparison between Unknown and I4
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Invalid comparison between Unknown and I4
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0119: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		if (_applyingRemote || !CoopRuntime.SessionActive || __instance == null)
		{
			return;
		}
		bool flag;
		bool flag2;
		if ((int)__instance.type == 110)
		{
			if ((int)__instance.gameObjectModifyType != 30 && (int)__instance.gameObjectModifyType != 40)
			{
				return;
			}
			flag = (int)__instance.gameObjectModifyType == 40;
			flag2 = __instance.activeModifier;
		}
		else if ((int)__instance.type == 105)
		{
			flag = false;
			flag2 = __instance.activeModifier;
		}
		else
		{
			if ((int)__instance.type != 106)
			{
				return;
			}
			flag = true;
			flag2 = false;
		}
		if (__instance.targetGameObjects == null)
		{
			return;
		}
		foreach (GameObject targetGameObject in __instance.targetGameObjects)
		{
			if (!((Object)(object)targetGameObject == (Object)null))
			{
				string name = ((Object)targetGameObject).name;
				Vector3 position = targetGameObject.transform.position;
				string item = KeyOf(name, position, flag);
				if (_announced.Add(item))
				{
					Plugin.Log.LogInfo("[GameObjectActionSync] '" + name + "' " + (flag ? "removido" : $"setActive={flag2}") + " " + $"por GameEvent (pos={position:F0}) - avisando o peer.");
					CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.GameObjectAction).Str(name).Vec3(position)
						.Bool(flag)
						.Bool(flag2)
						.Finish());
				}
			}
		}
	}

	public static void ApplyAction(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 val = r.Vec3();
		bool flag = r.Bool();
		bool flag2 = r.Bool();
		_announced.Add(KeyOf(text, val, flag));
		GameObject val2 = null;
		float num = 400f;
		Transform[] array = Object.FindObjectsOfType<Transform>(true);
		foreach (Transform val3 in array)
		{
			if (!((Object)(object)val3 == (Object)null) && !(((Object)((Component)val3).gameObject).name != text))
			{
				float num2 = Vector3.Distance(val3.position, val);
				if (num2 < num)
				{
					num = num2;
					val2 = ((Component)val3).gameObject;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			Plugin.Log.LogWarning("[GameObjectActionSync] peer aplicou ação em '" + text + "' mas não achei esse GameObject na minha cena.");
			return;
		}
		_applyingRemote = true;
		if (flag)
		{
			Plugin.Log.LogInfo("[GameObjectActionSync] removendo '" + text + "' recebido do peer.");
			Object.Destroy((Object)(object)val2);
		}
		else
		{
			Plugin.Log.LogInfo($"[GameObjectActionSync] setActive={flag2} em '{text}' recebido do peer.");
			val2.SetActive(flag2);
		}
		_applyingRemote = false;
	}

	private static string KeyOf(string name, Vector3 p, bool remove)
	{
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		return $"{name}@{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}_{remove}";
	}

	public static void Reset()
	{
		_announced.Clear();
	}
}
