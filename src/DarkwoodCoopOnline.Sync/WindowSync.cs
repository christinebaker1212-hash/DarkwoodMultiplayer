using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class WindowSync
{
	private class Info
	{
		public string key;

		public string lastSig = "";
	}

	private const float Range = 4000f;

	private static readonly Dictionary<Window, Info> _windows = new Dictionary<Window, Info>();

	private static readonly Dictionary<string, Window> _byKey = new Dictionary<string, Window>();

	private static Info GetInfo(Window w)
	{
		if (!_windows.TryGetValue(w, out var value))
		{
			value = new Info
			{
				key = w.name + "@" + Key(((Component)w).transform.position)
			};
			_windows[w] = value;
			if (!_byKey.ContainsKey(value.key))
			{
				_byKey[value.key] = w;
			}
		}
		return value;
	}

	private static string Sig(Window w)
	{
		return $"{w.barricaded}|{w.barricadeHealth}|{w.playerBarricade}";
	}

	private static string Key(Vector3 p)
	{
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		List<(string, bool, int, bool)> list = null;
		Window[] array = SceneCache.Windows();
		foreach (Window val in array)
		{
			if ((Object)(object)val == (Object)null || CoopPeers.MinDistance(((Component)val).transform.position, p1) > 4000f)
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
					list = new List<(string, bool, int, bool)>();
				}
				list.Add((info.key, val.barricaded, val.barricadeHealth, val.playerBarricade));
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.WindowState).UShort((ushort)list.Count);
		foreach (var item in list)
		{
			packetWriter.Str(item.Item1).Bool(item.Item2).Int(item.Item3)
				.Bool(item.Item4);
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
			if (!_byKey.TryGetValue(key, out var value) || (Object)(object)value == (Object)null)
			{
				continue;
			}
			GetInfo(value).lastSig = $"{flag}|{num2}|{flag2}";
			if (flag)
			{
				if (!value.barricaded)
				{
					value.barricadeState = 3;
					value.setBarricadeState(num2, flag2);
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
		foreach (KeyValuePair<Window, Info> window in _windows)
		{
			window.Value.lastSig = "";
		}
	}

	public static void Reset()
	{
		_windows.Clear();
		_byKey.Clear();
	}
}
