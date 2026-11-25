using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class FurnitureDiagnostics
{
	private struct Entry
	{
		public float t;

		public string name;

		public Vector3 pos;

		public bool frozen;

		public bool remoteOwned;

		public bool moved;

		public bool claimedByMe;

		public bool beingDragged;
	}

	private const int Capacity = 400;

	private static readonly Queue<Entry> _ring = new Queue<Entry>(400);

	public static void Record(string name, Vector3 pos, bool frozen, bool remoteOwned, bool moved, bool claimedByMe, bool beingDragged)
	{
		//IL_003f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		if (_ring.Count >= 400)
		{
			_ring.Dequeue();
		}
		_ring.Enqueue(new Entry
		{
			t = Time.time,
			name = name,
			pos = pos,
			frozen = frozen,
			remoteOwned = remoteOwned,
			moved = moved,
			claimedByMe = claimedByMe,
			beingDragged = beingDragged
		});
	}

	public static void Dump()
	{
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		Plugin.Log.LogInfo($"========================= [FurnDiag] DUMP ({_ring.Count} entradas) =========================");
		foreach (Entry item in _ring)
		{
			TimestampedLogSource log = Plugin.Log;
			object[] obj = new object[8] { item.t, item.name, null, null, null, null, null, null };
			Vector3 pos = item.pos;
			obj[2] = pos.ToString("F1");
			obj[3] = item.frozen;
			obj[4] = item.remoteOwned;
			obj[5] = item.moved;
			obj[6] = item.claimedByMe;
			obj[7] = item.beingDragged;
			log.LogInfo(string.Format("[FurnDiag] t={0:F2} '{1}' pos={2} frozen={3} remoteOwned={4} moved={5} claimedByMe={6} beingDragged={7}", obj));
		}
		Plugin.Log.LogInfo("========================= [FurnDiag] FIM =========================");
	}

	public static void Clear()
	{
		_ring.Clear();
	}
}
