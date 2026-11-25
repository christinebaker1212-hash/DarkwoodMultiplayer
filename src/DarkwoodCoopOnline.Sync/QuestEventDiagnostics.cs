using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class QuestEventDiagnostics
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Expected O, but got Unknown
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "fire", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(QuestEventDiagnostics), "AfterFire", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "doActionsAfterSpawn", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(QuestEventDiagnostics), "AfterSpawn", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static string Categoria(GameEvent.Type t)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		//IL_008d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Invalid comparison between Unknown and I4
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Invalid comparison between Unknown and I4
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Invalid comparison between Unknown and I4
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Invalid comparison between Unknown and I4
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Invalid comparison between Unknown and I4
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Invalid comparison between Unknown and I4
		//IL_00db: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Invalid comparison between Unknown and I4
		//IL_00c9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cf: Invalid comparison between Unknown and I4
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Invalid comparison between Unknown and I4
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a3: Invalid comparison between Unknown and I4
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Invalid comparison between Unknown and I4
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0055: Invalid comparison between Unknown and I4
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Invalid comparison between Unknown and I4
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Invalid comparison between Unknown and I4
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Invalid comparison between Unknown and I4
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Invalid comparison between Unknown and I4
		//IL_00b7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Invalid comparison between Unknown and I4
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Invalid comparison between Unknown and I4
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Invalid comparison between Unknown and I4
		//IL_005a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0060: Invalid comparison between Unknown and I4
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Invalid comparison between Unknown and I4
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Invalid comparison between Unknown and I4
		//IL_00eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Invalid comparison between Unknown and I4
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Invalid comparison between Unknown and I4
		if ((int)t <= 600)
		{
			if ((int)t <= 201)
			{
				if ((int)t <= 4)
				{
					if ((int)t == 1 || (uint)((int)t - 3) <= 1u)
					{
						return "PERSONAGEM";
					}
				}
				else
				{
					if ((int)t == 130)
					{
						goto IL_00fb;
					}
					if ((int)t == 201)
					{
						goto IL_00f5;
					}
				}
			}
			else if ((int)t <= 210)
			{
				if ((int)t == 205 || (int)t == 210)
				{
					goto IL_00f5;
				}
			}
			else
			{
				if ((int)t == 550)
				{
					return "DIALOGO";
				}
				if ((int)t == 590 || (int)t == 600)
				{
					return "SONHO";
				}
			}
		}
		else if ((int)t <= 950)
		{
			if ((int)t <= 850)
			{
				if ((int)t == 800)
				{
					return "FLAG_MUNDO";
				}
				if ((int)t == 850)
				{
					goto IL_0119;
				}
			}
			else
			{
				if ((int)t == 870)
				{
					goto IL_0119;
				}
				if ((int)t == 950)
				{
					goto IL_011f;
				}
			}
		}
		else if ((int)t <= 980)
		{
			if ((int)t == 970 || (int)t == 980)
			{
				goto IL_011f;
			}
		}
		else if ((int)t == 1100 || (int)t == 1150 || (int)t == 1170)
		{
			goto IL_00fb;
		}
		return "outro";
		IL_00fb:
		return "TELEPORTE";
		IL_0119:
		return "DISPARA_OUTRO_EVENTO";
		IL_011f:
		return "ITEM";
		IL_00f5:
		return "PORTA";
	}

	private static void AfterFire(GameEvent __instance, GameObject thisGO)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Invalid comparison between Unknown and I4
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Invalid comparison between Unknown and I4
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b9: Unknown result type (might be due to invalid IL or missing references)
		//IL_024e: Unknown result type (might be due to invalid IL or missing references)
		if (__instance == null)
		{
			return;
		}
		string arg = Categoria(__instance.type);
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("[QuestEventDiag] '" + (((Object)(object)thisGO != (Object)null) ? ((Object)thisGO).name : "?") + "' custom='" + __instance.customName + "' ");
		stringBuilder.Append($"type={__instance.type}({arg})");
		if ((int)__instance.type == 210)
		{
			stringBuilder.Append($" doorModifyType={__instance.doorModifyType}");
		}
		if ((int)__instance.type == 110)
		{
			stringBuilder.Append($" gameObjectModifyType={__instance.gameObjectModifyType} activeModifier={__instance.activeModifier}");
		}
		string value = Traverse.Create((object)__instance).Field("chomperBrideName").GetValue<string>();
		if (!string.IsNullOrEmpty(value))
		{
			stringBuilder.Append(" chomperBrideName='" + value + "'");
		}
		if (!string.IsNullOrEmpty(__instance.Value))
		{
			stringBuilder.Append(" Value='" + __instance.Value + "'");
		}
		if (!string.IsNullOrEmpty(__instance.Value2))
		{
			stringBuilder.Append(" Value2='" + __instance.Value2 + "'");
		}
		if ((Object)(object)__instance.targetTransform != (Object)null)
		{
			stringBuilder.Append(" targetTransform='" + ((Object)__instance.targetTransform).name + "'");
		}
		if ((Object)(object)__instance.sourceTransform != (Object)null)
		{
			stringBuilder.Append(" sourceTransform='" + ((Object)__instance.sourceTransform).name + "'");
		}
		if (__instance.targetGameObjects != null && __instance.targetGameObjects.Count > 0)
		{
			stringBuilder.Append(" targets=[");
			for (int i = 0; i < __instance.targetGameObjects.Count; i++)
			{
				GameObject val = __instance.targetGameObjects[i];
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(((Object)(object)val != (Object)null) ? ((Object)val).name : "null");
			}
			stringBuilder.Append(']');
		}
		if ((Object)(object)thisGO != (Object)null)
		{
			stringBuilder.Append($" pos={thisGO.transform.position:F0}");
		}
		Plugin.Log.LogInfo(stringBuilder.ToString());
	}

	private static void AfterSpawn(GameEvent __instance, GameObject trueTarget, Character spawnedCharacter)
	{
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)spawnedCharacter == (Object)null))
		{
			string text = (((Object)(object)trueTarget != (Object)null) ? ((Object)trueTarget).name : "?");
			string text2 = ((__instance != null) ? __instance.customName : "");
			Plugin.Log.LogInfo("[QuestEventDiag][Spawn] evento '" + text + "' (customName='" + text2 + "') spawnou " + $"personagem de quest: '{((Object)((Component)spawnedCharacter).gameObject).name}' type={((CharBase)spawnedCharacter).type} " + $"health={((CharBase)spawnedCharacter).health} pos={((Component)spawnedCharacter).transform.position:F0}");
		}
	}
}
