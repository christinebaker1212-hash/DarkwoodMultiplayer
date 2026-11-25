using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DropSync
{
	private class Info
	{
		public string id;

		public bool destroyReported;
	}

	private const float Range = 4000f;

	private static readonly Dictionary<Item, Info> _known = new Dictionary<Item, Info>();

	private static readonly Dictionary<string, Item> _byId = new Dictionary<string, Item>();

	public static byte[] PollCreates(Vector3 p1, Vector3 p2)
	{
		List<Item> list = null;
		Item[] array = SceneCache.Items();
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null || !val.isDroppedItem || (Object)(object)((Component)val).GetComponent<Trigger>() != (Object)null || _known.ContainsKey(val) || CoopPeers.MinDistance(((Component)val).transform.position, p1) > 4000f)
			{
				continue;
			}
			Inventory component = ((Component)val).GetComponent<Inventory>();
			string text = (((Object)(object)component != (Object)null && component.slots != null && component.slots.Count > 0 && !InvItemClass.isNull(component.slots[0].invItem)) ? component.slots[0].invItem.type : "");
			Vector3 position = ((Component)val).transform.position;
			string text2 = $"{text}@{Mathf.RoundToInt(position.x)}_{Mathf.RoundToInt(position.y)}_{Mathf.RoundToInt(position.z)}";
			if (_byId.ContainsKey(text2))
			{
				_known[val] = new Info
				{
					id = text2
				};
				continue;
			}
			_known[val] = new Info
			{
				id = text2
			};
			_byId[text2] = val;
			if (list == null)
			{
				list = new List<Item>();
			}
			list.Add(val);
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.DropCreate).UShort((ushort)list.Count);
		foreach (Item item in list)
		{
			Inventory component2 = ((Component)item).GetComponent<Inventory>();
			string v = "";
			int v2 = 1;
			float v3 = 0f;
			if ((Object)(object)component2 != (Object)null && component2.slots != null && component2.slots.Count > 0 && !InvItemClass.isNull(component2.slots[0].invItem))
			{
				v = component2.slots[0].invItem.type;
				v2 = component2.slots[0].invItem.amount;
				v3 = component2.slots[0].invItem.durability;
			}
			packetWriter.Str(_known[item].id).Str(v).Int(v2)
				.Vec3(((Component)item).transform.position)
				.Float(((Component)item).transform.eulerAngles.y)
				.Float(v3);
		}
		return packetWriter.Finish();
	}

	public static byte[] PollDestroys()
	{
		List<string> list = null;
		foreach (KeyValuePair<Item, Info> item in _known)
		{
			if (!item.Value.destroyReported && (Object)(object)item.Key == (Object)null)
			{
				item.Value.destroyReported = true;
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(item.Value.id);
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.DropDestroy).UShort((ushort)list.Count);
		foreach (string item2 in list)
		{
			packetWriter.Str(item2);
		}
		return packetWriter.Finish();
	}

	public static void ApplyCreate(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			string text2 = r.Str();
			int num2 = r.Int();
			Vector3 val = r.Vec3();
			float num3 = r.Float();
			float absoluteDurability = r.Float();
			if (_byId.ContainsKey(text))
			{
				continue;
			}
			Item val2 = null;
			if (!string.IsNullOrEmpty(text2))
			{
				Item[] array = SceneCache.Items();
				foreach (Item val3 in array)
				{
					if (!((Object)(object)val3 == (Object)null) && val3.isDroppedItem && !_known.ContainsKey(val3) && !(Vector3.Distance(((Component)val3).transform.position, val) > 2f))
					{
						Inventory component = ((Component)val3).GetComponent<Inventory>();
						if (!((((Object)(object)component != (Object)null && component.slots != null && component.slots.Count > 0 && !InvItemClass.isNull(component.slots[0].invItem)) ? component.slots[0].invItem.type : "") != text2))
						{
							val2 = val3;
							break;
						}
					}
				}
			}
			if ((Object)(object)val2 != (Object)null)
			{
				_known[val2] = new Info
				{
					id = text
				};
				_byId[text] = val2;
				Plugin.Log.LogInfo($"[DropSync] '{text2}' em pos={val:F0} já existia nativamente aqui - adotado em vez de duplicar.");
				continue;
			}
			InvItem val4 = ((!string.IsNullOrEmpty(text2) && (Object)(object)Singleton<ItemsDatabase>.Instance != (Object)null) ? Singleton<ItemsDatabase>.Instance.getItem(text2, false) : null);
			Object val5 = (((Object)(object)val4 != (Object)null) ? val4.item : null);
			GameObject val6 = ((val5 != (Object)null) ? Core.AddPrefab(val5, val, Quaternion.Euler(90f, num3, 0f), Core.ItemContainer, false) : Core.AddPrefab("Items/DroppedItem", val, Quaternion.Euler(90f, num3, 0f), Core.ItemContainer, false));
			Inventory val7 = (((Object)(object)val6 != (Object)null) ? val6.GetComponent<Inventory>() : null);
			if ((Object)(object)val7 != (Object)null && val7.slots != null && val7.slots.Count > 0)
			{
				val7.slots[0].createItem(text2, num2, ItemDurability.ToFraction(text2, absoluteDurability), (InvItem.ModifierQuality)0, false);
			}
			Item val8 = (((Object)(object)val6 != (Object)null) ? val6.GetComponent<Item>() : null);
			if ((Object)(object)val8 != (Object)null)
			{
				_known[val8] = new Info
				{
					id = text
				};
				_byId[text] = val8;
			}
		}
	}

	public static void ApplyDestroy(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			if (_byId.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)value).gameObject);
			}
			_byId.Remove(key);
		}
	}

	public static void Reset()
	{
		_known.Clear();
		_byId.Clear();
	}
}
