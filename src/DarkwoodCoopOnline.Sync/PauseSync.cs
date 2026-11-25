using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PauseSync
{
	private static bool _localIntent;

	// slots of the other players who want the game paused (it's paused while anyone does)
	private static readonly System.Collections.Generic.HashSet<int> _peerIntents = new System.Collections.Generic.HashSet<int>();

	private static bool _peerIntent => _peerIntents.Count > 0;

	private static bool _lastKnownCombined;

	private static bool _dialogueExclusionLogged;

	private static bool _suppressForDoctorDream;

	private static bool _lobbyHold;

	public static bool LobbyHold
	{
		get
		{
			return _lobbyHold;
		}
		set
		{
			if (_lobbyHold != value)
			{
				_lobbyHold = value;
				bool flag = _localIntent || _peerIntent || _lobbyHold;
				if (flag != Core.Paused)
				{
					ForceLocal(flag);
				}
				else
				{
					_lastKnownCombined = flag;
				}
			}
		}
	}

	public static bool PeerWantsPause => _peerIntent;

	public static string PausedByNames
	{
		get
		{
			System.Collections.Generic.List<string> list = new System.Collections.Generic.List<string>();
			foreach (int peerIntent in _peerIntents)
			{
				list.Add(CoopPeers.NameOf(peerIntent));
			}
			return string.Join(", ", list.ToArray());
		}
	}

	public static void EnterDreamSuppression()
	{
		_suppressForDoctorDream = true;
	}

	public static void ExitDreamSuppression()
	{
		if (_suppressForDoctorDream)
		{
			_suppressForDoctorDream = false;
			_localIntent = false;
			_peerIntents.Clear();
			_lastKnownCombined = Core.Paused;
		}
	}

	public static void ReassertTimeScale()
	{
		if (!_suppressForDoctorDream && _lastKnownCombined && Time.timeScale != 0f)
		{
			Plugin.Log.LogWarning($"[PauseSync] Time.timeScale vazou pra {Time.timeScale} enquanto devia " + "estar pausado (bypass nativo fora de Core.pause/unpause, ex. fim de geração de mundo) - corrigindo.");
			Time.timeScale = 0f;
		}
	}

	public static byte[] PollLocalChange()
	{
		if (_suppressForDoctorDream)
		{
			return null;
		}
		ReassertTimeScale();
		bool paused = Core.Paused;
		if (paused == _lastKnownCombined)
		{
			return null;
		}
		if ((Object)(object)Player.Instance != (Object)null && Player.Instance.inDialogue)
		{
			if (!_dialogueExclusionLogged)
			{
				_dialogueExclusionLogged = true;
				Plugin.Log.LogInfo($"[PauseSync] Core.Paused mudou pra {paused} mas é o próprio diálogo nativo " + "(Player.inDialogue=true) pausando/despausando - fica local, NÃO propagando pro peer.");
			}
			return null;
		}
		_dialogueExclusionLogged = false;
		_localIntent = paused;
		bool flag = _localIntent || _peerIntent || _lobbyHold;
		Plugin.Log.LogInfo($"[PauseSync] intenção local mudou pra pausado={_localIntent} — avisando peer.");
		if (flag != paused)
		{
			ForceLocal(flag);
		}
		else
		{
			_lastKnownCombined = flag;
		}
		return PacketWriter.Begin(PacketType.PauseState).Bool(_localIntent).Finish();
	}

	public static void ApplyPeerState(PacketReader r, int from)
	{
		if (!_suppressForDoctorDream)
		{
			if (r.Bool())
			{
				_peerIntents.Add(from);
			}
			else
			{
				_peerIntents.Remove(from);
			}
			Recompute();
		}
	}

	// A player who left can't keep the game paused
	public static void OnPeerLeft(int slot)
	{
		if (_peerIntents.Remove(slot))
		{
			Recompute();
		}
	}

	private static void Recompute()
	{
		{
			bool flag = _localIntent || _peerIntent || _lobbyHold;
			if (flag != Core.Paused)
			{
				ForceLocal(flag);
			}
			else
			{
				_lastKnownCombined = flag;
			}
		}
	}

	private static void ForceLocal(bool paused)
	{
		if (paused)
		{
			Core.pause(false, 1f);
		}
		else
		{
			Core.unpause(1f, false);
		}
		_lastKnownCombined = Core.Paused;
	}

	public static void Reset()
	{
		_localIntent = false;
		_peerIntents.Clear();
		_lobbyHold = false;
		_dialogueExclusionLogged = false;
		_suppressForDoctorDream = false;
		if (Core.Paused)
		{
			Core.unpause(1f, false);
		}
		_lastKnownCombined = Core.Paused;
	}
}
