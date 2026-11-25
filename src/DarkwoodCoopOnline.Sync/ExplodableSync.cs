using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class ExplodableSync
{
	private const float MatchRadius = 100f;

	private static bool _patched;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Expected O, but got Unknown
		//IL_0061: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Explodes), "onActivate", new Type[0], (Type[])null), new HarmonyMethod(typeof(ExplodableSync), "BeforeActivate", (Type[])null), new HarmonyMethod(typeof(ExplodableSync), "AfterActivate", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void BeforeActivate(Explodes __instance, out float __state)
	{
		__state = (((Object)(object)__instance != (Object)null) ? __instance.damage : 0f);
		if (_applyingRemote && (Object)(object)__instance != (Object)null)
		{
			__instance.damage *= 0.5f;
		}
	}

	private static void AfterActivate(Explodes __instance, float __state)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_006e: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)__instance != (Object)null)
		{
			__instance.damage = __state;
		}
		if (!_applyingRemote && CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null))
		{
			string name = ((Object)((Component)__instance).gameObject).name;
			Vector3 position = ((Component)__instance).transform.position;
			Plugin.Log.LogInfo($"[ExplodableSync] '{name}' explodiu aqui (pos={position:F0}) - avisando o peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ExplodableActivated).Str(name).Vec3(position)
				.Finish());
		}
	}

	public static void ApplyActivated(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 val = r.Vec3();
		Explodes val2 = null;
		float num = 100f;
		Explodes[] array = Object.FindObjectsOfType<Explodes>(true);
		foreach (Explodes val3 in array)
		{
			if (!((Object)(object)val3 == (Object)null) && !(((Object)((Component)val3).gameObject).name != text))
			{
				float num2 = Vector3.Distance(((Component)val3).transform.position, val);
				if (num2 < num)
				{
					num = num2;
					val2 = val3;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			Plugin.Log.LogWarning($"[ExplodableSync] peer detonou '{text}' (pos={val:F0}) mas não achei o " + "objeto correspondente aqui.");
			return;
		}
		Plugin.Log.LogInfo($"[ExplodableSync] aplicando explosão de '{text}' recebida do peer (dist={num:F0}).");
		_applyingRemote = true;
		val2.onActivate();
		_applyingRemote = false;
	}
}
