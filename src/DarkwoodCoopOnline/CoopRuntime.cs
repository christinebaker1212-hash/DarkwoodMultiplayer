using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

public static class CoopRuntime
{
	public static CoopSession Session { get; private set; }

	public static bool SessionActive
	{
		get
		{
			if (!DebugCheats.VanillaMode && Session != null)
			{
				return Session.Linked;
			}
			return false;
		}
	}

	public static bool RealSessionActive
	{
		get
		{
			if (Session != null)
			{
				return Session.Linked;
			}
			return false;
		}
	}

	public static bool PeerConnected => SessionActive;

	// The slot (0 = host, 1-3 = the others) of the player whose packet is being handled right now.
	// -1 outside of packet handling.
	public static int Sender { get; internal set; } = -1;


	public static bool IsHost
	{
		get
		{
			if (Session != null)
			{
				return Session.Role == NetRole.Host;
			}
			return false;
		}
	}

	public static void SetSession(CoopSession session)
	{
		Session = session;
		_idTag = null;
	}

	private static string _idTag;

	// Prefix for network ids this PC makes up (enemies, shadows): "P<slot>.<random>". Unity's object
	// numbers repeat from PC to PC (two players loading the same area get the same numbers), so an id
	// made of them alone - or tagged only "host" / "client", as with two players - collided between
	// clients: one enemy's death landed on another, and some players' enemies never died.
	public static string IdTag
	{
		get
		{
			if (_idTag == null)
			{
				_idTag = "P" + (Session?.MySlot ?? 0) + "." + new System.Random().Next(4096, 65535).ToString("x");
			}
			return _idTag;
		}
	}

	// Which player made a network id (from IdTag; old-style "H:" = host). -1 = unknown
	public static int SlotOfId(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return -1;
		}
		if (id.StartsWith("H:", System.StringComparison.Ordinal))
		{
			return 0;
		}
		if (id[0] == 'P')
		{
			int num = id.IndexOf('.');
			if (num > 1 && int.TryParse(id.Substring(1, num - 1), out var result))
			{
				return result;
			}
		}
		return -1;
	}
}

// Everyone else in the game, as seen from this PC.
public static class CoopPeers
{
	private static readonly List<RemotePlayerPuppet> Empty = new List<RemotePlayerPuppet>();

	// Every remote player's puppet (alive or not) that exists right now
	public static IReadOnlyList<RemotePlayerPuppet> Puppets => CoopRuntime.Session?.PuppetList ?? Empty;

	public static RemotePlayerPuppet Of(int slot)
	{
		return CoopRuntime.Session?.PuppetOf(slot);
	}

	// The puppet of whoever sent the packet being handled
	public static RemotePlayerPuppet SenderPuppet => Of(CoopRuntime.Sender);

	public static int Count => Puppets.Count;

	// Smallest distance from x to the local player (at local) or to any remote player.
	// Replaces the old two-player "Mathf.Min(Distance(x, me), Distance(x, peer))".
	public static float MinDistance(Vector3 x, Vector3 local)
	{
		float num = Vector3.Distance(x, local);
		IReadOnlyList<RemotePlayerPuppet> puppets = Puppets;
		for (int i = 0; i < puppets.Count; i++)
		{
			RemotePlayerPuppet remotePlayerPuppet = puppets[i];
			if ((Object)(object)remotePlayerPuppet != (Object)null && remotePlayerPuppet.HasState)
			{
				float num2 = Vector3.Distance(x, ((Component)remotePlayerPuppet).transform.position);
				if (num2 < num)
				{
					num = num2;
				}
			}
		}
		return num;
	}

	public static RemotePlayerPuppet Nearest(Vector3 x, System.Func<RemotePlayerPuppet, bool> filter = null)
	{
		RemotePlayerPuppet result = null;
		float num = float.MaxValue;
		IReadOnlyList<RemotePlayerPuppet> puppets = Puppets;
		for (int i = 0; i < puppets.Count; i++)
		{
			RemotePlayerPuppet remotePlayerPuppet = puppets[i];
			if (!((Object)(object)remotePlayerPuppet == (Object)null) && remotePlayerPuppet.HasState && (filter == null || filter(remotePlayerPuppet)))
			{
				float num2 = Vector3.Distance(x, ((Component)remotePlayerPuppet).transform.position);
				if (num2 < num)
				{
					num = num2;
					result = remotePlayerPuppet;
				}
			}
		}
		return result;
	}

	// Positions of every remote player that has sent at least one state
	public static List<Vector3> Positions()
	{
		List<Vector3> list = new List<Vector3>(4);
		IReadOnlyList<RemotePlayerPuppet> puppets = Puppets;
		for (int i = 0; i < puppets.Count; i++)
		{
			RemotePlayerPuppet remotePlayerPuppet = puppets[i];
			if ((Object)(object)remotePlayerPuppet != (Object)null && remotePlayerPuppet.HasState)
			{
				list.Add(((Component)remotePlayerPuppet).transform.position);
			}
		}
		return list;
	}

	public static bool AnyWithin(Vector3 x, float range)
	{
		IReadOnlyList<RemotePlayerPuppet> puppets = Puppets;
		for (int i = 0; i < puppets.Count; i++)
		{
			RemotePlayerPuppet remotePlayerPuppet = puppets[i];
			if ((Object)(object)remotePlayerPuppet != (Object)null && remotePlayerPuppet.HasState && Vector3.Distance(x, ((Component)remotePlayerPuppet).transform.position) <= range)
			{
				return true;
			}
		}
		return false;
	}

	public static string NameOf(int slot)
	{
		return CoopRuntime.Session?.PeerName(slot) ?? ("Player " + (slot + 1));
	}
}
