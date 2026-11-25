using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PrologueDiagnostics
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "fire", new Type[1] { typeof(GameObject) }, (Type[])null), new HarmonyMethod(typeof(PrologueDiagnostics), "BeforeGameEventFire", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "startScene", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PrologueDiagnostics), "AfterStartScene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "endCurrentScene", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PrologueDiagnostics), "AfterEndScene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "prologue_endCutscene", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PrologueDiagnostics), "AfterPrologueEndCutscene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Events), "fireWorldEvent", new Type[1] { typeof(string) }, (Type[])null), new HarmonyMethod(typeof(PrologueDiagnostics), "BeforeFireWorldEvent", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Plugin.Log.LogInfo("[PrologueDiagnostics] hooks instalados - GameEvent.fire / CutsceneManager.startScene|endCurrentScene|prologue_endCutscene / Events.fireWorldEvent (ALARGADO jul/2026: loga SEMPRE agora, não só na localização do prólogo - o gate por `isPrologueLocation` nunca bateu num teste real onde o usuário confirmou estar vendo a cutscene do prólogo, suspeita de que o próprio gate está errado/o prólogo não seta essa flag como eu esperava; log fica mais verboso até isso ser esclarecido).");
		}
	}

	private static void BeforeGameEventFire(GameEvent __instance, GameObject thisGO)
	{
		if (__instance != null)
		{
			Plugin.Log.LogInfo($"[PrologueDiag][GameEvent] type={__instance.type} eventType={__instance.eventType} " + $"customName='{__instance.customName}' delay={__instance.delay:F2} amount={__instance.amount} " + "stringValue='" + __instance.stringValue + "' Value='" + __instance.Value + "' Value2='" + __instance.Value2 + "' " + $"floatValue={__instance.floatValue:F2} intValue={__instance.intValue} " + string.Format("alvoGO='{0}' tempo={1:F1}", ((Object)(object)thisGO != (Object)null) ? ((Object)thisGO).name : "(null)", Time.time));
		}
	}

	private static void AfterStartScene(CutsceneManager __instance)
	{
		if (!((Object)(object)__instance == (Object)null))
		{
			int currentScene = __instance.currentScene;
			string arg = "(índice fora do range de cutscenes)";
			if (__instance.cutscenes != null && currentScene >= 0 && currentScene < __instance.cutscenes.Count)
			{
				CutsceneManager.Cutscene val = __instance.cutscenes[currentScene];
				arg = $"length={val.length:F1}s playerIsVisible={val.playerIsVisible} FOV={val.FOV:F1} " + "container='" + (((Object)(object)val.sceneContainer != (Object)null) ? ((Object)val.sceneContainer).name : "(null)") + "' cameraPos='" + (((Object)(object)val.cameraPos != (Object)null) ? ((Object)val.cameraPos).name : "(null)") + "'";
			}
			Plugin.Log.LogInfo($"[PrologueDiag][CutsceneManager.Cutscene] startScene() idx={currentScene} {arg} tempo={Time.time:F1}");
		}
	}

	private static void AfterEndScene(CutsceneManager __instance)
	{
		if (!((Object)(object)__instance == (Object)null))
		{
			Plugin.Log.LogInfo($"[PrologueDiag][CutsceneManager.Cutscene] endCurrentScene() idx(antes de avançar)={__instance.currentScene} tempo={Time.time:F1}");
		}
	}

	private static void AfterPrologueEndCutscene()
	{
		Plugin.Log.LogInfo($"[PrologueDiag][CutsceneManager.Cutscene] prologue_endCutscene() - prólogo terminando. tempo={Time.time:F1}");
	}

	private static void BeforeFireWorldEvent(string type)
	{
		Plugin.Log.LogInfo($"[PrologueDiag][WorldEvent] fireWorldEvent('{type}') tempo={Time.time:F1}");
	}
}
