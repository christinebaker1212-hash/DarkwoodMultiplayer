using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PuppetAttackSuppress
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(Character), "doAttack", new Type[1] { typeof(string) }, (Type[])null);
			val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(PuppetAttackSuppress), "Prefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool Prefix(Character __instance, string sensorType)
	{
		if ((Object)(object)__instance == (Object)null)
		{
			return true;
		}
		if ((Object)(object)((Component)__instance).GetComponent<EnemyPuppet>() == (Object)null)
		{
			return true;
		}
		Plugin.Log.LogWarning("[PuppetAttackSuppress] BLOQUEADO: puppet '" + ((Object)((Component)__instance).gameObject).name + "' tentou doAttack('" + sensorType + "') sozinho (evento de animação nativo, não a rede) - suprimido pra não gerar ataque/dano fantasma local.");
		return false;
	}
}
