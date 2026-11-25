using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ConstructibleSync
{
	private const float Range = 4000f;

	private const float MatchRadius = 250f;

	private static readonly Dictionary<string, bool> _known = new Dictionary<string, bool>();

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		List<(string, Vector3, int)> list = null;
		Constructible[] array = SceneCache.Constructibles();
		foreach (Constructible val in array)
		{
			if ((Object)(object)val == (Object)null || CoopPeers.MinDistance(((Component)val).transform.position, p1) > 4000f)
			{
				continue;
			}
			string key = KeyOf(((Object)((Component)val).gameObject).name, ((Component)val).transform.position);
			if (_known.TryGetValue(key, out var value) && value == val.constructed)
			{
				continue;
			}
			_known[key] = val.constructed;
			if (val.constructed)
			{
				if (list == null)
				{
					list = new List<(string, Vector3, int)>();
				}
				list.Add((((Object)((Component)val).gameObject).name, ((Component)val).transform.position, val.chosenOption));
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.ConstructibleBuilt).UShort((ushort)list.Count);
		foreach (var item in list)
		{
			packetWriter.Str(item.Item1).Vec3(item.Item2).Int(item.Item3);
		}
		return packetWriter.Finish();
	}

	public static void ApplyChanges(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			Vector3 val = r.Vec3();
			int num2 = r.Int();
			_known[KeyOf(text, val)] = true;
			Constructible val2 = FindNearest(text, val);
			if ((Object)(object)val2 != (Object)null)
			{
				val2.construct(false, num2);
				Plugin.Log.LogInfo($"[ConstructibleSync] '{text}' construído pelo peer (opção {num2}).");
			}
			else
			{
				Plugin.Log.LogWarning("[ConstructibleSync] peer construiu '" + text + "' mas não achei o objeto correspondente aqui.");
			}
		}
	}

	private static Constructible FindNearest(string name, Vector3 pos)
	{
		Constructible result = null;
		float num = 250f;
		Constructible[] array = SceneCache.Constructibles();
		foreach (Constructible val in array)
		{
			if (!((Object)(object)val == (Object)null) && !(((Object)((Component)val).gameObject).name != name))
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, pos);
				if (num2 < num)
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}

	private static string KeyOf(string name, Vector3 p)
	{
		return $"{name}@{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void Reset()
	{
		_known.Clear();
	}
}
