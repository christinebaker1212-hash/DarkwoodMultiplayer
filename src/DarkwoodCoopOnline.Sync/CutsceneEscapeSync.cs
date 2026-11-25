using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class CutsceneEscapeSync
{
	public static void ForceEndLocalCutscene()
	{
		CutsceneManager val = (((Object)(object)Singleton<Controller>.Instance != (Object)null) ? Singleton<Controller>.Instance.currentCutsceneManager : null);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[CutsceneEscapeSync] nenhuma cutscene rodando aqui agora (currentCutsceneManager null) - nada a fazer.");
			return;
		}
		Plugin.Log.LogWarning("[CutsceneEscapeSync] forçando fim da cutscene local (debugEndCutscene nativo) - avisando o peer pra fazer o mesmo.");
		val.debugEndCutscene();
		if (CoopRuntime.SessionActive)
		{
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.CutsceneForceEnded).Finish());
		}
	}

	public static void ApplyForceEnded()
	{
		CutsceneManager val = (((Object)(object)Singleton<Controller>.Instance != (Object)null) ? Singleton<Controller>.Instance.currentCutsceneManager : null);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[CutsceneEscapeSync] peer forçou fim de cutscene, mas eu não estou em nenhuma agora - ignorado.");
			return;
		}
		Plugin.Log.LogWarning("[CutsceneEscapeSync] peer forçou fim de cutscene - fazendo o mesmo aqui.");
		val.debugEndCutscene();
	}
}
