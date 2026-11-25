using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class CutscenePlayerVisibilitySync
{
	private static bool _lastHidden;

	private const float MaxHiddenSeconds = 90f;

	private static float _hiddenSince = -1f;

	public static void Tick()
	{
		if (!CoopRuntime.SessionActive)
		{
			EnsureVisible();
			return;
		}
		RemotePlayerPuppet remotePlayerPuppet = CoopRuntime.Session?.Puppet;
		if (!((Object)(object)remotePlayerPuppet == (Object)null))
		{
			Controller instance = Singleton<Controller>.Instance;
			bool flag = (Object)(object)instance != (Object)null && instance.playingCutscene && instance.currentCutscene != null && !instance.currentCutscene.playerIsVisible;
			if (flag && _hiddenSince >= 0f && Time.unscaledTime - _hiddenSince > 90f)
			{
				Plugin.Log.LogWarning($"[CutscenePlayerVisibilitySync] puppet escondido há mais de {90f:F0}s " + "(cutscene parece travada no estado nativo) - mostrando de volta por segurança, não fica escondido pra sempre.");
				flag = false;
			}
			if (flag != _lastHidden)
			{
				_lastHidden = flag;
				_hiddenSince = (flag ? Time.unscaledTime : (-1f));
				remotePlayerPuppet.SetCutsceneHidden(flag);
				Plugin.Log.LogInfo("[CutscenePlayerVisibilitySync] puppet do peer " + (flag ? "escondido" : "visível de novo") + " (cutscene playerIsVisible=false).");
			}
		}
	}

	private static void EnsureVisible()
	{
		if (_lastHidden)
		{
			_lastHidden = false;
			_hiddenSince = -1f;
			CoopRuntime.Session?.Puppet?.SetCutsceneHidden(hidden: false);
		}
	}

	public static void Reset()
	{
		_lastHidden = false;
		_hiddenSince = -1f;
	}
}
