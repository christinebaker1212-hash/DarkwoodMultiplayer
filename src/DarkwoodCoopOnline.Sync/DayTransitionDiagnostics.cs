using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DayTransitionDiagnostics
{
	private const float HotWindowSeconds = 30f;

	private const float SnapshotInterval = 0.5f;

	private static bool _initialized;

	private static bool _crossedDayTime;

	private static bool _crossedDayTimePlus1;

	private static bool _lastForbidInputs;

	private static float _hotWindowUntil = -1f;

	private static float _snapshotTimer;

	public static void Tick()
	{
		Controller instance = Singleton<Controller>.Instance;
		Player instance2 = Player.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			return;
		}
		if (!_initialized)
		{
			_initialized = true;
			_lastForbidInputs = Core.forbidInputs;
			_crossedDayTime = instance.CurrentTime >= (int)instance.dayTime;
			_crossedDayTimePlus1 = instance.CurrentTime >= (int)instance.dayTime + 1;
			return;
		}
		if (instance.CurrentTime >= (int)instance.dayTime && !_crossedDayTime)
		{
			_crossedDayTime = true;
			_hotWindowUntil = Time.time + 30f;
			Plugin.Log.LogInfo($"[DayTransitionDiag] CurrentTime cruzou dayTime ({instance.dayTime:F0}) em Time.time={Time.time:F2} " + $"(CurrentTime={instance.CurrentTime} day={instance.day}) - startDay/onNewDay devem disparar agora.");
		}
		if (instance.CurrentTime >= (int)instance.dayTime + 1 && !_crossedDayTimePlus1)
		{
			_crossedDayTimePlus1 = true;
			_hotWindowUntil = Time.time + 30f;
			Plugin.Log.LogInfo($"[DayTransitionDiag] CurrentTime cruzou dayTime+1 em Time.time={Time.time:F2} " + $"(CurrentTime={instance.CurrentTime} day={instance.day}) - showDayNotifier/startAfterNight devem disparar agora.");
		}
		if (Core.forbidInputs != _lastForbidInputs)
		{
			_lastForbidInputs = Core.forbidInputs;
			_hotWindowUntil = Time.time + 30f;
			Plugin.Log.LogInfo($"[DayTransitionDiag] Core.forbidInputs mudou pra {Core.forbidInputs} em Time.time={Time.time:F2}.");
		}
		if (!(Time.time >= _hotWindowUntil))
		{
			_snapshotTimer += Time.deltaTime;
			if (!(_snapshotTimer < 0.5f))
			{
				_snapshotTimer = 0f;
				string arg = (((Object)(object)instance2.torsoAnimator != (Object)null && (Object)(object)instance2.torsoAnimator.Library != (Object)null) ? ((Object)instance2.torsoAnimator.Library).name : "(null)");
				string arg2 = (((Object)(object)instance2.torsoAnimator != (Object)null && instance2.torsoAnimator.CurrentClip != null) ? instance2.torsoAnimator.CurrentClip.name : "(null)");
				Plugin.Log.LogInfo($"[DayTransitionDiag][Snapshot] Time.time={Time.time:F2} lib='{arg}' clip='{arg2}' " + $"performingAction={instance2.performingAction} endingSleep={instance2.endingSleep} fakingDeathAni={instance2.fakingDeathAni} " + $"forbidInputs={Core.forbidInputs} isAfterNight={instance.isAfterNight} skippedDay={instance2.skippedDay}");
			}
		}
	}

	public static void Reset()
	{
		_initialized = false;
		_crossedDayTime = false;
		_crossedDayTimePlus1 = false;
		_hotWindowUntil = -1f;
		_snapshotTimer = 0f;
	}
}
