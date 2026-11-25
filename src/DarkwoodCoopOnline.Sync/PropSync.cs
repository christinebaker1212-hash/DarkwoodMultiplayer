using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PropSync
{
	private const float Range = 4000f;

	private const float MatchRadius = 250f;

	private const int MaxPerPacket = 60;

	private static readonly Dictionary<string, bool> _known = new Dictionary<string, bool>();

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		List<KeyValuePair<Vector3, bool>> list = null;
		Door[] array = SceneCache.Doors();
		foreach (Door val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			Vector3 position = ((Component)val).transform.position;
			if (CoopPeers.MinDistance(position, p1) > 4000f)
			{
				continue;
			}
			string key = Key(position);
			bool opened = val.opened;
			if (!_known.TryGetValue(key, out var value) || value != opened)
			{
				_known[key] = opened;
				if (list == null)
				{
					list = new List<KeyValuePair<Vector3, bool>>();
				}
				list.Add(new KeyValuePair<Vector3, bool>(position, opened));
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
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.DoorDelta).UShort((ushort)list.Count);
		foreach (KeyValuePair<Vector3, bool> item in list)
		{
			packetWriter.Vec2XZ(item.Key).Bool(item.Value);
		}
		return packetWriter.Finish();
	}

	public static void ApplyDoors(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			Vector3 val = r.Vec2XZ(0f);
			bool flag = r.Bool();
			_known[Key(val)] = flag;
			Door val2 = FindNearest(val);
			// a stale "open" must never force a barricaded door (opening it broke the planks)
			if (!((Object)(object)val2 == (Object)null) && val2.opened != flag && !(flag && val2.barricaded))
			{
				Transform val3 = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform : ((Component)val2).transform);
				if (flag)
				{
					val2.open(val3.position, val3, 1f);
				}
				else
				{
					val2.close(val3);
				}
			}
		}
	}

	private static Door FindNearest(Vector3 pos)
	{
		Door result = null;
		float num = 250f;
		Door[] array = SceneCache.Doors();
		foreach (Door val in array)
		{
			if (!((Object)(object)val == (Object)null))
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

	private static string Key(Vector3 p)
	{
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void ForceRebroadcast()
	{
		_known.Clear();
	}

	public static void Reset()
	{
		_known.Clear();
	}
}
