using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine.SceneManagement;

namespace DarkwoodCoopOnline.Sync;

public static class WorldGenDiagnostics
{
	private static bool _wasGenerating;

	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(SceneManager), "LoadScene", new Type[1] { typeof(string) }, (Type[])null), new HarmonyMethod(typeof(WorldGenDiagnostics), "BeforeLoadScene", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void BeforeLoadScene(string sceneName)
	{
		Plugin.Log.LogWarning("[WorldGenDiag][Diag] SceneManager.LoadScene('" + sceneName + "') CHAMADO - " + $"sessionActive={CoopRuntime.SessionActive} lobbyActive={LobbySync.IsActive} " + $"sessionLinked={CoopRuntime.Session?.Linked} role={CoopRuntime.Session?.Role}");
	}

	public static void Tick()
	{
		bool flag = !Core.worldGenFinished();
		if (flag != _wasGenerating)
		{
			_wasGenerating = flag;
			Controller instance = Singleton<Controller>.Instance;
			if (flag)
			{
				Plugin.Log.LogInfo($"[WorldGenDiag] COMEÇOU a regenerar o mundo — day={instance?.day} time={((instance != null) ? new int?(instance.CurrentTime) : null)}");
			}
			else
			{
				Plugin.Log.LogInfo($"[WorldGenDiag] TERMINOU de regenerar o mundo — day={instance?.day} time={((instance != null) ? new int?(instance.CurrentTime) : null)}");
			}
		}
	}

	public static void Reset()
	{
		_wasGenerating = false;
	}
}
