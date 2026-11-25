using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class LobbySync
{
	private const KeyCode StartKey = (KeyCode)102;

	private const string PrimaryActionRewired = "Primary Action";

	private static GUIStyle _boxStyle;

	private static GUIStyle _titleStyle;

	private static GUIStyle _hintStyle;

	public static bool IsActive { get; private set; }

	public static bool PeerConnected { get; private set; }

	public static bool IsHost { get; private set; }

	public static bool RequireBothIntroDone
	{
		get
		{
			if ((Object)(object)Player.Instance != (Object)null)
			{
				return Player.Instance.firstPlay;
			}
			return false;
		}
	}

	public static bool LocalIntroDone { get; private set; }

	// slots of the other players whose prologue intro is over
	private static readonly System.Collections.Generic.HashSet<int> _introDone = new System.Collections.Generic.HashSet<int>();

	// everyone else in the world has finished the intro
	public static bool PeerIntroDone
	{
		get
		{
			foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
			{
				if ((Object)(object)puppet != (Object)null && !_introDone.Contains(puppet.Slot))
				{
					return false;
				}
			}
			return true;
		}
	}

	// The host pressed start (players joining after this skip the lobby)
	public static bool Started { get; private set; }

	private static bool BothIntroDoneIfNeeded()
	{
		if (RequireBothIntroDone)
		{
			if (LocalIntroDone)
			{
				return PeerIntroDone;
			}
			return false;
		}
		return true;
	}

	public static void Begin(bool isHost)
	{
		IsActive = true;
		IsHost = isHost;
		PeerConnected = false;
		PauseSync.LobbyHold = true;
		LocalIntroDone = false;
		_introDone.Clear();
		Started = false;
		LampSync.PreRegisterAll();
		FurnitureSync.PreRegisterAll();
		Plugin.Log.LogInfo($"[LobbySync] lobby iniciado (isHost={isHost}) - segurando pausa até o peer conectar" + (isHost ? " e eu dar Start." : "."));
	}

	public static void MarkPeerConnected()
	{
		if (IsActive && !PeerConnected)
		{
			PeerConnected = true;
			LanSaveTransfer.ClearHostPeerStatus();
			Plugin.Log.LogInfo("[LobbySync] peer conectado - " + (IsHost ? "botão Start liberado." : "aguardando o host dar Start."));
			if (LocalIntroDone)
			{
				SendIntroReadyPacket();
			}
		}
	}

	public static void MarkLocalIntroDone()
	{
		if (IsActive && RequireBothIntroDone && !LocalIntroDone)
		{
			LocalIntroDone = true;
			Plugin.Log.LogInfo("[LobbySync] minha intro do prólogo terminou - avisando o peer (se já conectado).");
			if (PeerConnected)
			{
				SendIntroReadyPacket();
			}
		}
	}

	private static void SendIntroReadyPacket()
	{
		CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.PrologueIntroReady).Finish());
	}

	public static void ApplyPeerIntroDone(int from)
	{
		if (_introDone.Add(from))
		{
			Plugin.Log.LogInfo($"[LobbySync] '{CoopPeers.NameOf(from)}' finished the prologue intro.");
		}
	}

	public static void OnPeerLeft(int slot)
	{
		_introDone.Remove(slot);
		if (IsActive && CoopPeers.Count == 0)
		{
			PeerConnected = false;
		}
	}

	public static void RequestStart()
	{
		if (IsHost && IsActive && PeerConnected && BothIntroDoneIfNeeded())
		{
			CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.LobbyStart).Finish());
			Complete();
			ReconcileAll();
		}
	}

	public static void ReconcileAll()
	{
		Plugin.Log.LogInfo("[LobbySync] HOST reconciliando estado do mundo pro cliente (o que mexi durante a espera).");
		LampSync.ForceRebroadcast();
		GeneratorSync.ForceRebroadcast();
		SawSync.ForceRebroadcast();
		WindowSync.ForceRebroadcast();
		DoorBarricadeSync.ForceRebroadcast();
		PropSync.ForceRebroadcast();
		LockSync.ForceRebroadcast();
		TrapSync.ForceRebroadcast();
		ContainerSync.ForceRebroadcast();
		FurnitureSync.ForceRebroadcast();
		WorkbenchSync.ForceRebroadcast();
	}

	public static void ApplyStart()
	{
		Complete();
	}

	private static bool StartActionDown()
	{
		if (Input.GetKeyDown((KeyCode)102))
		{
			return true;
		}
		InputScript instance = Singleton<InputScript>.Instance;
		if ((Object)(object)instance != (Object)null && instance.rewiredPlayer != null)
		{
			return instance.rewiredPlayer.GetButtonDown("Primary Action");
		}
		return false;
	}

	public static void Tick()
	{
		if (IsActive && IsHost && PeerConnected && StartActionDown())
		{
			RequestStart();
		}
	}

	public static void ForceClose()
	{
		if (!IsActive)
		{
			Plugin.Log.LogInfo("[LobbySync] ForceClose chamado, mas nenhum lobby está ativo agora - nada a fazer.");
			return;
		}
		Plugin.Log.LogWarning("[LobbySync] lobby removido manualmente (botão da janela de conexão) - jogo destravado à força.");
		Complete();
		PeerConnected = false;
		IsHost = false;
	}

	private static void Complete()
	{
		Started = true;
		if (IsActive)
		{
			IsActive = false;
			PauseSync.LobbyHold = false;
			Plugin.Log.LogInfo("[LobbySync] jogo iniciado - despausando.");
		}
	}

	public static void Reset()
	{
		IsActive = false;
		PeerConnected = false;
		IsHost = false;
		LocalIntroDone = false;
		_introDone.Clear();
		Started = false;
	}

	public static void Draw()
	{
		if (!IsActive)
		{
			return;
		}
		EnsureStyles();
		float num = ((float)Screen.width - 420f) * 0.5f;
		float num2 = ((float)Screen.height - 130f) * 0.5f;
		GUI.Box(new Rect(num, num2, 420f, 130f), GUIContent.none, _boxStyle);
		float num3 = num2 + 14f;
		GUI.Label(new Rect(num, num3, 420f, 26f), "LOBBY", _titleStyle);
		num3 += 32f;
		string text;
		if (PeerConnected)
		{
			text = ((!BothIntroDoneIfNeeded()) ? ((!LocalIntroDone) ? "Watching the prologue intro..." : "Waiting for the other player to finish the prologue intro...") : ((!IsHost) ? "Waiting for the host to start the game..." : $"{CoopRuntime.Session?.PlayersInGame ?? 1}/{Proto.MaxPlayers} players here - press [{(object)(KeyCode)102}] to start!\n(more friends can still join after you start)"));
		}
		else
		{
			string text2 = "";
			if (string.IsNullOrEmpty(text2))
			{
				text2 = LanSaveTransfer.HostPeerStatus;
			}
			text = (string.IsNullOrEmpty(text2) ? ("Waiting for players..." + (string.IsNullOrEmpty(HostFlow.InviteLine) ? "" : ("\n" + HostFlow.InviteLine))) : text2);
		}
		GUI.Label(new Rect(num + 10f, num3, 400f, 44f), text, _hintStyle);
	}

	private static void EnsureStyles()
	{
		if (_boxStyle == null)
		{
			Texture2D val = new Texture2D(1, 1);
			val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.85f));
			val.Apply();
			GUIStyle val2 = new GUIStyle();
			val2.normal.background = val;
			_boxStyle = val2;
			_titleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 20,
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			_titleStyle.normal.textColor = Color.white;
			_hintStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 14,
				alignment = (TextAnchor)4,
				wordWrap = true
			};
			_hintStyle.normal.textColor = new Color(0.85f, 0.85f, 0.8f);
		}
	}
}
