using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class CutsceneDiagnostics
{
	private static bool _patched;

	private static bool _wasPlaying;

	private static readonly string[] DoctorVariantNames = new string[3] { "Doctor_confronted", "Doctor_trapSet", "Doctor_confronted2" };

	private static readonly HashSet<CutsceneManager> _forcedEndCutscenes = new HashSet<CutsceneManager>();

	private static bool _recoveredThisCutscene;

	private const bool AutoRecoverEnabled = false;

	private static bool _initiateDialogueCalledRecently;

	private static NPC _pendingDialogueNpc;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Expected O, but got Unknown
		//IL_0073: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Expected O, but got Unknown
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Expected O, but got Unknown
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_015b: Expected O, but got Unknown
		//IL_0183: Unknown result type (might be due to invalid IL or missing references)
		//IL_0198: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a5: Expected O, but got Unknown
		//IL_01a5: Expected O, but got Unknown
		//IL_01ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Expected O, but got Unknown
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0211: Expected O, but got Unknown
		//IL_0239: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "startScene", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterStartScene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "endCurrentScene", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterEndCurrentScene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "prologue_endCutscene", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterPrologueEndCutscene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(CutsceneManager), "onFadedIn", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterOnFadedIn", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Core), "displayMessage", new Type[4]
			{
				typeof(string),
				typeof(Transform),
				typeof(float),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterDisplayMessage", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(DialogueWindow), "initiateDialogue", (Type[])null, (Type[])null), new HarmonyMethod(typeof(CutsceneDiagnostics), "BeforeInitiateDialogue", (Type[])null), new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterInitiateDialogue", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(DialogueWindow), "displayDialogue", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterDisplayDialogue", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(DialogueWindow), "checkIfWantToDisplayWelcome", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CutsceneDiagnostics), "AfterCheckWelcome", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(GameEvent), "removeMe", (Type[])null, (Type[])null), new HarmonyMethod(typeof(CutsceneDiagnostics), "BeforeRemoveMe", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool BeforeRemoveMe(GameObject target)
	{
		if ((Object)(object)target == (Object)null)
		{
			return true;
		}
		if (DebugCheats.VanillaMode)
		{
			return true;
		}
		string[] doctorVariantNames = DoctorVariantNames;
		foreach (string text in doctorVariantNames)
		{
			if (!(((Object)target).name != text))
			{
				Plugin.Log.LogWarning("[CutsceneDiag][AutoRecover] BLOQUEANDO destruição de '" + text + "' (removeMe() -> Destroy real) - desativando em vez disso, pra 'openDialogue' agendado (34s) ainda achar o alvo depois.");
				Helpers.setMeActive(target, false);
				return false;
			}
		}
		return true;
	}

	public static void Tick()
	{
		if ((Object)(object)_pendingDialogueNpc != (Object)null)
		{
			Dreams instance = Singleton<Dreams>.Instance;
			if (!((Object)(object)instance != (Object)null) || (!instance.dreaming && !instance.dreamPrepared))
			{
				NPC pendingDialogueNpc = _pendingDialogueNpc;
				_pendingDialogueNpc = null;
				DialogueWindow val = (((Object)(object)Singleton<UI>.Instance != (Object)null) ? Singleton<UI>.Instance.dialogueWindow : null);
				if ((Object)(object)val != (Object)null && (Object)(object)pendingDialogueNpc != (Object)null)
				{
					Plugin.Log.LogInfo("[CutsceneDiag][CharacterDialogue.Dialogue] sonho acabou - retomando initiateDialogue() pra npc='" + ((Object)((Component)pendingDialogueNpc).gameObject).name + "' agora.");
					val.initiateDialogue(pendingDialogueNpc);
				}
			}
		}
		Controller instance2 = Singleton<Controller>.Instance;
		if ((Object)(object)instance2 == (Object)null)
		{
			return;
		}
		bool playingCutscene = instance2.playingCutscene;
		if (playingCutscene != _wasPlaying)
		{
			_wasPlaying = playingCutscene;
			if (playingCutscene)
			{
				string arg = (((Object)(object)instance2.currentCutsceneManager != (Object)null) ? ((Object)((Component)instance2.currentCutsceneManager).gameObject).name : "(null)");
				int num = (((Object)(object)instance2.currentCutsceneManager != (Object)null) ? instance2.currentCutsceneManager.currentScene : (-1));
				Plugin.Log.LogInfo($"[CutsceneDiag] ENTROU em cutscene — manager='{arg}' cena={num} " + $"day={instance2.day} time={instance2.CurrentTime}");
			}
			else
			{
				Plugin.Log.LogInfo($"[CutsceneDiag] SAIU de cutscene — day={instance2.day} time={instance2.CurrentTime}");
			}
		}
	}

	private static void AfterStartScene(CutsceneManager __instance)
	{
		if (!((Object)(object)__instance == (Object)null))
		{
			int currentScene = __instance.currentScene;
			if (currentScene < 0 || currentScene >= __instance.cutscenes.Count)
			{
				Plugin.Log.LogWarning($"[CutsceneDiag] startScene() manager='{((Object)((Component)__instance).gameObject).name}' cena={currentScene} " + $"- ÍNDICE FORA DA LISTA (total={__instance.cutscenes.Count}) - cutscene não tem mais cenas a tocar.");
				return;
			}
			CutsceneManager.Cutscene val = __instance.cutscenes[currentScene];
			Plugin.Log.LogInfo($"[CutsceneDiag] startScene() manager='{((Object)((Component)__instance).gameObject).name}' cena={currentScene}/{__instance.cutscenes.Count} " + string.Format("length={0:F1}s playerVisible={1} transition={2}", val.length, val.playerIsVisible, ((Object)(object)val.transition != (Object)null) ? ((Object)val.transition).name : "NULL"));
		}
	}

	private static void AfterPrologueEndCutscene(CutsceneManager __instance)
	{
		Plugin.Log.LogInfo("[CutsceneDiag] prologue_endCutscene() rodou (manager='" + (((Object)(object)__instance != (Object)null) ? ((Object)((Component)__instance).gameObject).name : "NULL") + "') " + $"- depois: Core.forbidInputs={Core.forbidInputs} Core.cantChangeForbidInputs={Core.cantChangeForbidInputs} " + $"playingCutscene={Singleton<Controller>.Instance?.playingCutscene}.");
	}

	private static void AfterEndCurrentScene(CutsceneManager __instance)
	{
		if (!((Object)(object)__instance == (Object)null))
		{
			int currentScene = __instance.currentScene;
			bool flag = currentScene >= 0 && currentScene < __instance.cutscenes.Count && (Object)(object)__instance.cutscenes[currentScene].transition != (Object)null;
			Plugin.Log.LogInfo($"[CutsceneDiag] endCurrentScene() manager='{((Object)((Component)__instance).gameObject).name}' cena={currentScene} " + string.Format("disabled={0} transition={1}", __instance.disabled, flag ? "OK" : "NULL") + (flag ? "" : " *** SEM TRANSIÇÃO - a cutscene provavelmente vai TRAVAR aqui (só onFadedIn avança de cena) ***"));
			if (!flag)
			{
				TryAutoRecoverTrainWreck(__instance);
			}
			if (!flag)
			{
				ForceTrainWreckEndCutscene(__instance);
			}
		}
	}

	private static void ForceTrainWreckEndCutscene(CutsceneManager __instance)
	{
		if (_forcedEndCutscenes.Add(__instance))
		{
			Plugin.Log.LogWarning("[CutsceneDiag] '" + ((Object)((Component)__instance).gameObject).name + "' sem transition - forçando prologue_endCutscene() direto (mesmo se o despacho nativo via GameEvent for tentar rodar depois, chamar 2x é seguro).");
			__instance.prologue_endCutscene();
		}
	}

	private static void TryAutoRecoverTrainWreck(CutsceneManager mgr)
	{
	}

	private static NPC FindDoctor()
	{
		NPC[] array = Object.FindObjectsOfType<NPC>(true);
		foreach (NPC val in array)
		{
			if ((Object)(object)val != (Object)null && ((Object)((Component)val).gameObject).name.IndexOf("doctor", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return val;
			}
		}
		return null;
	}

	private static void ForceExactTrapStartDialogue(NPC doctor)
	{
		//IL_00d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e0: Invalid comparison between Unknown and I4
		try
		{
			DialogueWindow val = (((Object)(object)Singleton<UI>.Instance != (Object)null) ? Singleton<UI>.Instance.dialogueWindow : null);
			if ((Object)(object)val == (Object)null)
			{
				Plugin.Log.LogError("[CutsceneDiag][AutoRecover] UI.dialogueWindow nulo - impossível forçar.");
				return;
			}
			if (val.displayingDialogue && val.currentDialogue != null && val.currentDialogue.fullName != null && val.currentDialogue.fullName.IndexOf("trapStart", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Plugin.Log.LogInfo("[CutsceneDiag][AutoRecover] tentativa 1 já funcionou (trapStart já em exibição) - nada mais a fazer.");
				return;
			}
			if ((Object)(object)doctor == (Object)null || (Object)(object)doctor.characterDialogue == (Object)null)
			{
				Plugin.Log.LogError("[CutsceneDiag][AutoRecover] Doutor ou characterDialogue nulo na tentativa 2 - não dá pra forçar.");
				return;
			}
			CharacterDialogue.Dialogue val2 = null;
			foreach (CharacterDialogue.Dialogue dialogue in doctor.characterDialogue.dialogues)
			{
				if (dialogue != null && (int)dialogue.type == 200 && ((dialogue.name != null && dialogue.name.IndexOf("trapStart", StringComparison.OrdinalIgnoreCase) >= 0) || (dialogue.fullName != null && dialogue.fullName.IndexOf("trapStart", StringComparison.OrdinalIgnoreCase) >= 0)))
				{
					val2 = dialogue;
					break;
				}
			}
			if (val2 == null)
			{
				Plugin.Log.LogError("[CutsceneDiag][AutoRecover] Nenhum diálogo 'trapStart' encontrado no characterDialogue do " + $"Doutor ({doctor.characterDialogue.dialogues.Count} diálogo(s) no total) - jogador liberado, mas sem essa cena específica.");
				return;
			}
			Plugin.Log.LogWarning("[CutsceneDiag][AutoRecover] FORÇANDO displayDialogue('" + val2.fullName + "') direto (tentativa 1 não abriu a tela certa sozinha).");
			if (!((Component)val).gameObject.activeSelf)
			{
				val.npc = doctor;
				((Component)val).gameObject.SetActive(true);
			}
			val.displayDialogue(val2.fullName);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("[CutsceneDiag][AutoRecover] ForceExactTrapStartDialogue EXPLODIU: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	private static void AfterOnFadedIn(CutsceneManager __instance)
	{
		if (!((Object)(object)__instance == (Object)null))
		{
			Plugin.Log.LogInfo("[CutsceneDiag] onFadedIn() manager='" + ((Object)((Component)__instance).gameObject).name + "' - avançando pra próxima cena " + $"(era cena={__instance.currentScene}).");
		}
	}

	private static void AfterDisplayMessage(string message, Transform destTransform, CharacterMessage __result)
	{
		if (!((Object)(object)__result == (Object)null))
		{
			string text = (((Object)(object)destTransform != (Object)null) ? ((Object)destTransform).name : "(null)");
			Plugin.Log.LogInfo("[CutsceneDiag][Message] alvo='" + text + "' texto=\"" + message + "\"");
		}
	}

	private static bool BeforeInitiateDialogue(NPC _npc)
	{
		bool num = (Object)(object)_npc != (Object)null && Array.IndexOf(DoctorVariantNames, ((Object)((Component)_npc).gameObject).name) >= 0;
		Dreams instance = Singleton<Dreams>.Instance;
		if (num && (Object)(object)instance != (Object)null && (instance.dreaming || instance.dreamPrepared))
		{
			_pendingDialogueNpc = _npc;
			Plugin.Log.LogWarning("[CutsceneDiag][CharacterDialogue.Dialogue] initiateDialogue() tentou abrir pra npc='" + (((Object)(object)_npc != (Object)null) ? ((Object)((Component)_npc).gameObject).name : "NULL") + "' " + $"mas o sonho JÁ está ativo (dreaming={instance.dreaming} dreamPrepared={instance.dreamPrepared}) - " + "SEGURANDO o diálogo até o sonho acabar, pra não abrir em cima e travar o clique.");
			return false;
		}
		_initiateDialogueCalledRecently = true;
		Plugin.Log.LogWarning("[CutsceneDiag][CharacterDialogue.Dialogue] PREFIX initiateDialogue() CHAMADO - npc=" + (((Object)(object)_npc != (Object)null) ? ("'" + ((Object)((Component)_npc).gameObject).name + "'") : "NULL") + (((Object)(object)_npc != (Object)null) ? (" characterDialogue=" + (((Object)(object)_npc.characterDialogue != (Object)null) ? "OK" : "NULL")) : " *** NPC NULL - vai explodir na 1ª linha (npc.portraitCache) ***"));
		return true;
	}

	private static void AfterInitiateDialogue(NPC _npc)
	{
		Plugin.Log.LogInfo("[CutsceneDiag][CharacterDialogue.Dialogue] initiateDialogue() TERMINOU sem exceção - npc='" + (((Object)(object)_npc != (Object)null) ? ((Object)((Component)_npc).gameObject).name : "NULL") + "'");
	}

	private static void AfterDisplayDialogue(string dialogueName)
	{
		Plugin.Log.LogInfo("[CutsceneDiag][CharacterDialogue.Dialogue] displayDialogue() dialogueName='" + dialogueName + "'");
		if (Core.cantChangeForbidInputs || Core.forbidInputs)
		{
			Plugin.Log.LogWarning("[CutsceneDiag][CharacterDialogue.Dialogue] diálogo '" + dialogueName + "' na tela mas Core.forbidInputs=" + $"{Core.forbidInputs} cantChangeForbidInputs={Core.cantChangeForbidInputs} - isso bloquearia o clique " + "pra sempre (prologue_endCutscene() não rodou ou não bastou) - forçando limpeza agora.");
			Core.cantChangeForbidInputs = false;
			Core.forbidInputs = false;
		}
	}

	private static void AfterCheckWelcome(DialogueWindow __instance)
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Invalid comparison between Unknown and I4
		if ((Object)(object)__instance == (Object)null || (Object)(object)__instance.npc == (Object)null || (Object)(object)__instance.npc.characterDialogue == (Object)null)
		{
			return;
		}
		List<CharacterDialogue.Dialogue> dialogues = __instance.npc.characterDialogue.dialogues;
		Plugin.Log.LogInfo("[CutsceneDiag][CharacterDialogue.Dialogue] checkIfWantToDisplayWelcome() npc='" + ((Object)((Component)__instance.npc).gameObject).name + "' " + $"- {dialogues.Count} diálogo(s) no total, listando os tipo 'welcome':");
		for (int i = 0; i < dialogues.Count; i++)
		{
			CharacterDialogue.Dialogue val = dialogues[i];
			if ((int)val.type == 200)
			{
				Plugin.Log.LogInfo("[CutsceneDiag][CharacterDialogue.Dialogue]   fullName='" + val.fullName + "' name='" + val.name + "' " + $"requirementsMet={val.requirementsMet()} disabled={val.disabled} disabledInEditor={val.disabledInEditor} " + $"alreadyShown={val.alreadyShown} alwaysShow={val.alwaysShow} isCritical={val.isCritical} priority={val.priority}");
			}
		}
	}

	public static void Reset()
	{
		_wasPlaying = false;
		_recoveredThisCutscene = false;
		_initiateDialogueCalledRecently = false;
		_pendingDialogueNpc = null;
		_forcedEndCutscenes.Clear();
	}
}
