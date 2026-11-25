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
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_008b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		List<KeyValuePair<Vector3, bool>> list = null;
		Door[] array = SceneCache.Doors();
		foreach (Door val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			Vector3 position = ((Component)val).transform.position;
			if (Mathf.Min(Vector3.Distance(position, p1), Vector3.Distance(position, p2)) > 4000f)
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
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_0019: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_0032: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			Vector3 val = r.Vec2XZ(0f);
			bool flag = r.Bool();
			_known[Key(val)] = flag;
			Door val2 = FindNearest(val);
			if (!((Object)(object)val2 == (Object)null) && val2.opened != flag)
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
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
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
