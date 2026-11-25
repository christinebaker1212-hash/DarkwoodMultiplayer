using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class RainSync
{
	public static byte[] BuildHeartbeat()
	{
		Rain instance = Singleton<Rain>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		return PacketWriter.Begin(PacketType.RainState).Bool(instance.rainToday).Float(instance.timeToStart)
			.Float(instance.lightningTime)
			.Float(instance.preRainLightningTime)
			.Bool(instance.preRainLightning)
			.Float(instance.duration)
			.Int(instance.timeToFadeInFog?.time ?? 0)
			.Int(instance.timeToFadeInFog?.day ?? 0)
			.Bool(instance.fogFadedInToday)
			.Int(instance.timeToFadeOutFog?.time ?? 0)
			.Int(instance.timeToFadeOutFog?.day ?? 0)
			.Bool(instance.fogFadedOutToday)
			.Bool(instance.fogIsActive)
			.Finish();
	}

	public static void ApplyHeartbeat(PacketReader r)
	{
		//IL_00b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Expected O, but got Unknown
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Expected O, but got Unknown
		bool rainToday = r.Bool();
		float timeToStart = r.Float();
		float lightningTime = r.Float();
		float preRainLightningTime = r.Float();
		bool preRainLightning = r.Bool();
		float duration = r.Float();
		int num = r.Int();
		int num2 = r.Int();
		bool fogFadedInToday = r.Bool();
		int num3 = r.Int();
		int num4 = r.Int();
		bool fogFadedOutToday = r.Bool();
		bool fogIsActive = r.Bool();
		Rain instance = Singleton<Rain>.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			instance.rainToday = rainToday;
			instance.timeToStart = timeToStart;
			instance.lightningTime = lightningTime;
			instance.preRainLightningTime = preRainLightningTime;
			instance.preRainLightning = preRainLightning;
			instance.duration = duration;
			instance.timeToFadeInFog = new TimeAndDay(num, num2, (EqualsEnum)30);
			instance.fogFadedInToday = fogFadedInToday;
			instance.timeToFadeOutFog = new TimeAndDay(num3, num4, (EqualsEnum)30);
			instance.fogFadedOutToday = fogFadedOutToday;
			instance.fogIsActive = fogIsActive;
		}
	}
}
