using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class ThrownItemSync
{
	private const float SettleTimeout = 4f;

	private static bool _patched;

	private static readonly List<(ThrownItem item, float since)> _pending = new List<(ThrownItem, float)>();

	private static readonly HashSet<ThrownItem> _known = new HashSet<ThrownItem>();

	private static bool _suppressNextAwake;

	private static readonly Dictionary<ThrownItem, string> _typeByItem = new Dictionary<ThrownItem, string>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(ThrownItem), "Awake", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(ThrownItemSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(ThrownItem), "onCollide", new Type[2]
			{
				typeof(Collider),
				typeof(Vector3)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(ThrownItemSync), "CollidePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void CollidePostfix(ThrownItem __instance)
	{
		if ((Object)(object)__instance == (Object)null)
		{
			return;
		}
		int num = -1;
		for (int i = 0; i < _pending.Count; i++)
		{
			if (_pending[i].item == __instance)
			{
				num = i;
				break;
			}
		}
		if (num < 0)
		{
			return;
		}
		_pending.RemoveAt(num);
		_known.Add(__instance);
		string value;
		string text = (_typeByItem.TryGetValue(__instance, out value) ? value : "");
		_typeByItem.Remove(__instance);
		if (!string.IsNullOrEmpty(text) && CoopRuntime.SessionActive)
		{
			Vector3 position = ((Component)__instance).transform.position;
			Quaternion rotation = ((Component)__instance).transform.rotation;
			Plugin.Log.LogInfo($"[ThrownItemSync] '{text}' colidiu de verdade agora - anunciando pos={position:F0} (sem esperar poll/timeout).");
			PacketWriter packetWriter = PacketWriter.Begin(PacketType.ThrownItemLanded).UShort(1);
			packetWriter.Vec3(position).Quat(rotation).Str(text);
			Inventory component = ((Component)__instance).GetComponent<Inventory>();
			InvItemClass val = (((Object)(object)component != (Object)null && component.slots.Count > 0) ? component.slots[0].invItem : null);
			bool flag = (Object)(object)component != (Object)null && component.slots.Count > 0 && !InvItemClass.isNull(val);
			packetWriter.Bool(flag);
			if (flag)
			{
				packetWriter.Str(val.type).Int(val.amount).Float(val.durability);
			}
			CoopRuntime.Session.SendReliable(packetWriter.Finish());
		}
	}

	private static void Postfix(ThrownItem __instance)
	{
		if (_suppressNextAwake)
		{
			_suppressNextAwake = false;
			if ((Object)(object)__instance != (Object)null)
			{
				_known.Add(__instance);
			}
		}
		else if (CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null) && !_known.Contains(__instance) && !((Object)(object)((Component)__instance).GetComponent<Flare>() != (Object)null))
		{
			InvItemClass val = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.currentItem : null);
			if (!InvItemClass.isNull(val) && !((Object)(object)val.baseClass == (Object)null) && val.baseClass.isThrowable)
			{
				_typeByItem[__instance] = val.type;
				_pending.Add((__instance, Time.time));
			}
		}
	}

	public static byte[] PollSettled()
	{
		if (_pending.Count == 0)
		{
			return null;
		}
		List<(ThrownItem, string)> list = null;
		for (int num = _pending.Count - 1; num >= 0; num--)
		{
			var (val, num2) = _pending[num];
			if ((Object)(object)val == (Object)null)
			{
				_pending.RemoveAt(num);
			}
			else if (!val.thrown)
			{
				_pending[num] = (val, Time.time);
			}
			else if (val.onGround || Time.time - num2 > 4f)
			{
				_pending.RemoveAt(num);
				_known.Add(val);
				string value;
				string text = (_typeByItem.TryGetValue(val, out value) ? value : "");
				_typeByItem.Remove(val);
				if (!string.IsNullOrEmpty(text))
				{
					if (list == null)
					{
						list = new List<(ThrownItem, string)>();
					}
					list.Add((val, text));
				}
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.ThrownItemLanded).UShort((ushort)list.Count);
		Vector3 val2 = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.position : Vector3.zero);
		foreach (var (val3, text2) in list)
		{
			if (TimestampedLogSource.Verbose)
			{
				Plugin.Log.LogInfo($"[ThrownItemSync][diag] MINHA pos jogador={val2:F0} vs pos anunciada do item={((Component)val3).transform.position:F0} (delta={Vector3.Distance(val2, ((Component)val3).transform.position):F0}) onGround={val3.onGround}");
			}
			Plugin.Log.LogInfo($"[ThrownItemSync] '{text2}' assentou - anunciando pos={((Component)val3).transform.position:F0}.");
			packetWriter.Vec3(((Component)val3).transform.position).Quat(((Component)val3).transform.rotation).Str(text2);
			Inventory component = ((Component)val3).GetComponent<Inventory>();
			InvItemClass val4 = (((Object)(object)component != (Object)null && component.slots.Count > 0) ? component.slots[0].invItem : null);
			bool flag = (Object)(object)component != (Object)null && component.slots.Count > 0 && !InvItemClass.isNull(val4);
			packetWriter.Bool(flag);
			if (flag)
			{
				packetWriter.Str(val4.type).Int(val4.amount).Float(val4.durability);
			}
		}
		return packetWriter.Finish();
	}

	public static void ApplyLanded(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			Vector3 val = r.Vec3();
			Quaternion val2 = r.Quat();
			string text = r.Str();
			bool flag = r.Bool();
			string text2 = (flag ? r.Str() : null);
			int num2 = (flag ? r.Int() : 0);
			float absoluteDurability = (flag ? r.Float() : 0f);
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			InvItem val3 = (((Object)(object)Singleton<ItemsDatabase>.Instance != (Object)null) ? Singleton<ItemsDatabase>.Instance.getItem(text, false) : null);
			Object val4 = (((Object)(object)val3 != (Object)null) ? val3.item : null);
			if (val4 == (Object)null)
			{
				Plugin.Log.LogWarning("[ThrownItemSync] falhou ao resolver prefab '" + text + "' na ItemsDatabase.");
				continue;
			}
			if (TimestampedLogSource.Verbose)
			{
				Plugin.Log.LogInfo($"[ThrownItemSync][diag] resolvido prefab '{val4}' pro tipo '{text}' (hasItem={flag} " + "itType='" + text2 + "').");
			}
			_suppressNextAwake = true;
			GameObject val5 = Core.AddPrefab(val4, val, val2, (GameObject)null, true);
			_suppressNextAwake = false;
			ThrownItem val6 = (((Object)(object)val5 != (Object)null) ? val5.GetComponent<ThrownItem>() : null);
			if ((Object)(object)val6 == (Object)null)
			{
				Plugin.Log.LogWarning("[ThrownItemSync] criei '" + text + "' mas sem componente ThrownItem (go=" + (((Object)(object)val5 != (Object)null) ? ((Object)val5).name : "NULL") + ").");
				continue;
			}
			if (flag)
			{
				Inventory component = val5.GetComponent<Inventory>();
				if ((Object)(object)component != (Object)null && component.slots.Count > 0)
				{
					component.slots[0].createItem(text2, num2, ItemDurability.ToFraction(text2, absoluteDurability), (InvItem.ModifierQuality)0, false);
				}
			}
			val6.damage /= 2;
			Explodes component2 = val5.GetComponent<Explodes>();
			if ((Object)(object)component2 != (Object)null)
			{
				component2.damage /= 2f;
			}
			val6.thrown = true;
			val6.onCollide((Collider)null, val);
			_known.Add(val6);
			ThrownItem rbCleanupItem = val6;
			GameObject rbCleanupGo = val5;
			Singleton<Controller>.Instance.waitFramesAndRun((Action)delegate
			{
				if (!((Object)(object)rbCleanupItem == (Object)null) && !((Object)(object)rbCleanupGo == (Object)null))
				{
					Rigidbody component3 = rbCleanupGo.GetComponent<Rigidbody>();
					if ((Object)(object)component3 != (Object)null)
					{
						component3.velocity = Vector3.zero;
					}
					rbCleanupItem.onGround = true;
					rbCleanupItem.thrown = false;
				}
			}, 2);
			Plugin.Log.LogInfo($"[ThrownItemSync] '{text}' do peer replicado e detonado aqui, pos={val:F0} (dano cortado pela metade).");
		}
	}

	public static void Reset()
	{
		_pending.Clear();
		_known.Clear();
		_suppressNextAwake = false;
		_typeByItem.Clear();
	}
}
