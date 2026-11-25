using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class LampSync
{
	private class Info
	{
		public string key;

		public bool known;

		public bool lastOn;
	}

	private const float Range = 4000f;

	private const int MaxPerPacket = 60;

	private static readonly Dictionary<Item, Info> _items = new Dictionary<Item, Info>();

	private static readonly Dictionary<string, Item> _byKey = new Dictionary<string, Item>();

	private const float FallbackRadius = 5f;

	private static Item[] Scene()
	{
		return SceneCache.Items();
	}

	private static Info GetInfo(Item it)
	{
		if (!_items.TryGetValue(it, out var value))
		{
			value = new Info
			{
				key = it.name + "@" + Key(((Component)it).transform.position)
			};
			_items[it] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = it;
			}
			Plugin.Log.LogInfo("[LampSync][diag] lâmpada vista: '" + it.name + "' key='" + value.key + "' " + $"draggable={it.draggable} switchable={it.switchable} pos={((Component)it).transform.position:F0}");
		}
		return value;
	}

	public static void PreRegisterAll()
	{
		int num = 0;
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if (!((Object)(object)val == (Object)null) && val.switchable)
			{
				GetInfo(val);
				num++;
			}
		}
		Plugin.Log.LogInfo($"[LampSync] pré-registradas {num} lâmpada(s) no carregamento.");
	}

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		List<KeyValuePair<string, bool>> list = null;
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null || !val.switchable || CoopPeers.MinDistance(((Component)val).transform.position, p1) > 4000f)
			{
				continue;
			}
			Info info = GetInfo(val);
			bool isOn = val.isOn;
			if (!info.known || info.lastOn != isOn)
			{
				Plugin.Log.LogInfo("[LampSync][diag] mudança local detectada: '" + val.name + "' key='" + info.key + "' " + $"isOn {info.lastOn}->{isOn} (known={info.known}) - anunciando pro peer.");
				info.known = true;
				info.lastOn = isOn;
				if (list == null)
				{
					list = new List<KeyValuePair<string, bool>>();
				}
				list.Add(new KeyValuePair<string, bool>(info.key, isOn));
				if (list.Count >= 60)
				{
					break;
				}
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.LampToggle).UShort((ushort)list.Count);
		foreach (KeyValuePair<string, bool> item in list)
		{
			packetWriter.Str(item.Key).Bool(item.Value);
		}
		return packetWriter.Finish();
	}

	public static void ApplyToggles(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			bool flag = r.Bool();
			Item val = FindByKey(text);
			if ((Object)(object)val == (Object)null)
			{
				Plugin.Log.LogWarning($"[LampSync][diag] recebi mudança pro key='{text}' (isOn={flag}) mas não achei " + "NENHUM item switchable correspondente aqui - descartado.");
				continue;
			}
			Info info = GetInfo(val);
			Plugin.Log.LogInfo($"[LampSync][diag] '{val.name}' recebido do peer: isOn={flag} (era {val.isOn}) - aplicando aqui.");
			info.known = true;
			info.lastOn = flag;
			if (val.isOn != flag)
			{
				if (flag)
				{
					val.turnOn();
				}
				else
				{
					val.turnOff();
				}
			}
		}
	}

	private static Item FindByKey(string key)
	{
		if (_byKey.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		Item[] array = Scene();
		foreach (Item val in array)
		{
			if (!((Object)(object)val == (Object)null) && val.switchable && GetInfo(val).key == key)
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
				if (!((Object)(object)val3 == (Object)null) && val3.switchable)
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
				Plugin.Log.LogWarning("[LampSync] chave '" + key + "' não bateu exata - achei a lâmpada por RAIO " + $"(dist={num:F1}) e aliei - chaves divergiram entre os 2 lados (ver SyncKeyUtil).");
				return val2;
			}
		}
		return null;
	}

	private static string Key(Vector3 p)
	{
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void ForceRebroadcast()
	{
		foreach (KeyValuePair<Item, Info> item in _items)
		{
			item.Value.known = false;
		}
	}

	public static void Reset()
	{
		_items.Clear();
		_byKey.Clear();
	}
}
