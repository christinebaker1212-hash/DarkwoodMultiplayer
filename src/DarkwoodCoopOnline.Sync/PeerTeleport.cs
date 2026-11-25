using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PeerTeleport
{
	private const float CooldownSeconds = 1.5f;

	private static float _lastUseAt = -999f;

	public static void TeleportToPeer()
	{
		if (!CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo("[PeerTeleport] sem sessão de co-op ativa - ignorado.");
		}
		else if (!(Time.time - _lastUseAt < 1.5f))
		{
			Player instance = Player.Instance;
			RemotePlayerPuppet remotePlayerPuppet = ((CoopRuntime.Session != null) ? CoopRuntime.Session.Puppet : null);
			if ((Object)(object)instance == (Object)null || (Object)(object)remotePlayerPuppet == (Object)null)
			{
				Plugin.Log.LogInfo("[PeerTeleport] jogador local ou puppet do peer indisponível - ignorado.");
				return;
			}
			if (!remotePlayerPuppet.HasState)
			{
				Plugin.Log.LogInfo("[PeerTeleport] ainda não recebi a posição real do peer - ignorado.");
				return;
			}
			_lastUseAt = Time.time;
			Vector3 position = ((Component)remotePlayerPuppet).transform.position;
			Plugin.Log.LogInfo($"[PeerTeleport] teleportando de {((Component)instance).transform.position:F0} para perto do peer {position:F0}.");
			((Component)instance).transform.position = position;
			SceneCache.ForceRescanCharacters();
		}
	}

	public static void Reset()
	{
		_lastUseAt = -999f;
	}
}
