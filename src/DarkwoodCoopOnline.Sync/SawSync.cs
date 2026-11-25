using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class SawSync
{
	private class Info
	{
		public string key;

		public float lastFuel = -1f;
	}

	private const float Range = 4000f;

	private static readonly Dictionary<Saw, Info> _saws = new Dictionary<Saw, Info>();

	private static readonly Dictionary<string, Saw> _byKey = new Dictionary<string, Saw>();

	private static Info GetInfo(Saw s)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!_saws.TryGetValue(s, out var value))
		{
			value = new Info
			{
				key = ((Object)s).name + "@" + Key(((Component)s).transform.position)
			};
			_saws[s] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = s;
			}
		}
		return value;
	}

	private static string Key(Vector3 p)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		List<(string, float)> list = null;
		Saw[] array = SceneCache.Saws();
		foreach (Saw val in array)
		{
			if ((Object)(object)val == (Object)null || Mathf.Min(Vector3.Distance(((Component)val).transform.position, p1), Vector3.Distance(((Component)val).transform.position, p2)) > 4000f)
			{
				continue;
			}
			Info info = GetInfo(val);
			if (!Mathf.Approximately(val.fuel, info.lastFuel))
			{
				info.lastFuel = val.fuel;
				if (list == null)
				{
					list = new List<(string, float)>();
				}
				list.Add((info.key, val.fuel));
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.SawFuel).UShort((ushort)list.Count);
		foreach (var item in list)
		{
			packetWriter.Str(item.Item1).Float(item.Item2);
		}
		return packetWriter.Finish();
	}

	public static void ApplyChanges(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			float num2 = r.Float();
			if (_byKey.TryGetValue(key, out var value) && !((Object)(object)value == (Object)null))
			{
				GetInfo(value).lastFuel = num2;
				value.fuel = num2;
			}
		}
	}

	public static void ForceRebroadcast()
	{
		foreach (KeyValuePair<Saw, Info> saw in _saws)
		{
			saw.Value.lastFuel = -1f;
		}
	}

	public static void Reset()
	{
		_saws.Clear();
		_byKey.Clear();
	}
}
