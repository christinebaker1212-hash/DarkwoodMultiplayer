using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class NightEventSync
{
	private struct PendingApply
	{
		public string name;

		public Vector3 pos;

		public int type;

		public string value;

		public float expiresAt;
	}

	private const float MatchRadius = 2000f;

	private static bool _patched;

	private static bool _applyingRemote;

	private static bool _bypassClientDefer;

	private static readonly HashSet<string> _claimedByPeer = new HashSet<string>();

	private const float ClientDeferSeconds = 0.4f;

	private static readonly Dictionary<CustomEvent, float> _pendingClientRetry = new Dictionary<CustomEvent, float>();

	private static Vector3? _peerPos;

	private const float ApplyRetryWindow = 30f;

	private static List<PendingApply> _pendingApplies;

	private static readonly HashSet<string> _dumpedOnce = new HashSet<string>();

	private const float ApplyRetryInterval = 0.5f;

	private static float _nextApplyRetryAt;

	public static void SetPeerPosition(Vector3 pos)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		_peerPos = pos;
	}

	private static bool IsCoLocatedWithPeer()
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		if (!_peerPos.HasValue || (Object)(object)Player.Instance == (Object)null)
		{
			return false;
		}
		Location atPos = Location.getAtPos(((Component)Player.Instance).transform.position, false);
		Location atPos2 = Location.getAtPos(_peerPos.Value, false);
		if ((Object)(object)atPos == (Object)null || (Object)(object)atPos2 == (Object)null)
		{
			return false;
		}
		return ((Object)((Component)atPos).gameObject).name == ((Object)((Component)atPos2).gameObject).name;
	}

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Expected O, but got Unknown
		//IL_005d: Expected O, but got Unknown
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(CustomEvent), "fire", (Type[])null, (Type[])null), new HarmonyMethod(typeof(NightEventSync), "BeforeFire", (Type[])null), new HarmonyMethod(typeof(NightEventSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Inventory), "removeItemAmountFromPlayer", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(NightEventSync), "AfterRemoveItemAmount", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterRemoveItemAmount(string type, int removeAmount, int __result)
	{
		if (_applyingRemote && __result < removeAmount)
		{
			Plugin.Log.LogWarning("[NightEventSync][DIAG] durante replay de evento noturno, remoção de item " + $"'{type}' pediu {removeAmount} mas só removeu {__result} - EU não tinha o item todo (histórico de " + "jogo diferente do peer até aqui) - risco real de inventário divergir entre os 2 jogadores. Ainda não corrigido de forma genérica - ver comentário em EnsurePatched.");
		}
	}

	private static bool BeforeFire(CustomEvent __instance)
	{
		if (_applyingRemote || _bypassClientDefer)
		{
			return true;
		}
		if (!CoopRuntime.SessionActive || (Object)(object)__instance == (Object)null)
		{
			return true;
		}
		string name = ((Object)((Component)__instance).gameObject).name;
		if (_claimedByPeer.Contains(name))
		{
			Plugin.Log.LogWarning("[NightEventSync] '" + name + "' tentou disparar NATIVAMENTE, mas o peer já reivindicou esse evento antes (em algum momento da sessão) - bloqueando pra sempre evitar cacofonia duplicada (bug real: sons de terror do Wedding sobrepostos).");
			return false;
		}
		if (CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
		{
			return true;
		}
		if (!IsCoLocatedWithPeer())
		{
			return true;
		}
		if (!_pendingClientRetry.ContainsKey(__instance))
		{
			_pendingClientRetry[__instance] = Time.time + 0.4f;
			Plugin.Log.LogInfo("[NightEventSync] '" + name + "' tentou disparar aqui (cliente, MESMA sala do host agora) - adiando pra dar prioridade determinística ao host.");
		}
		return false;
	}

	public static void Tick()
	{
		TickClientRetry();
		TickApplyRetry();
	}

	private static void TickClientRetry()
	{
		if (_pendingClientRetry.Count == 0)
		{
			return;
		}
		List<CustomEvent> list = null;
		foreach (KeyValuePair<CustomEvent, float> item in _pendingClientRetry)
		{
			if (!((Object)(object)item.Key == (Object)null) && !(Time.time < item.Value))
			{
				if (list == null)
				{
					list = new List<CustomEvent>();
				}
				list.Add(item.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (CustomEvent item2 in list)
		{
			_pendingClientRetry.Remove(item2);
			if (!((Object)(object)item2 == (Object)null))
			{
				string name = ((Object)((Component)item2).gameObject).name;
				if (_claimedByPeer.Contains(name))
				{
					Plugin.Log.LogInfo("[NightEventSync] '" + name + "' - host reivindicou enquanto eu esperava, cancelando meu disparo local.");
					continue;
				}
				Plugin.Log.LogInfo($"[NightEventSync] '{name}' - host não reivindicou em {0.4f:F1}s, " + "disparando aqui mesmo (era só eu, sem corrida).");
				_bypassClientDefer = true;
				item2.fire(true);
				_bypassClientDefer = false;
			}
		}
	}

	private static void TickApplyRetry()
	{
		//IL_0056: Unknown result type (might be due to invalid IL or missing references)
		if (_pendingApplies == null || _pendingApplies.Count == 0 || Time.time < _nextApplyRetryAt)
		{
			return;
		}
		_nextApplyRetryAt = Time.time + 0.5f;
		for (int num = _pendingApplies.Count - 1; num >= 0; num--)
		{
			PendingApply pendingApply = _pendingApplies[num];
			if (TryApplyLocationEvent(pendingApply.name, pendingApply.pos, pendingApply.type, pendingApply.value))
			{
				_pendingApplies.RemoveAt(num);
			}
			else if (Time.time >= pendingApply.expiresAt)
			{
				Plugin.Log.LogWarning($"[NightEventSync] '{pendingApply.name}' (type={(object)(RandomEvent.Type)pendingApply.type}) - tentei por " + $"{30f:F0}s e NUNCA achei o CustomEvent correspondente na minha cena - desistindo " + "de vez (isso não deveria acontecer com o mesmo save/versão - investigar se persistir).");
				_pendingApplies.RemoveAt(num);
			}
		}
	}

	private static void Postfix(CustomEvent __instance, bool force)
	{
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		if (!_applyingRemote && CoopRuntime.SessionActive)
		{
			string name = ((Object)((Component)__instance).gameObject).name;
			Vector3 position = ((Component)__instance).transform.position;
			int num = ((__instance.theEvent == null) ? (-1) : ((int)__instance.theEvent.type));
			string text = ((__instance.theEvent != null) ? __instance.theEvent.Value : null);
			Plugin.Log.LogInfo($"[NightEventSync] '{name}' disparou aqui (pos={position:F0} type={(object)(RandomEvent.Type)num} " + "value='" + text + "') — retransmitindo pro peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.NightEventFire).Str(name).Vec3(position)
				.Int(num)
				.Str(text)
				.Finish());
			PropSpawnSync.NoteEventContext(name);
		}
	}

	public static void ApplyFire(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 pos = r.Vec3();
		int num = r.Int();
		string text2 = r.Str();
		if (!string.IsNullOrEmpty(text))
		{
			_claimedByPeer.Add(text);
		}
		if (num == 700)
		{
			Plugin.Log.LogInfo("[NightEventSync] aplicando '" + text + "' (fireWorldEvent, value='" + text2 + "') via API global.");
			Singleton<Events>.Instance.fireWorldEvent(text2);
		}
		else if (!TryApplyLocationEvent(text, pos, num, text2))
		{
			if (_pendingApplies == null)
			{
				_pendingApplies = new List<PendingApply>();
			}
			_pendingApplies.Add(new PendingApply
			{
				name = text,
				pos = pos,
				type = num,
				value = text2,
				expiresAt = Time.time + 30f
			});
			Plugin.Log.LogInfo("[NightEventSync] '" + text + "' não achado ainda - vou tentar de novo por até " + $"{30f:F0}s (mesmo save/versão, tem que existir do meu lado também).");
		}
	}

	private static bool TryApplyLocationEvent(string name, Vector3 pos, int type, string value)
	{
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0305: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		CustomEvent val = null;
		float num = float.MaxValue;
		List<NightScenario> list = (((Object)(object)Singleton<NightScenarios>.Instance != (Object)null) ? Singleton<NightScenarios>.Instance.scenarios : null);
		if (list != null)
		{
			foreach (NightScenario item in list)
			{
				if ((Object)(object)item == (Object)null)
				{
					continue;
				}
				foreach (NightScenario.CustomEventAndInt customEventAndInt in item.customEventAndInts)
				{
					CustomEvent val2 = customEventAndInt?.customEvent;
					if (!((Object)(object)val2 == (Object)null) && !(((Object)((Component)val2).gameObject).name != name))
					{
						float num2 = Vector3.Distance(((Component)val2).transform.position, pos);
						if (num2 < num)
						{
							num = num2;
							val = val2;
						}
					}
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			CustomEvent[] array = Object.FindObjectsOfType<CustomEvent>(true);
			foreach (CustomEvent val3 in array)
			{
				if (!((Object)(object)val3 == (Object)null) && !(((Object)((Component)val3).gameObject).name != name))
				{
					float num3 = Vector3.Distance(((Component)val3).transform.position, pos);
					if (num3 < num)
					{
						num = num3;
						val = val3;
					}
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			if (!_dumpedOnce.Contains(name))
			{
				_dumpedOnce.Add(name);
				StringBuilder stringBuilder = new StringBuilder();
				CustomEvent[] array = Object.FindObjectsOfType<CustomEvent>(true);
				foreach (CustomEvent val4 in array)
				{
					if ((Object)(object)val4 != (Object)null)
					{
						stringBuilder.Append(((Object)((Component)val4).gameObject).name).Append(", ");
					}
				}
				int num4 = list?.Count ?? (-1);
				Plugin.Log.LogWarning("[NightEventSync][DIAG] procurando '" + name + "' - não achei nem na lista de " + $"cenários ({num4} carregados) nem na cena. CustomEvent na cena agora " + string.Format("({0}): {1}", (stringBuilder.Length > 0) ? "count>0" : "nenhum", stringBuilder));
			}
			return false;
		}
		if (num > 2000f)
		{
			Plugin.Log.LogWarning($"[NightEventSync] '{name}' achado, mas longe DEMAIS (dist={num:F0}, limite=" + $"{2000f:F0}) — provável objeto ERRADO com o mesmo nome (ex. sala diferente com o mesmo " + "gatilho repetido), não o par de verdade - rejeitando (tentar de novo não mudaria nada aqui).");
			return true;
		}
		if (!((Component)val).gameObject.activeSelf)
		{
			((Component)val).gameObject.SetActive(true);
		}
		if (val.started)
		{
			Plugin.Log.LogInfo("[NightEventSync] '" + name + "' já tinha disparado por conta própria aqui — ignorando eco.");
			return true;
		}
		switch (type)
		{
		case 600:
			MarkStartedOnly(val);
			Plugin.Log.LogInfo("[NightEventSync] '" + name + "' (spawnRedneck) — marcado como disparado SEM re-executar (evitaria Redneck duplicado); o personagem real do host já propaga via EnemySync.");
			return true;
		case 0:
		{
			Location atPos = Location.getAtPos(pos, false);
			Location val5 = (((Object)(object)Player.Instance != (Object)null) ? Location.getAtPos(((Component)Player.Instance).transform.position, false) : null);
			if (!((Object)(object)atPos == (Object)null) && (!((Object)(object)val5 != (Object)null) || !(((Object)((Component)val5).gameObject).name == ((Object)((Component)atPos).gameObject).name)))
			{
				Plugin.Log.LogInfo("[NightEventSync] '" + name + "' (locationEvent) - ainda não estou na MESMA sala de onde isso disparou de verdade ('" + ((Object)((Component)atPos).gameObject).name + "') - esperando eu chegar lá antes de aplicar (evita o conteúdo nascer na sala ERRADA, ligada à minha location atual).");
				return false;
			}
			break;
		}
		}
		Plugin.Log.LogInfo($"[NightEventSync] aplicando '{name}' (dist={num:F0}) recebido do peer.");
		PropSpawnSync.NoteEventContext(name);
		_applyingRemote = true;
		val.fire(true);
		_applyingRemote = false;
		return true;
	}

	private static void MarkStartedOnly(CustomEvent ce)
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		ce.timeStarted = Singleton<Controller>.Instance.CurrentTimeAndDay;
		for (int i = 0; i < ce.categories.Count; i++)
		{
			Events instance = Singleton<Events>.Instance;
			CustomEventCategory val = ce.categories[i];
			instance.addCategory(((object)val).ToString());
		}
		if ((Object)(object)Singleton<NightScenarios>.Instance.currentScenario != (Object)null)
		{
			Singleton<NightScenarios>.Instance.currentScenario.currentEvent = ce;
		}
		ce.started = true;
	}

	public static void Reset()
	{
		_claimedByPeer.Clear();
		_pendingClientRetry.Clear();
		_pendingApplies = null;
		_dumpedOnce.Clear();
		_applyingRemote = false;
		_bypassClientDefer = false;
		_peerPos = null;
		_nextApplyRetryAt = 0f;
	}
}
