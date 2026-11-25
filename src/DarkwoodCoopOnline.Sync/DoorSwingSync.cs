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
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_0277: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0193: Unknown result type (might be due to invalid IL or missing references)
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
			if (Mathf.Min(Vector3.Distance(position, p1), Vector3.Distance(position, p2)) > 3000f)
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
		//IL_0014: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_009d: Unknown result type (might be due to invalid IL or missing references)
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_00a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ab: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		//IL_0244: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0235: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_0138: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01fe: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
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
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
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
