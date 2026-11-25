using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class NpcStateSync
{
	private class Snapshot
	{
		public int reputation;

		public bool dead;

		public int deadID;

		public int attackedID;

		public bool wantsToTalk;
	}

	private static readonly Dictionary<string, Snapshot> _known = new Dictionary<string, Snapshot>();

	private static int _knownKarma = int.MinValue;

	public static byte[] PollChanges()
	{
		Flags instance = Singleton<Flags>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		List<(string, int, bool, int, int, bool)> list = null;
		foreach (Flags.NPCState npcState in instance.npcStates)
		{
			if (npcState == null || string.IsNullOrEmpty(npcState.name))
			{
				continue;
			}
			if (!_known.TryGetValue(npcState.name, out var value))
			{
				_known[npcState.name] = new Snapshot
				{
					reputation = npcState.reputation,
					dead = npcState.dead,
					deadID = npcState.deadID,
					attackedID = npcState.attackedID,
					wantsToTalk = npcState.wantsToTalk
				};
			}
			else if (value.reputation != npcState.reputation || value.dead != npcState.dead || value.deadID != npcState.deadID || value.attackedID != npcState.attackedID || value.wantsToTalk != npcState.wantsToTalk)
			{
				value.reputation = npcState.reputation;
				value.dead = npcState.dead;
				value.deadID = npcState.deadID;
				value.attackedID = npcState.attackedID;
				value.wantsToTalk = npcState.wantsToTalk;
				if (list == null)
				{
					list = new List<(string, int, bool, int, int, bool)>();
				}
				list.Add((npcState.name, npcState.reputation, npcState.dead, npcState.deadID, npcState.attackedID, npcState.wantsToTalk));
			}
		}
		bool flag = false;
		if (_knownKarma == int.MinValue)
		{
			_knownKarma = instance.karmaPoints;
		}
		else if (_knownKarma != instance.karmaPoints)
		{
			flag = true;
			_knownKarma = instance.karmaPoints;
		}
		if (list == null && !flag)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.NpcStateChanged);
		packetWriter.UShort((ushort)(list?.Count ?? 0));
		if (list != null)
		{
			foreach (var item in list)
			{
				packetWriter.Str(item.Item1).Int(item.Item2).Bool(item.Item3)
					.Int(item.Item4)
					.Int(item.Item5)
					.Bool(item.Item6);
			}
		}
		packetWriter.Bool(flag);
		if (flag)
		{
			packetWriter.Int(_knownKarma);
		}
		return packetWriter.Finish();
	}

	public static void ApplyChanges(PacketReader r)
	{
		Flags instance = Singleton<Flags>.Instance;
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			int num2 = r.Int();
			bool flag = r.Bool();
			int num3 = r.Int();
			int attackedID = r.Int();
			bool wantsToTalk = r.Bool();
			if (!((Object)(object)instance == (Object)null) && !string.IsNullOrEmpty(text))
			{
				Flags.NPCState val = instance.getNPCState(text);
				if (val == null)
				{
					val = new Flags.NPCState
					{
						name = text
					};
					instance.npcStates.Add(val);
				}
				val.reputation = num2;
				val.dead = flag;
				val.deadID = num3;
				val.attackedID = attackedID;
				val.wantsToTalk = wantsToTalk;
				_known[text] = new Snapshot
				{
					reputation = num2,
					dead = flag,
					deadID = num3,
					attackedID = attackedID,
					wantsToTalk = wantsToTalk
				};
				Plugin.Log.LogInfo($"[NpcStateSync] '{text}' atualizado (peer): reputation={num2} dead={flag} deadID={num3}");
			}
		}
		if (r.Bool())
		{
			int num4 = r.Int();
			if ((Object)(object)instance != (Object)null)
			{
				instance.karmaPoints = num4;
			}
			_knownKarma = num4;
			Plugin.Log.LogInfo($"[NpcStateSync] karmaPoints atualizado (peer): {num4}");
		}
	}

	public static void Reset()
	{
		_known.Clear();
		_knownKarma = int.MinValue;
	}
}
