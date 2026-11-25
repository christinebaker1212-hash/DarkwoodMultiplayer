using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DreamSync
{
	private struct PendingChainDream
	{
		public string PresetName;

		public int DreamId;

		public bool Lvl2;

		public bool Lvl3;

		public bool Lvl5;

		public bool Lvl6;

		public bool Lvl7;
	}

	private static bool _patched;

	private static bool _applyingRemote;

	private static string _currentDreamPreset;

	private static string _pendingPresetName;

	private static int _pendingDreamId;

	private static bool _pendingLvl2;

	private static bool _pendingLvl3;

	private static bool _pendingLvl5;

	private static bool _pendingLvl6;

	private static bool _pendingLvl7;

	private static readonly Queue<PendingChainDream> _pendingChainDreams = new Queue<PendingChainDream>();

	private const float CloseGraceSeconds = 0.5f;

	private static float _dialogueClosedAt = -1f;

	private static bool _wasDreamingBeforeEnd;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Expected O, but got Unknown
		//IL_00b3: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Dreams), "prepareDream", new Type[2]
			{
				typeof(string),
				typeof(int)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(DreamSync), "AfterPrepareDream", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Dreams), "endDreaming", (Type[])null, (Type[])null), new HarmonyMethod(typeof(DreamSync), "BeforeEndDreaming", (Type[])null), new HarmonyMethod(typeof(DreamSync), "AfterEndDreaming", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool IsDoctorDream(string presetName)
	{
		if (!(presetName == "dream_doctor_01"))
		{
			return presetName == "dream_doctor_02";
		}
		return true;
	}

	private static void AfterPrepareDream(string presetName, int dreamId)
	{
		if (!string.IsNullOrEmpty(presetName))
		{
			_currentDreamPreset = presetName;
		}
		if (IsDoctorDream(presetName))
		{
			PauseSync.EnterDreamSuppression();
		}
		if (!_applyingRemote && CoopRuntime.SessionActive && !string.IsNullOrEmpty(presetName))
		{
			Dreams instance = Singleton<Dreams>.Instance;
			Plugin.Log.LogInfo($"[DreamSync] sonho '{presetName}' (id={dreamId}) começou aqui - chamando o peer pra sonhar junto " + $"(flags: lvl2={instance.hadDreamAtLvl2} lvl3={instance.hadDreamAtLvl3} lvl5={instance.hadDreamAtLvl5} lvl6={instance.hadDreamAtLvl6} lvl7={instance.hadDreamAtLvl7}).");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.DreamStarted).Str(presetName).Int(dreamId)
				.Bool(instance.hadDreamAtLvl2)
				.Bool(instance.hadDreamAtLvl3)
				.Bool(instance.hadDreamAtLvl5)
				.Bool(instance.hadDreamAtLvl6)
				.Bool(instance.hadDreamAtLvl7)
				.Finish());
		}
	}

	public static void ApplyStarted(PacketReader r)
	{
		string text = r.Str();
		int num = r.Int();
		bool flag = r.Bool();
		bool flag2 = r.Bool();
		bool flag3 = r.Bool();
		bool flag4 = r.Bool();
		bool flag5 = r.Bool();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		Dreams instance = Singleton<Dreams>.Instance;
		if ((Object)(object)instance != (Object)null && (instance.dreaming || instance.dreamPrepared))
		{
			if (text != _currentDreamPreset)
			{
				_pendingChainDreams.Enqueue(new PendingChainDream
				{
					PresetName = text,
					DreamId = num,
					Lvl2 = flag,
					Lvl3 = flag2,
					Lvl5 = flag3,
					Lvl6 = flag4,
					Lvl7 = flag5
				});
				Plugin.Log.LogWarning("[DreamSync] peer encadeou pro sonho '" + text + "', mas eu ainda estou em " + $"'{_currentDreamPreset}' (dreaming={instance.dreaming}/dreamPrepared={instance.dreamPrepared}) - guardando o próximo " + $"sonho na fila ({_pendingChainDreams.Count} pendente(s)), entro assim que não tiver diálogo local aberto.");
			}
			else
			{
				Plugin.Log.LogWarning("[DreamSync] peer começou o sonho '" + text + "', mas eu JÁ estou " + $"dreaming={instance.dreaming}/dreamPrepared={instance.dreamPrepared} (provável gatilho nativo local meu, quase junto) - " + "NÃO chamando prepareDream de novo (evita corrotina duplicada/reentrante).");
			}
		}
		else
		{
			DialogueWindow val = (((Object)(object)Singleton<UI>.Instance != (Object)null) ? Singleton<UI>.Instance.dialogueWindow : null);
			if ((Object)(object)val != (Object)null && (val.displayingDialogue || ((Component)val).gameObject.activeSelf))
			{
				_pendingPresetName = text;
				_pendingDreamId = num;
				_pendingLvl2 = flag;
				_pendingLvl3 = flag2;
				_pendingLvl5 = flag3;
				_pendingLvl6 = flag4;
				_pendingLvl7 = flag5;
				Plugin.Log.LogWarning("[DreamSync] peer começou o sonho '" + text + "', mas eu tenho um diálogo aberto agora ('" + val.currentDialogue?.fullName + "') - segurando o sonho até o diálogo fechar, pra não travar o clique.");
			}
			else
			{
				ApplyStartedNow(text, num, flag, flag2, flag3, flag4, flag5);
			}
		}
	}

	private static void ApplyStartedNow(string presetName, int dreamId, bool lvl2, bool lvl3, bool lvl5, bool lvl6, bool lvl7)
	{
		Dreams instance = Singleton<Dreams>.Instance;
		Plugin.Log.LogInfo("[DreamSync] peer começou o sonho '" + presetName + "' - entrando junto e copiando as flags " + $"de nível dele (lvl2={lvl2} lvl3={lvl3} lvl5={lvl5} lvl6={lvl6} lvl7={lvl7}) pra nunca repetir esse sonho depois.");
		instance.hadDreamAtLvl2 = instance.hadDreamAtLvl2 || lvl2;
		instance.hadDreamAtLvl3 = instance.hadDreamAtLvl3 || lvl3;
		instance.hadDreamAtLvl5 = instance.hadDreamAtLvl5 || lvl5;
		instance.hadDreamAtLvl6 = instance.hadDreamAtLvl6 || lvl6;
		instance.hadDreamAtLvl7 = instance.hadDreamAtLvl7 || lvl7;
		_applyingRemote = true;
		((MonoBehaviour)instance).StartCoroutine(instance.prepareDream(presetName, dreamId));
		_applyingRemote = false;
	}

	public static void Tick()
	{
		TickDialoguePending();
		TickChainPending();
	}

	private static void TickDialoguePending()
	{
		if (_pendingPresetName == null)
		{
			_dialogueClosedAt = -1f;
			return;
		}
		DialogueWindow val = (((Object)(object)Singleton<UI>.Instance != (Object)null) ? Singleton<UI>.Instance.dialogueWindow : null);
		if ((Object)(object)val != (Object)null && (val.displayingDialogue || ((Component)val).gameObject.activeSelf))
		{
			_dialogueClosedAt = -1f;
		}
		else if (_dialogueClosedAt < 0f)
		{
			_dialogueClosedAt = Time.unscaledTime;
		}
		else if (!(Time.unscaledTime - _dialogueClosedAt < 0.5f))
		{
			Dreams instance = Singleton<Dreams>.Instance;
			if ((Object)(object)instance != (Object)null && (instance.dreaming || instance.dreamPrepared))
			{
				Plugin.Log.LogInfo($"[DreamSync] diálogo fechou, mas já estou dreaming={instance.dreaming}/dreamPrepared={instance.dreamPrepared} " + "(gatilho local deve ter vencido) - descartando o sonho pendente do peer.");
				_pendingPresetName = null;
				return;
			}
			string pendingPresetName = _pendingPresetName;
			int pendingDreamId = _pendingDreamId;
			bool pendingLvl = _pendingLvl2;
			bool pendingLvl2 = _pendingLvl3;
			bool pendingLvl3 = _pendingLvl5;
			bool pendingLvl4 = _pendingLvl6;
			bool pendingLvl5 = _pendingLvl7;
			_pendingPresetName = null;
			Plugin.Log.LogInfo("[DreamSync] diálogo fechou - retomando o sonho pendente do peer '" + pendingPresetName + "' agora.");
			ApplyStartedNow(pendingPresetName, pendingDreamId, pendingLvl, pendingLvl2, pendingLvl3, pendingLvl4, pendingLvl5);
		}
	}

	private static void TickChainPending()
	{
		if (_pendingChainDreams.Count == 0)
		{
			return;
		}
		DialogueWindow val = (((Object)(object)Singleton<UI>.Instance != (Object)null) ? Singleton<UI>.Instance.dialogueWindow : null);
		if ((Object)(object)val != (Object)null && (val.displayingDialogue || ((Component)val).gameObject.activeSelf))
		{
			return;
		}
		PendingChainDream pendingChainDream = _pendingChainDreams.Dequeue();
		if (pendingChainDream.PresetName == _currentDreamPreset)
		{
			Plugin.Log.LogInfo($"[DreamSync] já estou em '{_currentDreamPreset}' (gatilho nativo local deve ter vencido) - descartando o sonho encadeado pendente '{pendingChainDream.PresetName}' ({_pendingChainDreams.Count} ainda na fila).");
			return;
		}
		Plugin.Log.LogInfo($"[DreamSync] encadeando agora pro sonho do peer '{pendingChainDream.PresetName}' (switchingDream+destroyDream+Player.endDreaming antes do prepareDream, {_pendingChainDreams.Count} ainda na fila).");
		Dreams instance = Singleton<Dreams>.Instance;
		if ((Object)(object)instance != (Object)null)
		{
			instance.switchingDream = true;
			instance.wantToDream = true;
			instance.destroyDream();
		}
		Player.Instance.endDreaming(false);
		ApplyStartedNow(pendingChainDream.PresetName, pendingChainDream.DreamId, pendingChainDream.Lvl2, pendingChainDream.Lvl3, pendingChainDream.Lvl5, pendingChainDream.Lvl6, pendingChainDream.Lvl7);
	}

	private static void BeforeEndDreaming()
	{
		Dreams instance = Singleton<Dreams>.Instance;
		_wasDreamingBeforeEnd = (Object)(object)instance != (Object)null && instance.dreaming;
	}

	private static void AfterEndDreaming()
	{
		if (!_wasDreamingBeforeEnd)
		{
			return;
		}
		PauseSync.ExitDreamSuppression();
		if (!_applyingRemote && CoopRuntime.SessionActive)
		{
			if (_currentDreamPreset == "dream_doctor_02")
			{
				Plugin.Log.LogInfo("[DreamSync] sonho 2 terminou DE VERDADE aqui - NÃO avisando o peer (fim do sonho 2 é individual, cada lado acorda sozinho pelo próprio gatilho nativo).");
				return;
			}
			string text = Singleton<Dreams>.Instance?.outcome ?? "";
			Plugin.Log.LogInfo("[DreamSync] sonho terminou DE VERDADE aqui (outcome='" + text + "') - avisando o peer pra acordar junto.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.DreamEnded).Str(text).Finish());
		}
	}

	public static void ApplyEnded(PacketReader r)
	{
		string text = r.Str();
		Dreams instance = Singleton<Dreams>.Instance;
		if (!((Object)(object)instance == (Object)null) && instance.dreaming)
		{
			Plugin.Log.LogInfo("[DreamSync] peer terminou o sonho de vez (outcome='" + text + "') - acordando junto.");
			_applyingRemote = true;
			instance.outcome = text;
			instance.endDreaming();
			_applyingRemote = false;
		}
	}

	public static void Reset()
	{
		_pendingPresetName = null;
		_dialogueClosedAt = -1f;
		_pendingChainDreams.Clear();
		_currentDreamPreset = null;
	}
}
