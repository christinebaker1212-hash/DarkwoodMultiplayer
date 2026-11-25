using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class LockSync
{
	private const float Range = 4000f;

	private const float MatchRadius = 250f;

	private static readonly Dictionary<string, bool> _known = new Dictionary<string, bool>();

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		List<(string, Vector3, bool)> changes = null;
		Locked[] array = SceneCache.Locks();
		foreach (Locked val in array)
		{
			if (!((Object)(object)val == (Object)null))
			{
				CheckOne(((Object)((Component)val).gameObject).name, ((Component)val).transform.position, val.locked, isPadlock: false, p1, p2, ref changes);
			}
		}
		Padlock[] array2 = SceneCache.Padlocks();
		foreach (Padlock val2 in array2)
		{
			if (!((Object)(object)val2 == (Object)null))
			{
				CheckOne(((Object)((Component)val2).gameObject).name, ((Component)val2).transform.position, val2.locked, isPadlock: true, p1, p2, ref changes);
			}
		}
		if (changes == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.LockState).UShort((ushort)changes.Count);
		foreach (var item in changes)
		{
			packetWriter.Str(item.Item1).Vec3(item.Item2).Bool(item.Item3);
		}
		return packetWriter.Finish();
	}

	private static void CheckOne(string name, Vector3 pos, bool locked, bool isPadlock, Vector3 p1, Vector3 p2, ref List<(string, Vector3, bool)> changes)
	{
		if (CoopPeers.MinDistance(pos, p1) > 4000f)
		{
			return;
		}
		string key = KeyOf(name, pos);
		if (_known.TryGetValue(key, out var value) && value == locked)
		{
			return;
		}
		_known[key] = locked;
		if (!locked)
		{
			if (changes == null)
			{
				changes = new List<(string, Vector3, bool)>();
			}
			changes.Add((name, pos, isPadlock));
		}
	}

	public static void ApplyChanges(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			Vector3 val = r.Vec3();
			bool num2 = r.Bool();
			_known[KeyOf(text, val)] = false;
			if (num2)
			{
				Padlock val2 = FindNearest<Padlock>(SceneCache.Padlocks(), text, val);
				if ((Object)(object)val2 != (Object)null && val2.locked)
				{
					val2.unlock(false);
					Plugin.Log.LogInfo("[LockSync] cadeado '" + text + "' destrancado pelo peer.");
				}
			}
			else
			{
				Locked val3 = FindNearest<Locked>(SceneCache.Locks(), text, val);
				if ((Object)(object)val3 != (Object)null && val3.locked)
				{
					val3.unlock();
					Plugin.Log.LogInfo("[LockSync] tranca '" + text + "' destrancada pelo peer.");
				}
			}
		}
	}

	private static T FindNearest<T>(T[] items, string name, Vector3 pos) where T : Component
	{
		T result = default(T);
		float num = 250f;
		foreach (T val in items)
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

	public static void ForceRebroadcast()
	{
		_known.Clear();
	}

	public static void Reset()
	{
		_known.Clear();
	}
}
