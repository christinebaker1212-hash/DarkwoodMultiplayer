using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DamageDiagnostics
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00db: Expected O, but got Unknown
		//IL_00db: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(Player), "getHit", new Type[9]
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
			val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(DamageDiagnostics), "Prefix", (Type[])null), new HarmonyMethod(typeof(DamageDiagnostics), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Prefix(Player __instance, out float __state)
	{
		__state = ((CharBase)__instance).health;
	}

	private static void Postfix(Player __instance, float __state, Transform attackerTransform, bool byPlayer, bool normalHit)
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_026d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0272: Unknown result type (might be due to invalid IL or missing references)
		//IL_02b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_02bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_02fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0144: Unknown result type (might be due to invalid IL or missing references)
		//IL_015e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0165: Unknown result type (might be due to invalid IL or missing references)
		float num = __state - ((CharBase)__instance).health;
		if (num <= 0.01f)
		{
			return;
		}
		string text = "desconhecido (sem Transform)";
		Vector3 val = default(Vector3);
		bool flag = false;
		string text2 = "n/a";
		if ((Object)(object)attackerTransform != (Object)null)
		{
			val = attackerTransform.position;
			Character component = ((Component)attackerTransform).GetComponent<Character>();
			if (!((Object)(object)component != (Object)null))
			{
				text = ((!((Object)(object)((Component)attackerTransform).GetComponent<RemotePlayerPuppet>() != (Object)null)) ? ("'" + ((Object)attackerTransform).name + "' (sem Character — trap/objeto/etc.)") : "puppet do peer");
			}
			else
			{
				string arg = (((Object)((Component)component).gameObject).name.EndsWith("(Clone)") ? ((Object)((Component)component).gameObject).name.Substring(0, ((Object)((Component)component).gameObject).name.Length - 7) : ((Object)((Component)component).gameObject).name);
				flag = EnemySync.IsTrackedOnHost(component);
				text = $"'{arg}' (type={((CharBase)component).type}, aggressiveness={component.aggressiveness})";
				string text3 = EnemySync.FindOwnedId(component);
				string text4 = EnemySync.FindPuppetId(component);
				string text5 = ((text3 != null) ? ("DONO(id=" + text3 + ")") : ((text4 != null) ? ("PUPPET(id=" + text4 + ")") : "NAO-RASTREADO(nativo puro)"));
				EnemyPuppet component2 = ((Component)component).GetComponent<EnemyPuppet>();
				string text6 = (((Object)(object)component2 != (Object)null) ? ($"TEM EnemyPuppet anexado, alvoLerp=({component2.DebugTargetPos.x:F0},{component2.DebugTargetPos.z:F0}) " + $"distRealVsAlvo={Vector3.Distance(attackerTransform.position, component2.DebugTargetPos):F0}") : "sem EnemyPuppet");
				text = text + " | posse=" + text5 + " | " + text6;
			}
			Renderer componentInChildren = ((Component)attackerTransform).GetComponentInChildren<Renderer>();
			text2 = ((!((Object)(object)componentInChildren != (Object)null)) ? $"sem Renderer encontrado — objetoAtivo={((Component)attackerTransform).gameObject.activeInHierarchy}" : ($"visivel={componentInChildren.isVisible} rendererAtivo={componentInChildren.enabled} " + $"objetoAtivo={((Component)attackerTransform).gameObject.activeInHierarchy} sortingOrder={componentInChildren.sortingOrder}"));
		}
		float num2 = (((Object)(object)attackerTransform != (Object)null) ? Vector3.Distance(((Component)__instance).transform.position, val) : (-1f));
		Plugin.Log.LogInfo($"[DamageDiag] tomei {num:F1} de dano (vida {__state:F1}->{((CharBase)__instance).health:F1}) " + $"de {text} | pos_atacante=({val.x:F0},{val.z:F0}) dist={num2:F0} " + $"minha_pos=({((Component)__instance).transform.position.x:F0},{((Component)__instance).transform.position.z:F0}) " + $"sincronizado_com_peer={flag} {text2} byPlayer={byPlayer} normalHit={normalHit}");
	}
}
