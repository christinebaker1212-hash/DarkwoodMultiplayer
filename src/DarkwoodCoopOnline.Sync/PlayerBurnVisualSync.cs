using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PlayerBurnVisualSync
{
	private static bool _lastBurning;

	private static bool _knownRemote;

	private static bool _lastRemoteBurning;

	public static byte[] PollChanges()
	{
		if ((Object)(object)Player.Instance == (Object)null)
		{
			return null;
		}
		bool flag = (Object)(object)((Component)Player.Instance).GetComponent<Burn>() != (Object)null;
		if (flag == _lastBurning)
		{
			return null;
		}
		_lastBurning = flag;
		return PacketWriter.Begin(PacketType.PlayerBurningState).Bool(flag).Finish();
	}

	public static void ApplyState(PacketReader r, RemotePlayerPuppet puppet)
	{
		bool flag = r.Bool();
		if (_knownRemote && flag == _lastRemoteBurning)
		{
			return;
		}
		_knownRemote = true;
		_lastRemoteBurning = flag;
		if ((Object)(object)puppet == (Object)null)
		{
			return;
		}
		Burn component = ((Component)puppet).GetComponent<Burn>();
		if (flag)
		{
			if ((Object)(object)component == (Object)null)
			{
				((Component)puppet).gameObject.AddComponent<Burn>();
			}
		}
		else if ((object)component != null)
		{
			component.stop();
		}
	}

	public static void Reset()
	{
		_lastBurning = false;
		_knownRemote = false;
		_lastRemoteBurning = false;
	}
}
