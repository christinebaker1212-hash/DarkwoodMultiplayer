using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class NpcDeathDiagnostics
{
	private static bool _patched;

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
			val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(NpcDeathDiagnostics), "Prefix", (Type[])null), new HarmonyMethod(typeof(NpcDeathDiagnostics), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Prefix(Character __instance, out bool __state)
	{
		__state = (Object)(object)__instance != (Object)null && ((CharBase)__instance).alive;
	}

	private static void Postfix(Character __instance, bool __state, Transform attackerTransform, bool byPlayer, bool force)
	{
		if ((Object)(object)__instance == (Object)null || !__state || ((CharBase)__instance).alive || (Object)(object)__instance.npc == (Object)null)
		{
			return;
		}
		string name = __instance.npc.name;
		string text = "desconhecido (sem Transform)";
		if ((Object)(object)attackerTransform != (Object)null)
		{
			if ((Object)(object)attackerTransform == (Object)(object)(((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform : null))
			{
				text = "EU (jogador local)";
			}
			else
			{
				Character component = ((Component)attackerTransform).GetComponent<Character>();
				if (!((Object)(object)component != (Object)null))
				{
					text = ((!((Object)(object)((Component)attackerTransform).GetComponent<RemotePlayerPuppet>() != (Object)null)) ? ("'" + ((Object)attackerTransform).name + "' (sem Character - trap/objeto/explosão/etc.)") : "puppet do peer (jogador remoto)");
				}
				else
				{
					string arg = (((Object)((Component)component).gameObject).name.EndsWith("(Clone)") ? ((Object)((Component)component).gameObject).name.Substring(0, ((Object)((Component)component).gameObject).name.Length - 7) : ((Object)((Component)component).gameObject).name);
					text = $"'{arg}' (type={((CharBase)component).type})";
				}
			}
		}
		else if (force)
		{
			text = "kill forçado sem Transform (ex.: replicação de corpo já morto via CorpseSync/EnemySync - não é uma morte NOVA de verdade)";
		}
		string text2 = ((EnemySync.FindOwnedId(__instance) != null) ? "DONO(meu, real)" : ((EnemySync.FindPuppetId(__instance) != null) ? "PUPPET(réplica de rede)" : "NAO-RASTREADO"));
		int num = (((Object)(object)Singleton<Controller>.Instance != (Object)null) ? Singleton<Controller>.Instance.day : (-1));
		int num2 = (((Object)(object)Singleton<Controller>.Instance != (Object)null) ? Singleton<Controller>.Instance.CurrentTime : (-1));
		Plugin.Log.LogInfo($"[NpcDeathDiag] NPC '{name}' morreu (dia={num} hora={num2}) - " + $"atacante={text} | byPlayer={byPlayer} force={force} posse={text2} | " + $"pos=({((Component)__instance).transform.position.x:F0},{((Component)__instance).transform.position.z:F0})");
	}
}
