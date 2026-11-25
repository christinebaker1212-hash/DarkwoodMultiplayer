using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class TrapSync
{
	private class Info
	{
		public string key;

		public bool known;

		public bool lastTriggered;

		public bool destroyReported;
	}

	private const float Range = 4000f;

	private const int MaxPerPacket = 60;

	private static readonly Dictionary<Trigger, Info> _traps = new Dictionary<Trigger, Info>();

	private static readonly Dictionary<string, Trigger> _byKey = new Dictionary<string, Trigger>();

	private static Trigger[] Scene()
	{
		return SceneCache.Triggers();
	}

	private static Info GetInfo(Trigger t)
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		if (!_traps.TryGetValue(t, out var value))
		{
			value = new Info
			{
				key = ((Object)t).name + "@" + Key(((Component)t).transform.position)
			};
			_traps[t] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = t;
			}
		}
		return value;
	}

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		List<KeyValuePair<string, bool>> list = null;
		Trigger[] array = Scene();
		foreach (Trigger val in array)
		{
			if ((Object)(object)val == (Object)null || Mathf.Min(Vector3.Distance(((Component)val).transform.position, p1), Vector3.Distance(((Component)val).transform.position, p2)) > 4000f)
			{
				continue;
			}
			Info info = GetInfo(val);
			bool triggered = val.triggered;
			if (!info.known || info.lastTriggered != triggered)
			{
				info.known = true;
				info.lastTriggered = triggered;
				if (list == null)
				{
					list = new List<KeyValuePair<string, bool>>();
				}
				list.Add(new KeyValuePair<string, bool>(info.key, triggered));
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
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.TrapTrigger).UShort((ushort)list.Count);
		foreach (KeyValuePair<string, bool> item in list)
		{
			packetWriter.Str(item.Key).Bool(item.Value);
		}
		return packetWriter.Finish();
	}

	public static void ApplyTriggers(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			bool flag = r.Bool();
			Trigger val = FindByKey(key);
			if (!((Object)(object)val == (Object)null))
			{
				Info info = GetInfo(val);
				info.known = true;
				info.lastTriggered = flag;
				if (flag && !val.triggered)
				{
					val.switchToTriggered();
				}
			}
		}
	}

	public static byte[] PollDestroys()
	{
		List<string> list = null;
		foreach (KeyValuePair<Trigger, Info> trap in _traps)
		{
			if (!trap.Value.destroyReported && (Object)(object)trap.Key == (Object)null)
			{
				trap.Value.destroyReported = true;
				if (list == null)
				{
					list = new List<string>();
				}
				list.Add(trap.Value.key);
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.TrapDestroy).UShort((ushort)list.Count);
		foreach (string item in list)
		{
			packetWriter.Str(item);
		}
		return packetWriter.Finish();
	}

	public static void ApplyDestroy(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			if (_byKey.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)value).gameObject);
			}
		}
	}

	private static Trigger FindByKey(string key)
	{
		if (_byKey.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		Trigger[] array = Scene();
		foreach (Trigger val in array)
		{
			if (!((Object)(object)val == (Object)null) && GetInfo(val).key == key)
			{
				return val;
			}
		}
		return null;
	}

	private static string Key(Vector3 p)
	{
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void ForceRebroadcast()
	{
		foreach (KeyValuePair<Trigger, Info> trap in _traps)
		{
			trap.Value.known = false;
		}
	}

	public static void Reset()
	{
		_traps.Clear();
		_byKey.Clear();
	}
}
