using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class GeneratorSync
{
	private class Info
	{
		public string key;

		public string lastSig = "";
	}

	private const float Range = 4000f;

	private static readonly Dictionary<Generator, Info> _generators = new Dictionary<Generator, Info>();

	private static readonly Dictionary<string, Generator> _byKey = new Dictionary<string, Generator>();

	private static Info GetInfo(Generator g)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!_generators.TryGetValue(g, out var value))
		{
			value = new Info
			{
				key = ((Object)g).name + "@" + Key(((Component)g).transform.position)
			};
			_generators[g] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = g;
			}
		}
		return value;
	}

	private static string Sig(Generator g)
	{
		return $"{Mathf.RoundToInt(g.fuel)}|{g.isOn}";
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
		List<(string, float, bool)> list = null;
		Generator[] array = SceneCache.Generators();
		foreach (Generator val in array)
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
					list = new List<(string, float, bool)>();
				}
				list.Add((info.key, val.fuel, val.isOn));
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.GeneratorState).UShort((ushort)list.Count);
		foreach (var item in list)
		{
			packetWriter.Str(item.Item1).Float(item.Item2).Bool(item.Item3);
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
			bool flag = r.Bool();
			if (_byKey.TryGetValue(key, out var value) && !((Object)(object)value == (Object)null))
			{
				GetInfo(value).lastSig = $"{Mathf.RoundToInt(num2)}|{flag}";
				value.fuel = num2;
				if (flag && !value.isOn)
				{
					value.turnOn();
				}
				else if (!flag && value.isOn)
				{
					value.powerDown();
				}
			}
		}
	}

	public static void ForceRebroadcast()
	{
		foreach (KeyValuePair<Generator, Info> generator in _generators)
		{
			generator.Value.lastSig = "";
		}
	}

	public static void Reset()
	{
		_generators.Clear();
		_byKey.Clear();
	}
}
