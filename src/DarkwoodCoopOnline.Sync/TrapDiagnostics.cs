using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class TrapDiagnostics
{
	private const float SearchRadius = 500f;

	private static Item _item;

	private static Trigger _trigger;

	private static bool _streaming;

	private static float _lastLog = -999f;

	private const float LogInterval = 0.125f;

	public static void Capture()
	{
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogInfo("[TrapDiag] Player null");
			return;
		}
		Item val = null;
		Trigger val2 = null;
		float num = 500f;
		Item[] array = Object.FindObjectsOfType<Item>();
		foreach (Item val3 in array)
		{
			if ((Object)(object)val3 == (Object)null)
			{
				continue;
			}
			Trigger component = ((Component)val3).GetComponent<Trigger>();
			if (!((Object)(object)component == (Object)null))
			{
				float num2 = Vector3.Distance(((Component)val3).transform.position, ((Component)instance).transform.position);
				if (num2 < num)
				{
					num = num2;
					val = val3;
					val2 = component;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[TrapDiag] nenhum Item com Trigger na cena por perto");
			return;
		}
		_item = val;
		_trigger = val2;
		Plugin.Log.LogInfo("========================= [TrapDiag] CAPTURA =========================");
		Plugin.Log.LogInfo($"[TrapDiag] alvo a {num:F0} do Player: name='{val.name}'");
		DumpItemFields(val);
		DumpTriggerFields(val2);
		DumpHierarchy(((Component)val).transform, 0);
		Plugin.Log.LogInfo("[TrapDiag] alvo travado. PageDown = começar/parar streaming por frame.");
		Plugin.Log.LogInfo("========================================================================");
	}

	private static void DumpItemFields(Item it)
	{
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_0155: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		Plugin.Log.LogInfo("[TrapDiag] --- campos do Item ---");
		Plugin.Log.LogInfo($"[TrapDiag]   disabled={it.disabled} destroyed={it.destroyed} isSelectable={it.isSelectable} " + $"health={it.health}/{it.maxHealth} draggable={it.draggable}");
		Plugin.Log.LogInfo($"[TrapDiag]   doesNotNeedItemToDisarm={it.doesNotNeedItemToDisarm} disarmChance={it.disarmChance:F2} " + string.Format("disarmTime={0:F2} transformDisarmingMe={1}", it.disarmTime, ((Object)(object)it.transformDisarmingMe != (Object)null) ? ((Object)it.transformDisarmingMe).name : "null"));
		string text = (((Object)(object)it.animator != (Object)null && it.animator.CurrentClip != null) ? it.animator.CurrentClip.name : "-");
		TimestampedLogSource log = Plugin.Log;
		string[] obj = new string[6] { "[TrapDiag]   animator.clip=", text, " wpos=", null, null, null };
		Vector3 val = ((Component)it).transform.position;
		obj[3] = val.ToString("F1");
		obj[4] = " wrot=";
		Quaternion rotation = ((Component)it).transform.rotation;
		val = rotation.eulerAngles;
		obj[5] = val.ToString("F1");
		log.LogInfo(string.Concat(obj));
	}

	private static void DumpTriggerFields(Trigger t)
	{
		Plugin.Log.LogInfo("[TrapDiag] --- campos do Trigger ---");
		Plugin.Log.LogInfo($"[TrapDiag]   active={t.active} triggered={t.triggered} canDisarm={t.canDisarm} " + $"isBearTrap={t.isBearTrap} isChainTrap={t.isChainTrap}");
		Plugin.Log.LogInfo($"[TrapDiag]   staysAfterTriggering={t.staysAfterTriggering} staysAfterDisarming={t.staysAfterDisarming} " + $"triggerOnDisarmAttempt={t.triggerOnDisarmAttempt} bendDownToDisarm={t.bendDownToDisarm}");
		Plugin.Log.LogInfo($"[TrapDiag]   affectsPlayer={t.affectsPlayer} affectsCharacters={t.affectsCharacters} " + $"multipleTrigger={t.multipleTrigger} setByPlayer={t.setByPlayer}");
		Plugin.Log.LogInfo($"[TrapDiag]   triggeredState='{t.triggeredState}' triggeredStateFromAnim={t.triggeredStateFromAnim} " + "triggeredName='" + t.triggeredName + "'");
	}

	private static void DumpHierarchy(Transform tr, int depth)
	{
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		List<string> list = new List<string>();
		Component[] components = ((Component)tr).GetComponents<Component>();
		foreach (Component val in components)
		{
			if ((Object)(object)val != (Object)null && !(val is Transform))
			{
				list.Add(((object)val).GetType().Name);
			}
		}
		string text = new string(' ', depth * 2);
		Plugin.Log.LogInfo(string.Format("[TrapDiag]   {0}'{1}' [{2}] active={3}", text, ((Object)tr).name, string.Join(",", list.ToArray()), ((Component)tr).gameObject.activeSelf));
		if (depth >= 3)
		{
			return;
		}
		foreach (Transform item in tr)
		{
			DumpHierarchy(item, depth + 1);
		}
	}

	public static void ToggleStream()
	{
		if ((Object)(object)_item == (Object)null || (Object)(object)_trigger == (Object)null)
		{
			Plugin.Log.LogInfo("[TrapDiag] sem alvo — Delete perto da armadilha primeiro.");
			return;
		}
		_streaming = !_streaming;
		Plugin.Log.LogInfo($"[TrapDiag] streaming = {_streaming}");
		if (_streaming)
		{
			Plugin.Log.LogInfo("[TrapDiag] t | active | triggered | disabled | destroyed | health | clip | pos");
		}
	}

	public static void Tick()
	{
		//IL_00f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fb: Unknown result type (might be due to invalid IL or missing references)
		if (_streaming && !((Object)(object)_item == (Object)null) && !((Object)(object)_trigger == (Object)null) && !(Time.time - _lastLog < 0.125f))
		{
			_lastLog = Time.time;
			Item item = _item;
			Trigger trigger = _trigger;
			string text = (((Object)(object)item.animator != (Object)null && item.animator.CurrentClip != null) ? item.animator.CurrentClip.name : "-");
			TimestampedLogSource log = Plugin.Log;
			string text2 = $"[TrapWatch] t={Time.time:F2} active={trigger.active} triggered={trigger.triggered} ";
			string text3 = $"disabled={item.disabled} destroyed={item.destroyed} health={item.health} clip={text} ";
			Vector3 position = ((Component)item).transform.position;
			log.LogInfo(text2 + text3 + "pos=" + position.ToString("F1"));
		}
	}

	public static void Reset()
	{
		_item = null;
		_trigger = null;
		_streaming = false;
	}
}
