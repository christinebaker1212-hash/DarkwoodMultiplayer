using System.Collections.Generic;
using System.Text;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ContainerSync
{
	public struct SlotState
	{
		public string type;

		public int amount;

		public float durability;

		public int ammo;
	}

	private class Info
	{
		public string key;

		public bool everSynced;

		public string lastSyncedSig = "";

		public bool networkTouched;

		public string lastRequestedSig = "";

		public float lastRequestSentAt = -999f;

		public int version;
	}

	private const float Range = 4000f;

	private const float ClientRequestCooldown = 1f;

	private static readonly Dictionary<Inventory, Info> _containers = new Dictionary<Inventory, Info>();

	private static readonly Dictionary<string, Inventory> _byKey = new Dictionary<string, Inventory>();

	private const string CorpseKeyPrefix = "corpse:";

	private const float PeriodicReconcileInterval = 5f;

	private static float _nextReconcileAt;

	private const float CorpseFallbackRadius = 20f;

	private static bool IsContainerType(Inventory.InvType t)
	{
		if ((int)t != 0)
		{
			return (int)t == 60;
		}
		return true;
	}

	private static string ComputeKey(Item item, Inventory inv)
	{
		if ((int)inv.invType == 60)
		{
			Character component = ((Component)item).GetComponent<Character>();
			string text = (((Object)(object)component != (Object)null) ? EnemySync.ResolveOwnedId(component) : null);
			if (!string.IsNullOrEmpty(text))
			{
				return "corpse:" + text;
			}
		}
		return item.name + "@" + Key(((Component)item).transform.position);
	}

	private static Info GetInfo(Item item, Inventory inv)
	{
		if (!_containers.TryGetValue(inv, out var value))
		{
			value = new Info
			{
				key = ComputeKey(item, inv)
			};
			_containers[inv] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = inv;
			}
		}
		return value;
	}

	private static string Key(Vector3 p)
	{
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	private static List<SlotState> Snapshot(Inventory inv)
	{
		List<SlotState> list = new List<SlotState>(inv.slots.Count);
		foreach (InvSlot slot in inv.slots)
		{
			InvItemClass invItem = slot.invItem;
			list.Add(InvItemClass.isNull(invItem) ? new SlotState
			{
				type = "",
				amount = 0,
				durability = 0f,
				ammo = 0
			} : new SlotState
			{
				type = invItem.type,
				amount = invItem.amount,
				durability = invItem.durability,
				ammo = invItem.ammo
			});
		}
		return list;
	}

	private static string Sig(List<SlotState> s)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (SlotState item in s)
		{
			StringBuilder stringBuilder2 = stringBuilder.Append(item.type).Append('|').Append(item.amount)
				.Append('|');
			float durability = item.durability;
			stringBuilder2.Append(durability.ToString("F1")).Append('|').Append(item.ammo)
				.Append(';');
		}
		return stringBuilder.ToString();
	}

	public static List<byte[]> PollChanges(Vector3 p1, Vector3 p2, bool isHost)
	{
		List<byte[]> list = null;
		Item[] array = SceneCache.Items();
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			Inventory component = ((Component)val).GetComponent<Inventory>();
			if ((Object)(object)component == (Object)null || !IsContainerType(component.invType) || CoopPeers.MinDistance(((Component)val).transform.position, p1) > 4000f)
			{
				continue;
			}
			Info info = GetInfo(val, component);
			List<SlotState> list2 = Snapshot(component);
			string text = Sig(list2);
			if (!info.everSynced)
			{
				if ((int)component.invType == 0)
				{
					info.everSynced = true;
					info.lastSyncedSig = text;
					continue;
				}
				Character component2 = ((Component)val).GetComponent<Character>();
				if ((Object)(object)component2 != (Object)null && EnemySync.ResolveOwnedId(component2) != null)
				{
					info.everSynced = true;
					info.lastSyncedSig = text;
					info.networkTouched = true;
					info.version = 1;
					Plugin.Log.LogInfo("[ContainerSync] empurro corpo novo que eu possuo: key='" + info.key + "'");
					if (list == null)
					{
						list = new List<byte[]>();
					}
					list.Add(Build(info.key, list2, info.version));
				}
			}
			else
			{
				if (text == info.lastSyncedSig)
				{
					continue;
				}
				if (isHost)
				{
					info.lastSyncedSig = text;
					info.networkTouched = true;
					info.version++;
					Plugin.Log.LogInfo($"[ContainerSync] HOST mudou localmente, broadcast: key='{info.key}' versao={info.version}");
				}
				else
				{
					bool num = text != info.lastRequestedSig;
					bool flag = Time.time - info.lastRequestSentAt >= 1f;
					if (!num && !flag)
					{
						continue;
					}
					info.lastRequestedSig = text;
					info.lastRequestSentAt = Time.time;
					info.networkTouched = true;
					Plugin.Log.LogInfo($"[ContainerSync] CLIENT mudou localmente, pedido ao host: key='{info.key}' versaoConhecida={info.version}");
				}
				if (list == null)
				{
					list = new List<byte[]>();
				}
				list.Add(Build(info.key, list2, info.version));
			}
		}
		if (Time.time >= _nextReconcileAt)
		{
			_nextReconcileAt = Time.time + 5f;
			int num2 = 0;
			foreach (KeyValuePair<Inventory, Info> container in _containers)
			{
				Inventory key = container.Key;
				Info value = container.Value;
				if (!((Object)(object)key == (Object)null) && value.networkTouched)
				{
					CoopRuntime.Session?.SendReliable(Build(value.key, Snapshot(key), value.version));
					num2++;
				}
			}
			if (num2 > 0)
			{
				Plugin.Log.LogInfo($"[ContainerSync] reconciliação periódica: reenviando {num2} container(es) já tocados pela rede (rede de segurança contra pacote perdido).");
			}
		}
		return list;
	}

	public static byte[] ApplyIncoming(PacketReader r, bool isHost)
	{
		string text = r.Str();
		int num = r.Int();
		int num2 = r.UShort();
		List<SlotState> list = new List<SlotState>(num2);
		for (int i = 0; i < num2; i++)
		{
			list.Add(new SlotState
			{
				type = r.Str(),
				amount = r.Int(),
				durability = r.Float(),
				ammo = r.Int()
			});
		}
		Inventory val = FindByKey(text);
		if ((Object)(object)val == (Object)null)
		{
			return null;
		}
		Info value;
		Info info = (_containers.TryGetValue(val, out value) ? value : new Info
		{
			key = text
		});
		_containers[val] = info;
		if (!isHost)
		{
			if (info.everSynced && num < info.version)
			{
				Plugin.Log.LogWarning("[ContainerSync] IGNORANDO pacote atrasado/fora de ordem pra key='" + text + "' " + $"(versão recebida={num} < versão que já tenho={info.version}) - não aplicado.");
				return null;
			}
			Plugin.Log.LogInfo($"[ContainerSync] CLIENT aplica verdade do host: key='{text}' versao={num}");
			ApplyState(val, list);
			info.lastSyncedSig = Sig(list);
			info.everSynced = true;
			info.networkTouched = true;
			info.version = num;
			info.lastRequestedSig = "";
			info.lastRequestSentAt = -999f;
			return null;
		}
		List<SlotState> list2 = Snapshot(val);
		string text2 = Sig(list2);
		List<SlotState> list3;
		if (!info.everSynced || text2 == info.lastSyncedSig)
		{
			Plugin.Log.LogInfo("[ContainerSync] HOST aceita pedido do cliente: key='" + text + "'");
			ApplyState(val, list);
			list3 = list;
			info.version++;
		}
		else
		{
			Plugin.Log.LogInfo("[ContainerSync] HOST REJEITA pedido (conflito) — host vence: key='" + text + "'");
			list3 = list2;
		}
		info.lastSyncedSig = Sig(list3);
		info.everSynced = true;
		info.networkTouched = true;
		return Build(text, list3, info.version);
	}

	private static void ApplyState(Inventory inv, List<SlotState> want)
	{
		int num = Mathf.Min(want.Count, inv.slots.Count);
		for (int i = 0; i < num; i++)
		{
			InvSlot val = inv.slots[i];
			SlotState slotState = want[i];
			InvItemClass invItem = val.invItem;
			bool flag = InvItemClass.isNull(invItem);
			bool flag2 = string.IsNullOrEmpty(slotState.type);
			if (flag && flag2)
			{
				continue;
			}
			if (!flag && !flag2 && invItem.type == slotState.type && invItem.amount == slotState.amount && Mathf.Approximately(invItem.durability, slotState.durability))
			{
				if (invItem.ammo != slotState.ammo)
				{
					invItem.ammo = slotState.ammo;
				}
				continue;
			}
			val.removeItem();
			if (!flag2)
			{
				InvItemClass val2 = val.createItem(slotState.type, slotState.amount, ItemDurability.ToFraction(slotState.type, slotState.durability), (InvItem.ModifierQuality)0, false);
				if (val2 != null)
				{
					val2.ammo = slotState.ammo;
				}
			}
		}
	}

	private static Inventory FindByKey(string key)
	{
		if (_byKey.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		if (key.StartsWith("corpse:"))
		{
			Character val = EnemySync.ResolvePuppetById(key.Substring("corpse:".Length));
			Inventory val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<Inventory>() : null);
			if ((Object)(object)val2 != (Object)null)
			{
				_byKey[key] = val2;
				Plugin.Log.LogInfo("[ContainerSync] corpo resolvido por ID de rede do inimigo: key='" + key + "'.");
			}
			return val2;
		}
		if (!SyncKeyUtil.TryParsePos(key, out var pos))
		{
			return null;
		}
		int num = key.LastIndexOf('@');
		string text = ((num > 0) ? key.Substring(0, num) : key);
		Inventory val3 = null;
		float num2 = 20f;
		Item[] array = SceneCache.Items();
		foreach (Item val4 in array)
		{
			if ((Object)(object)val4 == (Object)null || val4.name != text)
			{
				continue;
			}
			Inventory component = ((Component)val4).GetComponent<Inventory>();
			if (!((Object)(object)component == (Object)null) && IsContainerType(component.invType))
			{
				float num3 = Vector3.Distance(((Component)val4).transform.position, pos);
				if (num3 < num2)
				{
					num2 = num3;
					val3 = component;
				}
			}
		}
		if ((Object)(object)val3 != (Object)null)
		{
			_byKey[key] = val3;
			Plugin.Log.LogWarning("[ContainerSync] chave '" + key + "' não bateu exata - achei o container por " + $"NOME+RAIO (dist={num2:F1}) e aliei - chaves divergiram (ver SyncKeyUtil).");
		}
		return val3;
	}

	private static byte[] Build(string key, List<SlotState> slots, int version)
	{
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.ContainerState).Str(key).Int(version)
			.UShort((ushort)slots.Count);
		foreach (SlotState slot in slots)
		{
			packetWriter.Str(slot.type).Int(slot.amount).Float(slot.durability)
				.Int(slot.ammo);
		}
		return packetWriter.Finish();
	}

	public static void ForceRebroadcast()
	{
		foreach (KeyValuePair<Inventory, Info> container in _containers)
		{
			container.Value.lastSyncedSig = "";
		}
	}

	public static void Reset()
	{
		_containers.Clear();
		_byKey.Clear();
	}
}
