using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class FlareSync
{
	private const float SettleTimeout = 3f;

	private static bool _patched;

	private static readonly List<(Flare flare, float since)> _pending = new List<(Flare, float)>();

	private static readonly HashSet<Flare> _known = new HashSet<Flare>();

	private static readonly Dictionary<Flare, string> _typeByFlare = new Dictionary<Flare, string>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Flare), "Start", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(FlareSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(Flare __instance)
	{
		if (CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null) && !_known.Contains(__instance))
		{
			_pending.Add((__instance, Time.time));
			InvItemClass val = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.currentItem : null);
			if (!InvItemClass.isNull(val))
			{
				_typeByFlare[__instance] = val.type;
			}
		}
	}

	public static byte[] PollSettled()
	{
		if (_pending.Count == 0)
		{
			return null;
		}
		List<(Vector3, string)> list = null;
		for (int num = _pending.Count - 1; num >= 0; num--)
		{
			var (val, num2) = _pending[num];
			if ((Object)(object)val == (Object)null)
			{
				_pending.RemoveAt(num);
			}
			else if ((Object)(object)((Component)val).transform.parent != (Object)null)
			{
				_pending[num] = (val, Time.time);
			}
			else
			{
				ThrownItem component = ((Component)val).GetComponent<ThrownItem>();
				Rigidbody component2 = ((Component)val).GetComponent<Rigidbody>();
				if (((Object)(object)component != (Object)null && component.onGround) || ((Object)(object)component2 != (Object)null && component2.IsSleeping()) || Time.time - num2 > 3f)
				{
					_pending.RemoveAt(num);
					_known.Add(val);
					string value;
					string item = (_typeByFlare.TryGetValue(val, out value) ? value : "");
					_typeByFlare.Remove(val);
					if (list == null)
					{
						list = new List<(Vector3, string)>();
					}
					list.Add((((Component)val).transform.position, item));
				}
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.FlareThrown).UShort((ushort)list.Count);
		Vector3 val2 = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.position : Vector3.zero);
		foreach (var (val3, text) in list)
		{
			Plugin.Log.LogInfo($"[FlareSync][diag] MINHA pos jogador={val2:F0} vs pos anunciada do flare={val3:F0} (delta={Vector3.Distance(val2, val3):F0})");
			Plugin.Log.LogInfo($"[FlareSync] flare assentado - anunciando: type='{text}' pos={val3:F0}");
			packetWriter.Vec2XZ(val3).Str(text);
		}
		return packetWriter.Finish();
	}

	public static void ApplyThrown(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			Vector3 val = r.Vec2XZ(0f);
			string text = r.Str();
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			InvItem val2 = (((Object)(object)Singleton<ItemsDatabase>.Instance != (Object)null) ? Singleton<ItemsDatabase>.Instance.getItem(text, false) : null);
			Object val3 = (((Object)(object)val2 != (Object)null) ? val2.item : null);
			if (val3 == (Object)null)
			{
				Plugin.Log.LogWarning("[FlareSync] falhou ao resolver prefab do flare '" + text + "' na ItemsDatabase.");
				continue;
			}
			GameObject val4 = Core.AddPrefab(val3, val, Quaternion.Euler(90f, 0f, 0f), (GameObject)null, true);
			Flare val5 = (((Object)(object)val4 != (Object)null) ? val4.GetComponent<Flare>() : null);
			if ((Object)(object)val5 == (Object)null)
			{
				Plugin.Log.LogWarning("[FlareSync] criei '" + text + "' mas o objeto não tem componente Flare (go=" + (((Object)(object)val4 != (Object)null) ? ((Object)val4).name : "NULL") + ").");
			}
			else
			{
				_known.Add(val5);
				Rigidbody component = ((Component)val5).GetComponent<Rigidbody>();
				if ((Object)(object)component != (Object)null)
				{
					component.isKinematic = true;
				}
				Plugin.Log.LogInfo($"[FlareSync] flare criado a partir do anúncio do peer: type='{text}' pos={val:F0}");
			}
		}
	}

	public static void Reset()
	{
		_pending.Clear();
		_known.Clear();
		_typeByFlare.Clear();
	}
}
