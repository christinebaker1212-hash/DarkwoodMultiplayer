using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ForbidInputsWatchdog
{
	private const float MaxStuckSeconds = 15f;

	private static float _stuckSince = -1f;

	public static void Tick()
	{
		if (!CoopRuntime.SessionActive)
		{
			_stuckSince = -1f;
			return;
		}
		if (!Core.forbidInputs)
		{
			_stuckSince = -1f;
			return;
		}
		Controller instance = Singleton<Controller>.Instance;
		if ((Object)(object)instance != (Object)null && instance.playingCutscene)
		{
			_stuckSince = -1f;
			return;
		}
		DialogueWindow val = (((Object)(object)Singleton<UI>.Instance != (Object)null) ? Singleton<UI>.Instance.dialogueWindow : null);
		if ((Object)(object)val != (Object)null && (val.displayingDialogue || ((Component)val).gameObject.activeSelf))
		{
			_stuckSince = -1f;
		}
		else if (_stuckSince < 0f)
		{
			_stuckSince = Time.unscaledTime;
		}
		else if (!(Time.unscaledTime - _stuckSince < 15f))
		{
			Plugin.Log.LogWarning($"[ForbidInputsWatchdog] Core.forbidInputs travado em true por mais de {15f:F0}s " + "sem cutscene/diálogo ativo - forçando limpeza (mesma proteção 'nunca preso pra sempre' do puppet).");
			Core.cantChangeForbidInputs = false;
			Core.forbidInputs = false;
			_stuckSince = -1f;
		}
	}

	public static void Reset()
	{
		_stuckSince = -1f;
	}
}
