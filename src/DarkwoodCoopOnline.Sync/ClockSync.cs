using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ClockSync
{
	private static bool _knownDoUpdateTime = true;

	private static int _knownDay = -1;

	public static byte[] PollLocalResume()
	{
		Controller instance = Singleton<Controller>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		bool doUpdateTime = instance.DoUpdateTime;
		bool num = doUpdateTime && !_knownDoUpdateTime;
		_knownDoUpdateTime = doUpdateTime;
		if (!num)
		{
			return null;
		}
		return PacketWriter.Begin(PacketType.ClockResume).Finish();
	}

	public static void ApplyResume(PacketReader r)
	{
		Controller instance = Singleton<Controller>.Instance;
		if (instance != null)
		{
			instance.endAfterNight(false);
		}
	}

	public static byte[] PollDayAdvance()
	{
		Controller instance = Singleton<Controller>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		if (_knownDay == -1)
		{
			_knownDay = instance.day;
			return null;
		}
		if (instance.day <= _knownDay)
		{
			return null;
		}
		_knownDay = instance.day;
		return PacketWriter.Begin(PacketType.DayAdvance).Int(instance.day).Finish();
	}

	public static void ApplyDayAdvance(PacketReader r)
	{
		int num = r.Int();
		Controller instance = Singleton<Controller>.Instance;
		if (!((Object)(object)instance == (Object)null) && instance.day < num)
		{
			instance.day = num;
			instance.CurrentTime = (int)instance.dayTime;
			instance.refreshTime(false);
		}
	}

	public static byte[] BuildHeartbeat()
	{
		Controller instance = Singleton<Controller>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		return PacketWriter.Begin(PacketType.ClockHeartbeat).Int(instance.CurrentTime).Int(instance.day)
			.Bool(instance.isAfterNight)
			.Finish();
	}

	public static void ApplyHeartbeat(PacketReader r)
	{
		int num = r.Int();
		int num2 = r.Int();
		bool flag = r.Bool();
		Controller instance = Singleton<Controller>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		if (flag && !instance.isAfterNight)
		{
			instance.isAfterNight = true;
			instance.addAfterNightEffect();
			SpawnEndOfNightVines();
			Plugin.Log.LogInfo("[ClockSync] entrando na parada matinal (host congelado) - congelando o tempo aqui também.");
		}
		if (instance.CurrentTime != num || instance.day != num2)
		{
			if (num2 > instance.day)
			{
				instance.day = num2;
				instance.CurrentTime = num;
				instance.refreshTime(false);
			}
			else if (num2 == instance.day && instance.CurrentTime != num)
			{
				instance.CurrentTime = num;
				instance.refreshTimeNoLogic();
			}
		}
	}

	private static void SpawnEndOfNightVines()
	{
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null) && !((Object)(object)instance.whereAmI == (Object)null) && !((Object)(object)instance.whereAmI.bigLocation == (Object)null))
		{
			Core.AddPrefab("FX/efekt_konca_nocy", ((Component)instance.whereAmI.bigLocation).transform.position, Quaternion.identity, (GameObject)null, true);
		}
	}

	public static void Reset()
	{
		_knownDoUpdateTime = true;
		_knownDay = -1;
	}
}
