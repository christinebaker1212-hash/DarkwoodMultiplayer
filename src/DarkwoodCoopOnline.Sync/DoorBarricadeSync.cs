using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DoorBarricadeSync
{
	private class Info
	{
		public string key;

		public string lastSig = "";
	}

	private const float Range = 4000f;

	private static readonly Dictionary<Door, Info> _doors = new Dictionary<Door, Info>();

	private static readonly Dictionary<string, Door> _byKey = new Dictionary<string, Door>();

	private static Info GetInfo(Door d)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!_doors.TryGetValue(d, out var value))
		{
			value = new Info
			{
				key = d.name + "@" + Key(((Component)d).transform.position)
			};
			_doors[d] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = d;
			}
		}
		return value;
	}

	private static string Sig(Door d)
	{
		return $"{d.barricaded}|{d.barricadeHealth}|{d.playerBarricade}|{d.destroyed}";
	}

	private static string Key(Vector3 p)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		List<(string, bool, int, bool, bool)> list = null;
		Door[] array = SceneCache.Doors();
		foreach (Door val in array)
		{
			if ((Object)(object)val == (Object)null || Mathf.Min(Vector3.Distance(((Component)val).transform.position, p1), Vector3.Distance(((Component)val).transform.position, p2)) > 4000f)
			{
				continue;
			}
			Info info = GetInfo(val);
			string text = Sig(val);
			if (!(text == info.lastSig))
			{
				info.lastSig = text;
				if (list == null)
				{
					list = new List<(string, bool, int, bool, bool)>();
				}
				list.Add((info.key, val.barricaded, val.barricadeHealth, val.playerBarricade, val.destroyed));
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.DoorBarricadeState).UShort((ushort)list.Count);
		foreach (var item in list)
		{
			packetWriter.Str(item.Item1).Bool(item.Item2).Int(item.Item3)
				.Bool(item.Item4)
				.Bool(item.Item5);
		}
		return packetWriter.Finish();
	}

	public static void ApplyChanges(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			bool flag = r.Bool();
			int num2 = r.Int();
			bool flag2 = r.Bool();
			bool flag3 = r.Bool();
			if (!_byKey.TryGetValue(key, out var value) || (Object)(object)value == (Object)null)
			{
				continue;
			}
			GetInfo(value).lastSig = $"{flag}|{num2}|{flag2}|{flag3}";
			if (flag3 && !value.destroyed)
			{
				value.destroyDoor(false);
			}
			else if (!flag3 && value.destroyed)
			{
				value.unDestroy();
			}
			if (flag)
			{
				if (!value.barricaded)
				{
					value.playerBarricade = flag2;
					value.barricadeHealth = num2;
					value.barricadeState = 3;
					value.setBarricadeState();
					value.barricadeHealth = num2;
					AudioController.Play("hammer_barricade", ((Component)value).transform);
				}
				else if (value.barricadeHealth != num2)
				{
					value.barricadeHealth = num2;
				}
			}
			else if (value.barricaded)
			{
				value.destroyBarricade(false);
			}
		}
	}

	public static void ForceRebroadcast()
	{
		foreach (KeyValuePair<Door, Info> door in _doors)
		{
			door.Value.lastSig = "";
		}
	}

	public static void Reset()
	{
		_doors.Clear();
		_byKey.Clear();
	}
}
