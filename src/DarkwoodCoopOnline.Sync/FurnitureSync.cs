using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class FurnitureSync
{
	private struct Snap
	{
		public float t;

		public Vector3 p;

		public Quaternion r;
	}

	private class Info
	{
		public string key;

		public Vector3 homePos;

		public float remoteUntil;

		public bool frozen;

		public Vector3 lastPos;

		public bool pushing;

		public bool claimedByMe;

		public readonly List<Snap> buf = new List<Snap>();

		public int settleResendsLeft;

		public float nextSettleResendTime;
	}

	private const float Range = 3000f;

	private const float OwnTimeout = 0.4f;

	private const float InterpDelay = 0.08f;

	private const int MaxPerPacket = 40;

	private const float PushMove = 1f;

	private const float PushRadius = 180f;

	private const int SettleResendCount = 3;

	private const float SettleResendSpacing = 0.3f;

	private static readonly Dictionary<Item, Info> _items = new Dictionary<Item, Info>();

	private static readonly Dictionary<string, Item> _byKey = new Dictionary<string, Item>();

	private static readonly HashSet<Item> _loggedNonDraggableLamp = new HashSet<Item>();

	private const float FallbackRadius = 5f;

	private const float DisplaceThreshold = 10f;

	public static bool Debug = false;

	private static readonly Dictionary<Item, Vector3> _dbgLast = new Dictionary<Item, Vector3>();

	private static Item[] Scene()
	{
		return SceneCache.Items();
	}

	private static Info GetInfo(Item it)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		if (!_items.TryGetValue(it, out var value))
		{
			Vector3 position = ((Component)it).transform.position;
			value = new Info
			{
				homePos = position,
				key = it.name + "@" + Key(position),
				lastPos = position
			};
			_items[it] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = it;
			}
		}
		return value;
	}

	public static void PreRegisterAll()
	{
		int num = 0;
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if (!((Object)(object)val == (Object)null) && val.draggable)
			{
				GetInfo(val);
				num++;
			}
		}
		Plugin.Log.LogInfo($"[FurnitureSync] pré-registrados {num} móvel(is) no carregamento (homePos capturado antes de qualquer interação).");
	}

	public static byte[] PollDrags(Vector3 p1, Vector3 p2, bool isHost)
	{
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0086: Unknown result type (might be due to invalid IL or missing references)
		//IL_0093: Unknown result type (might be due to invalid IL or missing references)
		//IL_0098: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_03b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_033e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0312: Unknown result type (might be due to invalid IL or missing references)
		List<Item> list = null;
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			if (val.switchable && !val.draggable && !_loggedNonDraggableLamp.Contains(val))
			{
				_loggedNonDraggableLamp.Add(val);
				Plugin.Log.LogWarning("[FurnitureSync][diag] lâmpada '" + val.name + "' é switchable MAS NÃO draggable - FurnitureSync NUNCA vai rastrear a posição dela (LampSync assume que sim - suposição errada pra esta).");
			}
			if (!val.draggable || Mathf.Min(Vector3.Distance(((Component)val).transform.position, p1), Vector3.Distance(((Component)val).transform.position, p2)) > 3000f)
			{
				continue;
			}
			Info info = GetInfo(val);
			bool flag = Time.time < info.remoteUntil;
			Vector3 position = ((Component)val).transform.position;
			Vector3 val2 = position - info.lastPos;
			bool flag2 = val2.magnitude > 1f;
			bool flag3 = (Object)(object)Player.Instance != (Object)null && Vector3.Distance(position, ((Component)Player.Instance).transform.position) < 180f;
			info.lastPos = position;
			bool num = val.beingDragged && (!flag || isHost);
			bool flag4 = (info.pushing = (info.pushing ? (!flag && !val.beingDragged && flag3) : (!flag && !val.beingDragged && !info.frozen && flag2 && flag3)));
			bool claimedByMe = info.claimedByMe;
			if (num || flag4)
			{
				if (info.frozen)
				{
					Freeze(val, freeze: false);
					info.frozen = false;
					info.buf.Clear();
				}
				if (val.switchable)
				{
					Plugin.Log.LogInfo("[FurnitureSync][diag] lâmpada '" + val.name + "' RASTREADA e sendo mandada " + $"pro peer agora (drag={val.beingDragged} push={flag4} pos={position:F0}).");
				}
				info.claimedByMe = true;
				info.settleResendsLeft = 0;
				if (list == null)
				{
					list = new List<Item>();
				}
				list.Add(val);
				if (list.Count >= 40)
				{
					break;
				}
			}
			else
			{
				if (flag)
				{
					info.claimedByMe = false;
					if (!info.frozen)
					{
						Freeze(val, freeze: true);
						info.frozen = true;
					}
				}
				else
				{
					info.claimedByMe = false;
					if (info.frozen)
					{
						Freeze(val, freeze: false);
						info.frozen = false;
						info.buf.Clear();
					}
				}
				if (claimedByMe)
				{
					info.settleResendsLeft = 3;
					info.nextSettleResendTime = Time.time;
				}
				if (info.settleResendsLeft > 0 && Time.time >= info.nextSettleResendTime)
				{
					info.settleResendsLeft--;
					info.nextSettleResendTime = Time.time + 0.3f;
					if (list == null)
					{
						list = new List<Item>();
					}
					list.Add(val);
					Plugin.Log.LogInfo($"[FurnitureSync] reenvio de acomodação (restam {info.settleResendsLeft}): '{val.name}' pos={position:F0}.");
					if (list.Count >= 40)
					{
						break;
					}
				}
			}
			if (Debug && flag3)
			{
				FurnitureDiagnostics.Record(val.name, position, info.frozen, flag, flag2, info.claimedByMe, val.beingDragged);
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.FurnitureDelta).UShort((ushort)list.Count);
		foreach (Item item in list)
		{
			packetWriter.Str(GetInfo(item).key).Vec2XZ(((Component)item).transform.position).Quat(((Component)item).transform.rotation);
		}
		return packetWriter.Finish();
	}

	public static void ApplyDrags(PacketReader r, bool isHost)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0022: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_007d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0129: Unknown result type (might be due to invalid IL or missing references)
		//IL_012b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0132: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		int num = r.UShort();
		Vector3 p = default(Vector3);
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			Vector3 val = r.Vec2XZ(0f);
			Quaternion r2 = r.Quat();
			Item val2 = FindByKey(text);
			if ((Object)(object)val2 == (Object)null)
			{
				Plugin.Log.LogWarning("[FurnitureSync][diag] recebi posição pro key='" + text + "' mas não achei NENHUM objeto draggable correspondente aqui - descartado.");
				continue;
			}
			Info info = GetInfo(val2);
			if (val2.switchable)
			{
				Plugin.Log.LogInfo($"[FurnitureSync][diag] lâmpada '{val2.name}' recebida do peer, pos={val:F0} " + $"claimedByMe={info.claimedByMe} isHost={isHost}.");
			}
			if (!isHost || !info.claimedByMe)
			{
				info.remoteUntil = Time.time + 0.4f;
				if (!info.frozen)
				{
					Freeze(val2, freeze: true);
					info.frozen = true;
				}
				p = new Vector3(val.x, ((Component)val2).transform.position.y, val.z);
				info.buf.Add(new Snap
				{
					t = Time.time,
					p = p,
					r = r2
				});
				while (info.buf.Count > 0 && Time.time - info.buf[0].t > 0.5f)
				{
					info.buf.RemoveAt(0);
				}
			}
		}
	}

	public static void Tick()
	{
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_019c: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		float num = Time.time - 0.08f;
		foreach (KeyValuePair<Item, Info> item in _items)
		{
			Item key = item.Key;
			Info value = item.Value;
			if ((Object)(object)key == (Object)null || !value.frozen || Time.time >= value.remoteUntil)
			{
				continue;
			}
			List<Snap> buf = value.buf;
			if (buf.Count == 0)
			{
				continue;
			}
			if (buf.Count == 1 || num <= buf[0].t)
			{
				((Component)key).transform.position = buf[0].p;
				((Component)key).transform.rotation = buf[0].r;
				continue;
			}
			if (num >= buf[buf.Count - 1].t)
			{
				Snap snap = buf[buf.Count - 1];
				((Component)key).transform.position = snap.p;
				((Component)key).transform.rotation = snap.r;
				continue;
			}
			for (int i = 0; i < buf.Count - 1; i++)
			{
				if (num >= buf[i].t && num <= buf[i + 1].t)
				{
					float num2 = buf[i + 1].t - buf[i].t;
					float num3 = ((num2 > 0.0001f) ? ((num - buf[i].t) / num2) : 1f);
					((Component)key).transform.position = Vector3.Lerp(buf[i].p, buf[i + 1].p, num3);
					((Component)key).transform.rotation = Quaternion.Slerp(buf[i].r, buf[i + 1].r, num3);
					break;
				}
			}
		}
	}

	private static Item FindByKey(string key)
	{
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		if (_byKey.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if (!((Object)(object)val == (Object)null) && val.draggable && GetInfo(val).key == key)
			{
				return val;
			}
		}
		if (SyncKeyUtil.TryParsePos(key, out var pos))
		{
			Item val2 = null;
			float num = 5f;
			array = Scene();
			foreach (Item val3 in array)
			{
				if (!((Object)(object)val3 == (Object)null) && val3.draggable)
				{
					float num2 = Vector3.Distance(((Component)val3).transform.position, pos);
					if (num2 < num)
					{
						num = num2;
						val2 = val3;
					}
				}
			}
			if ((Object)(object)val2 != (Object)null)
			{
				_byKey[key] = val2;
				Plugin.Log.LogWarning("[FurnitureSync] chave '" + key + "' não bateu exata - achei o móvel por RAIO " + $"(dist={num:F1}) e aliei - chaves divergiram entre os 2 lados (ver SyncKeyUtil).");
				return val2;
			}
		}
		return null;
	}

	private static void Freeze(Item it, bool freeze)
	{
		Rigidbody2D component = ((Component)it).GetComponent<Rigidbody2D>();
		if ((Object)(object)component != (Object)null)
		{
			component.isKinematic = freeze;
		}
		Rigidbody component2 = ((Component)it).GetComponent<Rigidbody>();
		if ((Object)(object)component2 != (Object)null)
		{
			component2.isKinematic = freeze;
		}
	}

	private static string Key(Vector3 p)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void ForceRebroadcast()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		List<Item> list = null;
		foreach (KeyValuePair<Item, Info> item in _items)
		{
			Item key = item.Key;
			Info value = item.Value;
			if (!((Object)(object)key == (Object)null) && !(Vector3.Distance(((Component)key).transform.position, value.homePos) < 10f))
			{
				if (list == null)
				{
					list = new List<Item>();
				}
				list.Add(key);
			}
		}
		if (list == null)
		{
			return;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.FurnitureDelta).UShort((ushort)list.Count);
		foreach (Item item2 in list)
		{
			packetWriter.Str(GetInfo(item2).key).Vec2XZ(((Component)item2).transform.position).Quat(((Component)item2).transform.rotation);
		}
		CoopRuntime.Session?.SendReliable(packetWriter.Finish());
		Plugin.Log.LogInfo($"[FurnitureSync] reconciliação no Start: reenviando posição de {list.Count} móvel(is) deslocado(s).");
	}

	public static void DebugTick()
	{
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_014a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_0082: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		if (!Debug || (Object)(object)Player.Instance == (Object)null)
		{
			return;
		}
		Vector3 position = ((Component)Player.Instance).transform.position;
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			float num = Vector3.Distance(((Component)val).transform.position, position);
			if (num > 350f)
			{
				continue;
			}
			Vector3 position2 = ((Component)val).transform.position;
			if (_dbgLast.TryGetValue(val, out var value))
			{
				Vector3 val2 = position2 - value;
				if (val2.sqrMagnitude > 1f)
				{
					Plugin.Log.LogInfo($"[FurnDbg] '{val.name}' drag={val.draggable} beingDragged={val.beingDragged} " + string.Format("dropped={0} dist={1:F0} pos={2} ", val.isDroppedItem, num, position2.ToString("F0")) + $"rotY={((Component)val).transform.eulerAngles.y:F0} rb2d={(Object)(object)((Component)val).GetComponent<Rigidbody2D>() != (Object)null} " + $"rb3d={(Object)(object)((Component)val).GetComponent<Rigidbody>() != (Object)null}");
				}
			}
			_dbgLast[val] = position2;
		}
	}

	public static void Reset()
	{
		foreach (KeyValuePair<Item, Info> item in _items)
		{
			if ((Object)(object)item.Key != (Object)null && item.Value.frozen)
			{
				Freeze(item.Key, freeze: false);
			}
		}
		_items.Clear();
		_byKey.Clear();
		_loggedNonDraggableLamp.Clear();
	}
}
