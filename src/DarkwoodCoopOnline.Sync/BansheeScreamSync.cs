using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class BansheeScreamSync
{
	private const float ItemMatchRadius = 150f;

	private static bool _patched;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		//IL_0092: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "bansheeScreamHit", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(BansheeScreamSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Item), "empDisable", (Type[])null, (Type[])null), new HarmonyMethod(typeof(BansheeScreamSync), "EmpPrefix", (Type[])null), new HarmonyMethod(typeof(BansheeScreamSync), "EmpPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix()
	{
		if (!_applyingRemote && CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo("[BansheeScreamSync] levei o susto da Banshee de verdade (colisão física local) - avisando o peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.BansheeScreamHit).Finish());
		}
	}

	public static void ApplyRemote()
	{
		if (!((Object)(object)Player.Instance == (Object)null))
		{
			Plugin.Log.LogInfo("[BansheeScreamSync] peer levou o susto da Banshee - aplicando o mesmo efeito aqui.");
			_applyingRemote = true;
			Player.Instance.bansheeScreamHit();
			_applyingRemote = false;
		}
	}

	private static void EmpPrefix(Item __instance, out bool __state)
	{
		__state = (Object)(object)__instance != (Object)null && __instance.disabled;
	}

	private static void EmpPostfix(Item __instance, float _time, bool __state)
	{
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		if (!_applyingRemote && CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null) && !__state)
		{
			Plugin.Log.LogInfo("[BansheeScreamSync] item '" + ((Object)((Component)__instance).gameObject).name + "' levou EMP de verdade (colisão física local) - avisando o peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ItemEmpDisabled).Str(((Object)((Component)__instance).gameObject).name).Vec3(((Component)__instance).transform.position)
				.Float(_time)
				.Finish());
		}
	}

	public static void ApplyItemDisabled(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 val = r.Vec3();
		float num = r.Float();
		Item val2 = null;
		float num2 = 150f;
		Item[] array = Object.FindObjectsOfType<Item>(true);
		foreach (Item val3 in array)
		{
			if (!((Object)(object)val3 == (Object)null) && !(((Object)((Component)val3).gameObject).name != text))
			{
				float num3 = Vector3.Distance(((Component)val3).transform.position, val);
				if (num3 < num2)
				{
					num2 = num3;
					val2 = val3;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			Plugin.Log.LogWarning($"[BansheeScreamSync] peer levou EMP em '{text}' (pos={val:F0}) mas não " + "achei o item correspondente aqui.");
			return;
		}
		Plugin.Log.LogInfo($"[BansheeScreamSync] aplicando EMP em '{text}' recebido do peer (dist={num2:F0}).");
		_applyingRemote = true;
		val2.empDisable(num);
		_applyingRemote = false;
	}
}
