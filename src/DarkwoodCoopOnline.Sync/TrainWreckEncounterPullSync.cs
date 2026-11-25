using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class TrainWreckEncounterPullSync
{
	private const string ManagerName = "trainWreck_startCutscene";

	private const float PullRange = 1200f;

	private static bool _wasPlaying;

	public static void Tick()
	{
		//IL_009e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00aa: Unknown result type (might be due to invalid IL or missing references)
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		Controller instance = Singleton<Controller>.Instance;
		bool flag = (Object)(object)instance != (Object)null && instance.playingCutscene && (Object)(object)instance.currentCutsceneManager != (Object)null && ((Object)((Component)instance.currentCutsceneManager).gameObject).name == "trainWreck_startCutscene";
		bool num = flag && !_wasPlaying;
		_wasPlaying = flag;
		if (!num || !CoopRuntime.SessionActive)
		{
			return;
		}
		CoopSession session = CoopRuntime.Session;
		RemotePlayerPuppet remotePlayerPuppet = session?.Puppet;
		Player instance2 = Player.Instance;
		if (!((Object)(object)remotePlayerPuppet == (Object)null) && remotePlayerPuppet.HasState && !((Object)(object)instance2 == (Object)null))
		{
			float num2 = Vector3.Distance(((Component)remotePlayerPuppet).transform.position, ((Component)instance2).transform.position);
			if (num2 <= 1200f)
			{
				Plugin.Log.LogInfo($"[TrainWreckEncounterPullSync] entrei na cutscene do Doutor - peer já está perto (dist={num2:F0}), gatilho local dele deve disparar sozinho.");
				return;
			}
			Plugin.Log.LogInfo("[TrainWreckEncounterPullSync] entrei na cutscene do Doutor (Train Wreck) e o peer está longe " + $"(dist={num2:F0}) - puxando ele pra cá, pra disparar o gatilho local dele quase junto (sem pausar nada).");
			session.SendReliable(PacketWriter.Begin(PacketType.PullPeerToPosition).Vec3(((Component)instance2).transform.position).Finish());
		}
	}

	public static void Reset()
	{
		_wasPlaying = false;
	}
}
