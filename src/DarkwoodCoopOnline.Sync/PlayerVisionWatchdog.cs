using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PlayerVisionWatchdog
{
	private const float GraceSeconds = 5f;

	private const float NearZero = 1f;

	private static float _fovLogicBaseline = -1f;

	private static float _fovLogicStuckSince = -1f;

	private static float _lightDotStuckSince = -1f;

	public static void Tick()
	{
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null) && !((Object)(object)instance.FOVLogic == (Object)null) && !((Object)(object)instance.lightDot == (Object)null))
		{
			Controller instance2 = Singleton<Controller>.Instance;
			bool flag = (Object)(object)instance2 != (Object)null && instance2.playingCutscene;
			if (_fovLogicBaseline < 0f && !flag && instance.FOVLogic.LightRadius > 1f)
			{
				_fovLogicBaseline = instance.FOVLogic.LightRadius;
			}
			CheckAndRestore(instance, flag);
		}
	}

	private static void CheckAndRestore(Player player, bool inCutscene)
	{
		float unscaledTime = Time.unscaledTime;
		if (!inCutscene && player.FOVLogic.LightRadius <= 1f)
		{
			if (_fovLogicStuckSince < 0f)
			{
				_fovLogicStuckSince = unscaledTime;
			}
			else if (unscaledTime - _fovLogicStuckSince > 5f)
			{
				float num = ((_fovLogicBaseline > 1f) ? _fovLogicBaseline : 400f);
				Plugin.Log.LogWarning($"[PlayerVisionWatchdog] FOVLogic.LightRadius preso em {player.FOVLogic.LightRadius:F1} " + $"há mais de {5f:F0}s fora de cutscene - restaurando pra {num:F0} (visão do jogador estava travada).");
				player.FOVLogic.LightRadius = num;
				_fovLogicStuckSince = -1f;
			}
		}
		else
		{
			_fovLogicStuckSince = -1f;
		}
		if (!inCutscene && player.lightDot.LightRadius <= 1f)
		{
			if (_lightDotStuckSince < 0f)
			{
				_lightDotStuckSince = unscaledTime;
			}
			else if (unscaledTime - _lightDotStuckSince > 5f)
			{
				Plugin.Log.LogWarning($"[PlayerVisionWatchdog] lightDot.LightRadius preso em {player.lightDot.LightRadius:F1} " + $"há mais de {5f:F0}s fora de cutscene - restaurando pro padrão nativo.");
				player.modifyLightDot(0f);
				_lightDotStuckSince = -1f;
			}
		}
		else
		{
			_lightDotStuckSince = -1f;
		}
	}

	public static void Reset()
	{
		_fovLogicStuckSince = -1f;
		_lightDotStuckSince = -1f;
	}
}
