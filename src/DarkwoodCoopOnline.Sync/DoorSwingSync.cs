using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DoorSwingSync
{
	private struct Snap
	{
		public float t;

		public Vector3 pos;

		public Quaternion rot;
	}

	private class Info
	{
		public float remoteUntil;

		public float localOwnUntil;

		public bool frozen;

		public Quaternion lastRot;

		public Vector3 pivot;

		public readonly List<Snap> buf = new List<Snap>();
	}

	private const float Range = 3000f;

	private const float RotThresh = 3f;

	private const float NearRadius = 320f;

	private const float OwnTimeout = 0.4f;

	private const float StickyOwn = 0.5f;

	private const float InterpDelay = 0.08f;

	private const int MaxPerPacket = 40;

	private static readonly Dictionary<Door, Info> _doors = new Dictionary<Door, Info>();

	private static readonly Dictionary<string, Door> _byPivot = new Dictionary<string, Door>();

	private static Door[] Scene()
	{
		return SceneCache.Doors();
	}

	private static Rigidbody BodyRB(Door d)
	{
		if (!((Object)(object)d.body != (Object)null))
		{
			return null;
		}
		return ((Component)d.body).GetComponent<Rigidbody>();
	}

	private static Info GetInfo(Door d)
	{
		if (!_doors.TryGetValue(d, out var value))
		{
			value = new Info
			{
				pivot = ((Component)d).transform.position,
				lastRot = (((Object)(object)d.body != (Object)null) ? d.body.rotation : ((Component)d).transform.rotation)
			};
			string key = Key(value.pivot);
			if (!_byPivot.ContainsKey(key))
			{
				_byPivot[key] = d;
			}
			_doors[d] = value;
		}
		return value;
	}

	public static byte[] PollSwings(Vector3 p1, Vector3 p2)
	{
		Vector3 val = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.position : p1);
		List<Door> list = null;
		Door[] array = Scene();
		foreach (Door val2 in array)
		{
			if ((Object)(object)val2 == (Object)null || (Object)(object)val2.body == (Object)null)
			{
				continue;
			}
			Vector3 position = ((Component)val2).transform.position;
			if (CoopPeers.MinDistance(position, p1) > 3000f)
			{
				continue;
			}
			Info info = GetInfo(val2);
			Quaternion rotation = val2.body.rotation;
			float num = Quaternion.Angle(rotation, info.lastRot);
			info.lastRot = rotation;
			bool flag = Time.time < info.remoteUntil;
			bool flag2 = Vector3.Distance(position, val) < 320f;
			if (!info.frozen && num > 3f && flag2)
			{
				info.localOwnUntil = Time.time + 0.5f;
			}
			bool flag3 = flag2 && !flag && Time.time < info.localOwnUntil;
			if (FurnitureSync.Debug && (num > 1f || info.frozen || flag))
			{
				Plugin.Log.LogInfo($"[Swing] pivot={Key(position)} dRot={num:F1} own={flag3} near={flag2} " + $"remoteOwned={flag} frozen={info.frozen} bodyRotZ={rotation.eulerAngles.z:F0}");
			}
			if (flag3)
			{
				if (info.frozen)
				{
					Freeze(val2, freeze: false, info);
				}
				if (list == null)
				{
					list = new List<Door>();
				}
				list.Add(val2);
				if (list.Count >= 40)
				{
					break;
				}
			}
			else if (flag)
			{
				if (!info.frozen)
				{
					Freeze(val2, freeze: true, info);
				}
			}
			else if (info.frozen)
			{
				Freeze(val2, freeze: false, info);
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.DoorSwing).UShort((ushort)list.Count);
		foreach (Door item in list)
		{
			packetWriter.Vec2XZ(((Component)item).transform.position).Vec2XZ(item.body.position).Quat(item.body.rotation);
		}
		return packetWriter.Finish();
	}

	public static void ApplySwings(PacketReader r, bool isHost)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			Vector3 pivot = r.Vec2XZ(0f);
			Vector3 pos = r.Vec2XZ(0f);
			Quaternion rot = r.Quat();
			Door val = FindByPivot(pivot);
			if (!((Object)(object)val == (Object)null) && !((Object)(object)val.body == (Object)null))
			{
				Info info = GetInfo(val);
				info.remoteUntil = Time.time + 0.4f;
				if (!info.frozen)
				{
					Freeze(val, freeze: true, info);
				}
				info.buf.Add(new Snap
				{
					t = Time.time,
					pos = pos,
					rot = rot
				});
				while (info.buf.Count > 0 && Time.time - info.buf[0].t > 0.5f)
				{
					info.buf.RemoveAt(0);
				}
			}
		}
	}

	public static void Tick()
	{
		float num = Time.time - 0.08f;
		foreach (KeyValuePair<Door, Info> door in _doors)
		{
			Door key = door.Key;
			Info value = door.Value;
			if ((Object)(object)key == (Object)null || (Object)(object)key.body == (Object)null || !value.frozen || Time.time >= value.remoteUntil)
			{
				continue;
			}
			List<Snap> buf = value.buf;
			if (buf.Count == 0)
			{
				continue;
			}
			Vector3 position;
			Quaternion rotation;
			if (buf.Count == 1 || num <= buf[0].t)
			{
				position = buf[0].pos;
				rotation = buf[0].rot;
			}
			else if (num >= buf[buf.Count - 1].t)
			{
				position = buf[buf.Count - 1].pos;
				rotation = buf[buf.Count - 1].rot;
			}
			else
			{
				position = buf[buf.Count - 1].pos;
				rotation = buf[buf.Count - 1].rot;
				for (int i = 0; i < buf.Count - 1; i++)
				{
					if (num >= buf[i].t && num <= buf[i + 1].t)
					{
						float num2 = buf[i + 1].t - buf[i].t;
						float num3 = ((num2 > 0.0001f) ? ((num - buf[i].t) / num2) : 1f);
						position = Vector3.Lerp(buf[i].pos, buf[i + 1].pos, num3);
						rotation = Quaternion.Slerp(buf[i].rot, buf[i + 1].rot, num3);
						break;
					}
				}
			}
			Rigidbody val = BodyRB(key);
			if ((Object)(object)val != (Object)null)
			{
				val.position = position;
				val.rotation = rotation;
			}
			else
			{
				key.body.position = position;
				key.body.rotation = rotation;
			}
		}
	}

	private static void Freeze(Door d, bool freeze, Info info)
	{
		info.frozen = freeze;
		Rigidbody val = BodyRB(d);
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		if (freeze)
		{
			val.isKinematic = true;
		}
		else
		{
			val.isKinematic = !d.opened;
			if (!val.isKinematic)
			{
				val.velocity = Vector3.zero;
				val.angularVelocity = Vector3.zero;
			}
		}
		if (!freeze)
		{
			info.buf.Clear();
		}
	}

	private static Door FindByPivot(Vector3 pivot)
	{
		string text = Key(pivot);
		if (_byPivot.TryGetValue(text, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		Door[] array = Scene();
		foreach (Door val in array)
		{
			if (!((Object)(object)val == (Object)null) && Key(((Component)val).transform.position) == text)
			{
				GetInfo(val);
				return val;
			}
		}
		return null;
	}

	private static string Key(Vector3 p)
	{
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void Reset()
	{
		foreach (KeyValuePair<Door, Info> door in _doors)
		{
			if ((Object)(object)door.Key != (Object)null && door.Value.frozen)
			{
				Freeze(door.Key, freeze: false, door.Value);
			}
		}
		_doors.Clear();
		_byPivot.Clear();
	}
}
