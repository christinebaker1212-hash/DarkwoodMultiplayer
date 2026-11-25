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

	public static bool PeerIntroDone { get; private set; }

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
		PeerIntroDone = false;
		LampSync.PreRegisterAll();
		FurnitureSync.PreRegisterAll();
		Plugin.Log.LogInfo($"[LobbySync] lobby iniciado (isHost={isHost}) - segurando pausa até o peer conectar" + (isHost ? " e eu dar Start." : "."));
	}

	public static void MarkPeerConnected()
	{
		if (IsActive && !PeerConnected)
		{
			PeerConnected = true;
			SaveFetchServer.ClearPeerJoinStatus();
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

	public static void ApplyPeerIntroDone()
	{
		if (!PeerIntroDone)
		{
			PeerIntroDone = true;
			Plugin.Log.LogInfo("[LobbySync] peer avisou que a intro dele do prólogo terminou.");
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

	private static void ReconcileAll()
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
		PeerIntroDone = false;
	}

	public static void Draw()
	{
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
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
			text = ((!BothIntroDoneIfNeeded()) ? ((!LocalIntroDone) ? "Watching the prologue intro..." : "Waiting for the other player to finish the prologue intro...") : ((!IsHost) ? "Waiting for the host to start the game..." : $"Press [{(object)(KeyCode)102}] to start the game!"));
		}
		else
		{
			string text2 = SaveFetchServer.PeerJoinStatus;
			if (string.IsNullOrEmpty(text2))
			{
				text2 = LanSaveTransfer.HostPeerStatus;
			}
			text = (string.IsNullOrEmpty(text2) ? "Waiting for player..." : text2);
		}
		GUI.Label(new Rect(num + 10f, num3, 400f, 44f), text, _hintStyle);
	}

	private static void EnsureStyles()
	{
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Expected O, but got Unknown
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Expected O, but got Unknown
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0095: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Expected O, but got Unknown
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
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
