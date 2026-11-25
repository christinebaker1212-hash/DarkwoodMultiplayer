using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PuppetAnimSuppress
{
	private static bool _patched;

	public static bool WeAreCalling;

	private static readonly Dictionary<int, float> _lastWarnedAt = new Dictionary<int, float>();

	private const float WarnInterval = 2f;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			Type typeFromHandle = typeof(tk2dSpriteAnimator);
			HarmonyMethod val2 = new HarmonyMethod(typeof(PuppetAnimSuppress), "Prefix", (Type[])null);
			val.Patch((MethodBase)AccessTools.Method(typeFromHandle, "Play", new Type[0], (Type[])null), val2, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeFromHandle, "Play", new Type[1] { typeof(string) }, (Type[])null), val2, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeFromHandle, "Play", new Type[1] { typeof(tk2dSpriteAnimationClip) }, (Type[])null), val2, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeFromHandle, "Play", new Type[3]
			{
				typeof(tk2dSpriteAnimationClip),
				typeof(float),
				typeof(float)
			}, (Type[])null), val2, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool Prefix(tk2dSpriteAnimator __instance)
	{
		if (WeAreCalling)
		{
			return true;
		}
		if ((Object)(object)__instance == (Object)null)
		{
			return true;
		}
		if ((Object)(object)((Component)__instance).GetComponentInParent<EnemyPuppet>() == (Object)null)
		{
			return true;
		}
		int instanceID = ((Object)__instance).GetInstanceID();
		float time = Time.time;
		if (!_lastWarnedAt.TryGetValue(instanceID, out var value) || time - value >= 2f)
		{
			_lastWarnedAt[instanceID] = time;
			Plugin.Log.LogWarning("[PuppetAnimSuppress] BLOQUEADO: chamada nativa (fora do SafePlay) tentou Play() no animator '" + ((Object)((Component)__instance).gameObject).name + "' de um puppet - suprimido pra não brigar com o clipe que a REDE mandou.");
		}
		return false;
	}
}
