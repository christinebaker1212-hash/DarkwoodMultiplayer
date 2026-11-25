using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class LiquidSync
{
	private const float MatchRadius = 60f;

	private const string TrailPrefabName = "Items/GasolineTrail";

	private static bool _patched;

	private static bool _suppressNextAddPrefab;

	private static bool _suppressNextIgnite;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Core), "AddPrefab", new Type[5]
			{
				typeof(string),
				typeof(Vector3),
				typeof(Quaternion),
				typeof(GameObject),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(LiquidSync), "AddPrefabPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Liquid), "startBurning", (Type[])null, (Type[])null), new HarmonyMethod(typeof(LiquidSync), "BurnPrefix", (Type[])null), new HarmonyMethod(typeof(LiquidSync), "BurnPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AddPrefabPostfix(string prefab, Vector3 position, GameObject __result)
	{
		if (_suppressNextAddPrefab)
		{
			_suppressNextAddPrefab = false;
		}
		else if (!(prefab != "Items/GasolineTrail") && CoopRuntime.SessionActive && !((Object)(object)__result == (Object)null))
		{
			Plugin.Log.LogInfo($"[LiquidSync] rastro de gasolina criado localmente em pos={position:F0} - anunciando pro peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.LiquidTrailCreate).Vec3(position).Finish());
		}
	}

	public static void ApplyTrailCreate(PacketReader r)
	{
		Vector3 val = r.Vec3();
		Plugin.Log.LogInfo($"[LiquidSync] recriando rastro de gasolina do peer em pos={val:F0}.");
		_suppressNextAddPrefab = true;
		Core.AddPrefab("Items/GasolineTrail", val, Quaternion.Euler(90f, 0f, 0f), Core.ItemContainer, false);
		_suppressNextAddPrefab = false;
	}

	private static void BurnPrefix(Liquid __instance, out bool __state)
	{
		__state = (Object)(object)__instance != (Object)null && __instance.burning;
	}

	private static void BurnPostfix(Liquid __instance, bool __state)
	{
		if (_suppressNextIgnite)
		{
			_suppressNextIgnite = false;
		}
		else if (!((Object)(object)__instance == (Object)null))
		{
			Plugin.Log.LogInfo("[LiquidSync][diag] Liquid.startBurning() chamado: '" + CleanName(((Object)((Component)__instance).gameObject).name) + "' " + $"pos={((Component)__instance).transform.position:F0} jáEstavaQueimando={__state} sessionActive={CoopRuntime.SessionActive}");
			if (CoopRuntime.SessionActive && !__state && __instance.burning)
			{
				Plugin.Log.LogInfo("[LiquidSync] '" + CleanName(((Object)((Component)__instance).gameObject).name) + "' pegou fogo de verdade agora - anunciando pro peer.");
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.LiquidIgnited).Str(CleanName(((Object)((Component)__instance).gameObject).name)).Vec3(((Component)__instance).transform.position)
					.Finish());
			}
		}
	}

	public static void ApplyIgnited(PacketReader r)
	{
		string text = r.Str();
		Vector3 val = r.Vec3();
		Liquid val2 = null;
		float num = 60f;
		int num2 = 0;
		Liquid[] array = Object.FindObjectsOfType<Liquid>();
		foreach (Liquid val3 in array)
		{
			if (!((Object)(object)val3 == (Object)null) && !val3.burning && !(CleanName(((Object)((Component)val3).gameObject).name) != text))
			{
				num2++;
				float num3 = Vector3.Distance(((Component)val3).transform.position, val);
				if (num3 < num)
				{
					num = num3;
					val2 = val3;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			Plugin.Log.LogWarning($"[LiquidSync] recebi ignição de '{text}' em pos={val:F0} mas não achei nenhum " + $"Liquid não-queimando com esse nome dentro de {60f:F0} unidades (candidatos com mesmo nome " + $"em qualquer distância: {num2}) - não fiz nada.");
			return;
		}
		Plugin.Log.LogInfo($"[LiquidSync] '{text}' recebido do peer - acendendo aqui também (dist={num:F0}).");
		_suppressNextIgnite = true;
		val2.startBurning();
		_suppressNextIgnite = false;
	}

	private static string CleanName(string n)
	{
		if (n == null || !n.EndsWith("(Clone)"))
		{
			return n;
		}
		return n.Substring(0, n.Length - 7);
	}

	public static void Reset()
	{
		_suppressNextAddPrefab = false;
		_suppressNextIgnite = false;
	}
}
