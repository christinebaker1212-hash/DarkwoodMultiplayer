using System;
using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

[BepInPlugin("com.felipe.darkwoodcooponline", "Darkwood Co-op Online", "0.3.93")]
public class Plugin : BaseUnityPlugin
{
	public const string Version = "0.3.93";

	internal static TimestampedLogSource Log;

	private ConfigEntry<string> _transportMode;

	private ConfigEntry<int> _localPort;

	private ConfigEntry<int> _remotePort;

	private ConfigEntry<string> _peerSteamId;

	private ConfigEntry<string> _lanPeerIp;

	private ConfigEntry<int> _lanPort;

	private ConfigEntry<bool> _voiceEnabled;

	private ConfigEntry<string> _voiceDevice;

	private CoopSession _session;

	private bool _autoLoopbackPendingWorld;

	internal static Plugin Instance;

	private float _fps = 60f;

	private bool _showOverlay = true;

	private GUIStyle _ovLabel;

	private GUIStyle _ovBg;

	private float _lastEnemyStatsLogAt;

	private GUIStyle _bottomStatusLabel;

	private GUIStyle _bottomStatusBg;

	private GUIStyle _creditLabel;

	private bool _showDebugPanel;

	private Rect _debugPanelRect = new Rect(10f, 60f, 480f, 40f);

	private string _teleportKeyword = "train";

	private string _teleportKeyword2 = "doctor";

	private string _giveItemKeyword = "mushroom";

	private Vector2 _debugPanelScroll;

	private bool _showConnectionPanel;

	private Rect _connectionPanelRect = new Rect(10f, 60f, 320f, 40f);

	internal static bool TransportModeIsLan
	{
		get
		{
			if ((Object)(object)Instance != (Object)null && Instance._transportMode != null)
			{
				return Instance._transportMode.Value.Equals("LAN", StringComparison.OrdinalIgnoreCase);
			}
			return false;
		}
	}

	internal bool HasSession => _session != null;

	private void Awake()
	{
		Log = new TimestampedLogSource(Logger);
		Instance = this;
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		Version version2 = new Version("0.3.93.0");
		if (version != version2)
		{
			Log.LogError("[VersionCheck] DESSINCRONIA: Plugin.Version='0.3.93' mas AssemblyVersion " + $"compilada='{version}' - alguém bumpou só um dos dois (Properties/AssemblyInfo.cs " + "precisa acompanhar Plugin.Version SEMPRE). Corrija antes de publicar.");
		}
		else
		{
			Log.LogInfo("[VersionCheck] OK - Plugin.Version e AssemblyVersion batem (0.3.93).");
		}
		Application.runInBackground = true;
		_transportMode = ((BaseUnityPlugin)this).Config.Bind<string>("Transport", "Mode", "Loopback", "Loopback (UDP 127.0.0.1, teste numa máquina) ou Steam (relay P2P, online real) ou LAN (UDP direto por IP, via Radmin/Hamachi/rede local — sem depender do P2P da Steam)");
		_localPort = ((BaseUnityPlugin)this).Config.Bind<int>("Loopback", "LocalPort", 7777, "Porta local (troque entre as 2 cópias)");
		_remotePort = ((BaseUnityPlugin)this).Config.Bind<int>("Loopback", "RemotePort", 7778, "Porta do peer");
		_peerSteamId = ((BaseUnityPlugin)this).Config.Bind<string>("Steam", "PeerSteamId64", "0", "SteamID64 do outro jogador");
		_lanPeerIp = ((BaseUnityPlugin)this).Config.Bind<string>("LAN", "PeerIP", "", "IP do HOST (obrigatório só pra quem vai como CLIENTE) — o IP virtual do Radmin/Hamachi/ZeroTier, ou o IP da rede local");
		_lanPort = ((BaseUnityPlugin)this).Config.Bind<int>("LAN", "Port", 7777, "Porta UDP usada nos 2 lados (mesmo valor, máquinas diferentes não colidem)");
		_voiceEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("Voice", "Enabled", false, "Voice chat por proximidade (grava do microfone só durante sessão pareada)");
		_voiceDevice = ((BaseUnityPlugin)this).Config.Bind<string>("Voice", "Device", "", "Nome do dispositivo de microfone (Microphone.devices) — vazio usa o 1º disponível");
		Log.LogInfo("=== Darkwood Co-op Online 0.1.0 (M0) carregado ===");
		Log.LogInfo("Transporte=" + _transportMode.Value + "  F5=Auto  F7=Host  F8=Client  F6=DumpSaveApi  F4=Diag  F3=Overlay  F10=Stop");
		Log.LogInfo("F11=capturar estrutura da porta mais próxima  F12=stream por frame do estado da porta");
		Log.LogInfo("F9=capturar pernas REAL vs PUPPET  Insert=stream por frame das pernas");
		Log.LogInfo("Home=capturar Item switchable/isLight mais próximo  End=stream por frame");
		Log.LogInfo("Delete=capturar armadilha (Item+Trigger) mais próxima  PageDown=stream por frame");
		Log.LogInfo("PageUp=liga/desliga profiling de performance (loga [PerfSpike] sozinho ao travar)");
		Log.LogInfo("Backspace=capturar itens largados perto  Keypad0=liga/desliga watch (aparecer/sumir)");
		Log.LogInfo("Keypad1=capturar container (baú/guarda-roupa) mais próximo  Keypad2=stream por frame dos slots");
		Log.LogInfo("Keypad4=capturar cenário/eventos noturnos agora  Keypad3=liga/desliga stream de eventos noturnos");
		Log.LogInfo("Caído/reanimar: ao chegar a 0 de vida vira 'caído' em vez de morrer — segure F perto do peer caído p/ reanimar.");
		Log.LogInfo("DEBUG: Keypad5=própria vida→1 (testar caído na hora)  Keypad6=spawnar inimigo perto  Keypad7=zerar vida do inimigo mais próximo");
		Log.LogInfo("Keypad8=capturar alvo (target) dos inimigos próximos  Keypad9=liga/desliga stream de troca de alvo");
		Log.LogInfo("~ (til) = painel de DEBUG com botões (conexão, spawns, tempo, eventos, IA)");
		Log.LogInfo("KeypadDivide=capturar IA do cachorro mais próximo  KeypadMultiply=liga/desliga stream detalhado");
		Log.LogInfo("KeypadMinus=capturar TODOS os animadores (tk2dSpriteAnimator) do cachorro mais próximo, e qual o sync usa");
		Log.LogInfo("F1=teleportar até o peer AGORA (escape manual - ex.: preso do lado de fora de porta trancada em boss)");
		Log.LogInfo("F2=liga/desliga VER O CONE DE VISÃO do peer (opt-in, experimental - desliga na hora se der problema)");
		Log.LogInfo("N=liga/desliga NOME DO PEER em cima do personagem (opt-in, texto só - sem risco)");
		DownedSync.EnsurePatched("com.felipe.darkwoodcooponline");
		PlayerDamageSync.EnsurePatched("com.felipe.darkwoodcooponline");
		NightEventSync.EnsurePatched("com.felipe.darkwoodcooponline");
		DamageDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		NpcDeathDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		PuppetAttackSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		PuppetAnimSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		MainMenuNativeButtons.EnsurePatched("com.felipe.darkwoodcooponline");
		DurabilityDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		ItemBurnSync.EnsurePatched("com.felipe.darkwoodcooponline");
		MonsterRangedAttackSync.EnsurePatched("com.felipe.darkwoodcooponline");
		WildlifeRespawnSync.EnsurePatched("com.felipe.darkwoodcooponline");
		EnemyHitAnimSync.EnsurePatched("com.felipe.darkwoodcooponline");
		LiquidSync.EnsurePatched("com.felipe.darkwoodcooponline");
		LocationOwnership.EnsurePatched("com.felipe.darkwoodcooponline");
		MapPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		JournalPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		OvenPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		UiPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		EscMenuPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		DreamTransitionPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline");
		FlareSync.EnsurePatched("com.felipe.darkwoodcooponline");
		NightScenarioSync.EnsurePatched("com.felipe.darkwoodcooponline");
		FlagSync.EnsurePatched("com.felipe.darkwoodcooponline");
		ChapterTransitionSync.EnsurePatched("com.felipe.darkwoodcooponline");
		WorldGenDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		TrapSpawnSync.EnsurePatched("com.felipe.darkwoodcooponline");
		TrapPlaceSync.EnsurePatched("com.felipe.darkwoodcooponline");
		FirearmFxSync.EnsurePatched("com.felipe.darkwoodcooponline");
		ThrownItemSync.EnsurePatched("com.felipe.darkwoodcooponline");
		WorldEventSync.EnsurePatched("com.felipe.darkwoodcooponline");
		CharacterMessageSync.EnsurePatched("com.felipe.darkwoodcooponline");
		MapMarkerSync.EnsurePatched("com.felipe.darkwoodcooponline");
		LocationDiscoverySync.EnsurePatched("com.felipe.darkwoodcooponline");
		OutsideLocationSync.EnsurePatched("com.felipe.darkwoodcooponline");
		DreamSync.EnsurePatched("com.felipe.darkwoodcooponline");
		DoorLockEventSync.EnsurePatched("com.felipe.darkwoodcooponline");
		GameObjectActionSync.EnsurePatched("com.felipe.darkwoodcooponline");
		ReturnToMenuSync.EnsurePatched("com.felipe.darkwoodcooponline");
		QuestEventDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		CutsceneDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		QuestSpawnSync.EnsurePatched("com.felipe.darkwoodcooponline");
		WildlifeActivationSync.EnsurePatched("com.felipe.darkwoodcooponline");
		PlayerActionSoundSync.EnsurePatched("com.felipe.darkwoodcooponline");
		MorningRitualSync.EnsurePatched("com.felipe.darkwoodcooponline");
		ExplodableSync.EnsurePatched("com.felipe.darkwoodcooponline");
		DebugCheatKeys.EnsurePatched("com.felipe.darkwoodcooponline");
		BansheeScreamSync.EnsurePatched("com.felipe.darkwoodcooponline");
		InSightSync.EnsurePatched("com.felipe.darkwoodcooponline");
		PropSpawnSync.EnsurePatched("com.felipe.darkwoodcooponline");
		CharacterSaveSync.EnsurePatched("com.felipe.darkwoodcooponline");
		PrologueDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline");
		PrologueEncounterPullSync.EnsurePatched("com.felipe.darkwoodcooponline");
		WorldSync.EnsurePatched("com.felipe.darkwoodcooponline");
		LanSaveTransfer.EnsurePatched("com.felipe.darkwoodcooponline");
		PrologueJoinSync.EnsurePatched();
		SteamAvailability.LogOnce();
		if (SteamAvailability.Available)
		{
			try
			{
				StartSteamOnlyServices();
			}
			catch (Exception arg)
			{
				Log.LogError($"[SteamAvailability] falha iniciando os serviços de Steam: {arg}");
			}
		}
		VoiceSync.Configure(_voiceEnabled.Value, _voiceDevice.Value);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void StartSteamOnlyServices()
	{
		SaveFetchServer.EnsureStarted();
		AutoJoin.CheckStartup();
	}

	internal void SetVoiceEnabled(bool enabled)
	{
		_voiceEnabled.Value = enabled;
		VoiceSync.SetEnabled(enabled);
	}

	internal void SetVoiceDevice(string device)
	{
		_voiceDevice.Value = device;
		VoiceSync.SetDevice(device);
	}

	private void Update()
	{
		PauseSync.ReassertTimeScale();
		PerfDiagnostics.FrameStart();
		SaveFetchServer.Tick();
		AutoJoin.Tick();
		if (Input.GetKeyDown((KeyCode)286))
		{
			StartAutoLoopback();
		}
		TickAutoLoopbackPendingWorld();
		if (Input.GetKeyDown((KeyCode)288))
		{
			StartSession(NetRole.Host);
		}
		if (Input.GetKeyDown((KeyCode)289))
		{
			StartSession(NetRole.Client);
		}
		DebugCheatKeys.Tick();
		if (Input.GetKeyDown((KeyCode)91))
		{
			WorldDiagnostics.DumpPlayerTree();
		}
		if (Input.GetKeyDown((KeyCode)93))
		{
			FurnitureSync.Debug = !FurnitureSync.Debug;
			Log.LogInfo($"[FurnDbg] gravação de móveis (buffer, sem custo) = {FurnitureSync.Debug}");
		}
		if (Input.GetKeyDown((KeyCode)270))
		{
			FurnitureDiagnostics.Dump();
		}
		FurnitureSync.DebugTick();
		if (Input.GetKeyDown((KeyCode)292))
		{
			DoorDiagnostics.CaptureNearest();
		}
		if (Input.GetKeyDown((KeyCode)293))
		{
			DoorDiagnostics.ToggleWatch();
		}
		DoorDiagnostics.Tick();
		if (Input.GetKeyDown((KeyCode)290))
		{
			LegsDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)277))
		{
			LegsDiagnostics.ToggleStream();
		}
		LegsDiagnostics.Tick();
		VisionLightDiagnostics.Tick();
		if (Input.GetKeyDown((KeyCode)127))
		{
			TrapDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)281))
		{
			TrapDiagnostics.ToggleStream();
		}
		TrapDiagnostics.Tick();
		if (Input.GetKeyDown((KeyCode)280))
		{
			PerfDiagnostics.ToggleEnabled();
		}
		if (Input.GetKeyDown((KeyCode)8))
		{
			DropDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)256))
		{
			DropDiagnostics.ToggleWatch();
		}
		DropDiagnostics.Tick();
		if (Input.GetKeyDown((KeyCode)257))
		{
			ChestDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)258))
		{
			ChestDiagnostics.ToggleStream();
		}
		ChestDiagnostics.Tick();
		if (Input.GetKeyDown((KeyCode)260))
		{
			NightEventDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)259))
		{
			NightEventDiagnostics.ToggleStream();
		}
		NightEventDiagnostics.Tick();
		CutsceneDiagnostics.Tick();
		TrainWreckEncounterPullSync.Tick();
		ForbidInputsWatchdog.Tick();
		WorldGenDiagnostics.Tick();
		ChapterTransitionSync.Tick();
		CutscenePlayerVisibilitySync.Tick();
		PlayerVisionWatchdog.Tick();
		CharacterMessageSync.Tick();
		DreamSync.Tick();
		if (Input.GetKeyDown((KeyCode)267))
		{
			DogAiDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)268))
		{
			DogAiDiagnostics.ToggleStream();
		}
		if (Input.GetKeyDown((KeyCode)269))
		{
			DogAiDiagnostics.CaptureAnimators();
		}
		DogAiDiagnostics.Tick();
		AnimationSyncDiagnostics.Tick();
		DayTransitionDiagnostics.Tick();
		MainMenuHostJoinUI.Tick();
		MainMenuNativeButtons.Tick();
		MainMenuNativeButtons.TickPendingLabels();
		CoopMenuList.Tick();
		LanSaveTransfer.Tick();
		PrologueJoinSync.Tick();
		if (Input.GetKeyDown((KeyCode)291))
		{
			StopSession();
		}
		if (Input.GetKeyDown((KeyCode)96))
		{
			_showDebugPanel = !_showDebugPanel;
		}
		if (Input.GetKeyDown((KeyCode)48))
		{
			_showConnectionPanel = !_showConnectionPanel;
		}
		if (Input.GetKeyDown((KeyCode)261))
		{
			DebugCheats.SetOwnHealthCritical();
		}
		if (Input.GetKeyDown((KeyCode)262))
		{
			DebugCheats.SpawnEnemyNearby();
		}
		if (Input.GetKeyDown((KeyCode)263))
		{
			DebugCheats.KillNearestEnemy();
		}
		if (Input.GetKeyDown((KeyCode)264))
		{
			EnemyAiDiagnostics.Capture();
		}
		if (Input.GetKeyDown((KeyCode)265))
		{
			EnemyAiDiagnostics.ToggleStream();
		}
		if (Input.GetKeyDown((KeyCode)282))
		{
			PeerTeleport.TeleportToPeer();
		}
		SharedVisionSync.Tick();
		PeerNameTagSync.Tick();
		EnemyAiDiagnostics.Tick();
		_fps = Mathf.Lerp(_fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
		if (_session != null && _session.Linked && Time.time - _lastEnemyStatsLogAt > 10f)
		{
			_lastEnemyStatsLogAt = Time.time;
			(int, int) ownedStats = EnemySync.GetOwnedStats();
			(int, int) puppetStats = EnemySync.GetPuppetStats();
			Log.LogInfo($"[EnemyStats] meus inimigos: {ownedStats.Item1} ({ownedStats.Item2} visíveis) - puppets do peer: {puppetStats.Item1} ({puppetStats.Item2} visíveis)");
		}
		_session?.Update();
		if (_session != null && _session.PeerLeftSignaled)
		{
			bool num = _session.Role == NetRole.Host;
			StopSession();
			if (num)
			{
				Log.LogInfo("[Session] eu era host - reiniciando hospedagem automaticamente, voltando a aguardar jogador.");
				StartHostSession();
			}
			else
			{
				Log.LogInfo("[Session] eu era cliente e o host voltou pro menu - sessão encerrada, reconecte manualmente (F8/convite) quando ele hospedar de novo.");
			}
		}
		PerfDiagnostics.FrameEnd();
	}

	private void OnGUI()
	{
		//IL_020c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0218: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Expected O, but got Unknown
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_022c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0240: Unknown result type (might be due to invalid IL or missing references)
		//IL_024c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Expected O, but got Unknown
		//IL_025b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0260: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		//IL_0087: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Unknown result type (might be due to invalid IL or missing references)
		//IL_009b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_0108: Unknown result type (might be due to invalid IL or missing references)
		//IL_0150: Unknown result type (might be due to invalid IL or missing references)
		//IL_013a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0169: Unknown result type (might be due to invalid IL or missing references)
		//IL_018b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Expected O, but got Unknown
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_01dc: Unknown result type (might be due to invalid IL or missing references)
		MainMenuHostJoinUI.Draw();
		SaveSlotPickerUI.Draw();
		VoiceSettingsUI.Draw();
		LobbySync.Draw();
		if (_showOverlay && _session != null)
		{
			if (_ovBg == null)
			{
				Texture2D val = new Texture2D(1, 1);
				val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
				val.Apply();
				GUIStyle val2 = new GUIStyle();
				val2.normal.background = val;
				_ovBg = val2;
				_ovLabel = new GUIStyle(GUI.skin.label)
				{
					fontSize = 18,
					fontStyle = (FontStyle)1,
					alignment = (TextAnchor)4
				};
			}
			bool flag = _session.Role == NetRole.Host;
			string text = (flag ? "HOST" : "CLIENT");
			string text2 = (_session.Linked ? "connected" : "waiting...");
			float num = 240f;
			float num2 = 46f;
			float num3 = ((float)Screen.width - num) * 0.5f;
			float num4 = 6f;
			GUI.Box(new Rect(num3, num4, num, num2), GUIContent.none, _ovBg);
			_ovLabel.normal.textColor = (flag ? new Color(0.4f, 1f, 0.4f) : new Color(0.4f, 0.8f, 1f));
			GUI.Label(new Rect(num3, num4 + 2f, num, 24f), text + "  ·  " + text2, _ovLabel);
			GUIStyle val3 = new GUIStyle(_ovLabel)
			{
				fontSize = 13
			};
			val3.normal.textColor = (Color)((_fps < 45f) ? new Color(1f, 0.5f, 0.3f) : Color.white);
			GUI.Label(new Rect(num3, num4 + 24f, num, 20f), $"FPS: {_fps:F0}", val3);
		}
		if (_showDebugPanel)
		{
			_debugPanelRect = GUILayout.Window(918273, _debugPanelRect, new GUI.WindowFunction(DrawDebugPanel), "Darkwood Co-op - DEBUG", Array.Empty<GUILayoutOption>());
		}
		if (_showConnectionPanel)
		{
			_connectionPanelRect = GUILayout.Window(918274, _connectionPanelRect, new GUI.WindowFunction(DrawConnectionPanel), "Darkwood Co-op - Connection", Array.Empty<GUILayoutOption>());
		}
		DrawBottomStatus();
		DrawCredit();
		if (_session != null && _session.Linked)
		{
			DownedUI.Draw(_session.Puppet);
			PauseUI.Draw(_session.Role);
			PeerNameTagSync.Draw(_session, _session.Puppet);
		}
	}

	private void DrawBottomStatus()
	{
		if (AutoJoin.IsActive)
		{
			DrawStatusBanner(AutoJoin.StatusText, AutoJoin.IsFailed);
		}
		else if (LanSaveTransfer.IsActive)
		{
			DrawStatusBanner(LanSaveTransfer.StatusText, LanSaveTransfer.IsFailed);
		}
	}

	private void DrawStatusBanner(string statusText, bool failed)
	{
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Expected O, but got Unknown
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0040: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Expected O, but got Unknown
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0121: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_016a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0154: Unknown result type (might be due to invalid IL or missing references)
		if (_bottomStatusBg == null)
		{
			Texture2D val = new Texture2D(1, 1);
			val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
			val.Apply();
			GUIStyle val2 = new GUIStyle();
			val2.normal.background = val;
			_bottomStatusBg = val2;
			_bottomStatusLabel = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				alignment = (TextAnchor)4
			};
			_bottomStatusLabel.normal.textColor = new Color(0.85f, 0.85f, 0.8f);
		}
		string text = statusText;
		if (failed)
		{
			text += "\nShow this message (or send the log) to Felipesfx.";
		}
		float num = Mathf.Clamp((float)(failed ? Mathf.Max(statusText.Length, text.Length - statusText.Length - 1) : text.Length) * 7f + 40f, 260f, (float)Screen.width - 40f);
		float num2 = (failed ? 48f : 30f);
		Rect val3 = new Rect(((float)Screen.width - num) * 0.5f, (float)Screen.height - num2 - 24f, num, num2);
		GUI.Box(val3, GUIContent.none, _bottomStatusBg);
		_bottomStatusLabel.normal.textColor = (failed ? new Color(1f, 0.45f, 0.4f) : new Color(0.85f, 0.85f, 0.8f));
		GUI.Label(val3, text, _bottomStatusLabel);
	}

	private void DrawCredit()
	{
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (_creditLabel == null)
		{
			_creditLabel = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12
			};
			_creditLabel.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
		}
		GUI.Label(new Rect(8f, (float)Screen.height - 24f, 400f, 20f), "Developed by Felipesfx with AI help", _creditLabel);
		GUI.Label(new Rect(8f, (float)Screen.height - 40f, 400f, 20f), "Darkwood Co-op Online v0.3.93", _creditLabel);
	}

	private void DrawConnectionPanel(int id)
	{
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d5: Unknown result type (might be due to invalid IL or missing references)
		GUILayout.Label("Mode: " + _transportMode.Value, GUI.skin.box, Array.Empty<GUILayoutOption>());
		bool available = SteamAvailability.Available;
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Loopback\n(this PC)", Array.Empty<GUILayoutOption>()))
		{
			_transportMode.Value = "Loopback";
		}
		GUI.enabled = available;
		if (GUILayout.Button("Steam\n(friend)", Array.Empty<GUILayoutOption>()))
		{
			_transportMode.Value = "Steam";
		}
		GUI.enabled = true;
		if (GUILayout.Button("LAN\n(Radmin/Hamachi)", Array.Empty<GUILayoutOption>()))
		{
			_transportMode.Value = "LAN";
		}
		GUILayout.EndHorizontal();
		if (!available)
		{
			Color color = GUI.color;
			GUI.color = new Color(1f, 0.8f, 0.4f);
			GUILayout.Label("Steam not available in this build of the game (Epic/GOG?) - use LAN.", GUI.skin.box, Array.Empty<GUILayoutOption>());
			GUI.color = color;
		}
		if (_transportMode.Value.Equals("LAN", StringComparison.OrdinalIgnoreCase))
		{
			GUILayout.Space(6f);
			GUILayout.Label("Host's IP (client only):", Array.Empty<GUILayoutOption>());
			_lanPeerIp.Value = SanitizeIpInput(GUILayout.TextField(_lanPeerIp.Value, Array.Empty<GUILayoutOption>()));
			GUILayout.Label($"Port: {_lanPort.Value} (same on both sides)", Array.Empty<GUILayoutOption>());
		}
		else if (_transportMode.Value.Equals("Steam", StringComparison.OrdinalIgnoreCase))
		{
			GUILayout.Space(6f);
			GUILayout.Label("PeerSteamId64 (client manual only — JOIN button doesn't need this):", Array.Empty<GUILayoutOption>());
			_peerSteamId.Value = GUILayout.TextField(_peerSteamId.Value, Array.Empty<GUILayoutOption>());
		}
		GUILayout.Space(6f);
		Color color2 = GUI.color;
		GUI.color = new Color(1f, 0.6f, 0.5f);
		GUI.enabled = LobbySync.IsActive;
		if (GUILayout.Button(LobbySync.IsActive ? "Remove stuck lobby (force-unlock)" : "Remove stuck lobby (no lobby active)", Array.Empty<GUILayoutOption>()))
		{
			LobbySync.ForceClose();
		}
		GUI.enabled = true;
		GUI.color = color2;
		GUILayout.Space(6f);
		if (GUILayout.Button("Force-end stuck cutscene (both players - only ONE of you click this)", Array.Empty<GUILayoutOption>()))
		{
			CutsceneEscapeSync.ForceEndLocalCutscene();
		}
		GUILayout.Label("Only if actually stuck IN a cutscene (forbidInputs never clears, e.g. the Train Wreck/ Doctor scene). Native debugEndCutscene() restores control + tells your partner to do the same.", Array.Empty<GUILayoutOption>());
		GUILayout.Space(10f);
		if (GUILayout.Button("OK", Array.Empty<GUILayoutOption>()))
		{
			_showConnectionPanel = false;
		}
		GUI.DragWindow();
	}

	private void DrawDebugPanel(int id)
	{
		//IL_0002: Unknown result type (might be due to invalid IL or missing references)
		//IL_0038: Unknown result type (might be due to invalid IL or missing references)
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_06a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_06c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_06b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_06f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0839: Unknown result type (might be due to invalid IL or missing references)
		_debugPanelScroll = GUILayout.BeginScrollView(_debugPanelScroll, (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(460f),
			GUILayout.Height(Mathf.Min((float)Screen.height - 100f, 700f))
		});
		GUILayout.Label((_session != null) ? string.Format("Session: {0} - {1}", _session.Role, _session.Linked ? "connected" : "waiting...") : "Session: none", Array.Empty<GUILayoutOption>());
		GUILayout.Label("Steam invite: " + AutoJoin.StatusText, Array.Empty<GUILayoutOption>());
		GUILayout.Space(6f);
		GUILayout.Label("Menu UI (investigação botão nativo)", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Create+dump HOST/JOIN native buttons (must be at MAIN MENU, LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			MainMenuNativeButtons.DumpDiagnostics();
		}
		GUILayout.Space(6f);
		GUILayout.Label("Connection", GUI.skin.box, Array.Empty<GUILayoutOption>());
		GUILayout.Label("Mode: " + _transportMode.Value, Array.Empty<GUILayoutOption>());
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Loopback (test here on this PC)", Array.Empty<GUILayoutOption>()))
		{
			_transportMode.Value = "Loopback";
			Log.LogInfo("[Modo] trocado pra Loopback (F7/F8 manual vão usar UDP local).");
		}
		GUI.enabled = SteamAvailability.Available;
		if (GUILayout.Button("Steam (play with friend)", Array.Empty<GUILayoutOption>()))
		{
			_transportMode.Value = "Steam";
			Log.LogInfo("[Modo] trocado pra Steam (F7/F8 manual vão usar P2P real).");
		}
		GUI.enabled = true;
		if (GUILayout.Button("LAN (Radmin/Hamachi/local network)", Array.Empty<GUILayoutOption>()))
		{
			_transportMode.Value = "LAN";
			Log.LogInfo("[Modo] trocado pra LAN (F7/F8 manual vão usar UDP direto por IP).");
		}
		GUILayout.EndHorizontal();
		if (_transportMode.Value.Equals("LAN", StringComparison.OrdinalIgnoreCase))
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			GUILayout.Label("LAN.PeerIP (só CLIENTE precisa):", (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(220f) });
			_lanPeerIp.Value = SanitizeIpInput(GUILayout.TextField(_lanPeerIp.Value, (GUILayoutOption[])(object)new GUILayoutOption[1] { GUILayout.Width(140f) }));
			GUILayout.EndHorizontal();
			GUILayout.Label($"LAN.Port: {_lanPort.Value} (mesmo valor nos 2 lados)", Array.Empty<GUILayoutOption>());
		}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Host (F7)", Array.Empty<GUILayoutOption>()))
		{
			StartSession(NetRole.Host);
		}
		if (GUILayout.Button("Client (F8)", Array.Empty<GUILayoutOption>()))
		{
			StartSession(NetRole.Client);
		}
		GUILayout.EndHorizontal();
		if (GUILayout.Button("Stop (F10)", Array.Empty<GUILayoutOption>()))
		{
			StopSession();
		}
		GUILayout.Space(8f);
		GUILayout.Label("Peer visibility", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button(PeerNameTagSync.Enabled ? "Peer name tag: ON (N)" : "Peer name tag: OFF (N)", Array.Empty<GUILayoutOption>()))
		{
			PeerNameTagSync.Enabled = !PeerNameTagSync.Enabled;
		}
		if (GUILayout.Button(SharedVisionSync.Enabled ? "Peer vision cone: ON (F2, experimental)" : "Peer vision cone: OFF (F2, experimental)", Array.Empty<GUILayoutOption>()))
		{
			SharedVisionSync.Enabled = !SharedVisionSync.Enabled;
		}
		GUILayout.Space(8f);
		GUILayout.Label("Diagnostics", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button(VisionLightDiagnostics.Streaming ? "Vision light stream: ON (LogOutput.log, ~8Hz)" : "Vision light stream: OFF (cone + surrounding bubble - not the equipped lantern)", Array.Empty<GUILayoutOption>()))
		{
			VisionLightDiagnostics.ToggleStream();
		}
		if (GUILayout.Button("Dump ALL hotbar/inventory item types (LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			LampDiagnostics.DumpAllItemTypes();
		}
		GUILayout.Space(8f);
		GUILayout.Label("Enemy AI", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Spawn a Dog nearby (test ownership/loot bug anywhere, no need for an outside location)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.SpawnEnemyByName("Dog");
		}
		if (GUILayout.Button(EnemySync.AggroRedirectEnabled ? "Manual aggro redirect: ON" : "Manual aggro redirect: OFF (default - native AI already targets the peer on its own)", Array.Empty<GUILayoutOption>()))
		{
			EnemySync.AggroRedirectEnabled = !EnemySync.AggroRedirectEnabled;
		}
		GUILayout.Label("Fallback switch only - tested in real co-op with it off and enemies still attacked the peer correctly. Turn ON only if an enemy seems to ignore the peer.", Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Dump nearby enemies (real pos vs puppet target, LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.DumpNearbyEnemyPositions();
		}
		GUILayout.Label("One-shot snapshot of every living Character within 2000u of you - real position, ownership, and (if it has a network puppet component) how far the Lerp target has drifted from the real position. Use when something moves/attacks in a way that doesn't look physically possible.", Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.Label("Enemies hear your voice", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button(VoiceSync.AlertEnabled ? "Enabled" : "Disabled", Array.Empty<GUILayoutOption>()))
		{
			VoiceSync.AlertEnabled = !VoiceSync.AlertEnabled;
		}
		GUILayout.Label($"Sensitivity threshold: {VoiceSync.LoudVoiceRms:F3}  (last measured while speaking: {VoiceSync.LastAlertRms:F3})", Array.Empty<GUILayoutOption>());
		VoiceSync.LoudVoiceRms = GUILayout.HorizontalSlider(VoiceSync.LoudVoiceRms, 0.02f, 0.4f, Array.Empty<GUILayoutOption>());
		GUILayout.Label("Lower = easier to alert enemies. Higher = you need to shout. Watch \"last measured\" above while talking to find a good value for your mic.", Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.Label("Time", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Advance time +1h", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.AdvanceTime(60);
		}
		if (GUILayout.Button("Advance time +6h", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.AdvanceTime(360);
		}
		GUILayout.Label("Only \"sticks\" for real if clicked on the HOST (single source of truth for the clock, see ClockSync) - on the CLIENT the next heartbeat (~5s) overwrites it back.", Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.Label("Emergency", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Force-wake from dream (both players - only ONE of you click this)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.ForceEndDream();
		}
		GUILayout.Label("Only if actually stuck in a broken dream (blocked door, lock animation not running). Picks an automatic outcome and wakes the peer too - don't click on both sides at once.", Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.Label("Teleport to keyword (test a specific scripted scene, e.g. Doctor/Train Wreck)", GUI.skin.box, Array.Empty<GUILayoutOption>());
		_teleportKeyword = GUILayout.TextField(_teleportKeyword, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Teleport to nearest Character/Location matching keyword", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.TeleportToKeyword(_teleportKeyword);
		}
		if (GUILayout.Button("Dump ALL characters in scene (LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.DumpAllCharacters();
		}
		if (GUILayout.Button("Dump ALL locations in scene (LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.DumpAllLocations();
		}
		GUILayout.Space(4f);
		GUILayout.Label("Same Train Wreck spot for both paths - Wolfman's is the physical door+code confront, Musician's triggers a Dream instead. Which one fires depends on quest state, not position:", Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Force nearest un-fired event (bypasses quest-state gate, e.g. the Musician Dream trigger)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.ForceNearestEvent();
		}
		GUILayout.Label("Moves ONLY your position. Does NOT touch any save flag. Use F1 (PeerTeleport) after to pull your partner here too. If the keyword finds nothing, use the two Dump buttons to see the real object names and try again.", Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Unlock nearest locked door/padlock (no key/code needed)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.UnlockNearestLock();
		}
		GUILayout.Label("Native unlock() on the closest LOCKED lock/padlock - no need to find the real key/code item first. LockSync (already running) tells your partner automatically, same as a normal key.", Array.Empty<GUILayoutOption>());
		if (GUILayout.Button(DebugCheats.NoclipActive ? "Noclip: ON (walk out of stuck/no-collision spots)" : "Noclip: OFF", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.ToggleNoclip();
		}
		GUILayout.Label("Turns OFF your own collider + gravity - walk (WASD) through geometry/back onto solid ground if a teleport dropped you outside the map. Turn back OFF once you're standing somewhere safe.", Array.Empty<GUILayoutOption>());
		Color color = GUI.color;
		GUI.color = (Color)(DebugCheats.VanillaMode ? new Color(1f, 0.55f, 0.4f) : color);
		if (GUILayout.Button(DebugCheats.VanillaMode ? "Vanilla Mode: ON (WHOLE mod disabled, diagnostics still logging)" : "Vanilla Mode: OFF", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.VanillaMode = !DebugCheats.VanillaMode;
		}
		GUI.color = color;
		GUILayout.Label("Turns OFF the ENTIRE mod's sync/fixes (works by making CoopRuntime.SessionActive report false, which almost every sync system already gates on) - pure diagnostic logs (CutsceneDiag, QuestEventDiag, etc.) keep running regardless. Use this to see what the vanilla game itself does, with zero interference, even inside an active co-op session.", Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Dump Dream preset names (LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.DumpDreamPresets();
		}
		if (GUILayout.Button("Dump OutsideLocations state (LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.DumpOutsideLocationsState();
		}
		if (GUILayout.Button("Reset Train Wreck Dream state (clears stale spawnedLocations entry)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.ResetTrainWreckDreamState();
		}
		_teleportKeyword2 = GUILayout.TextField(_teleportKeyword2, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Talk to NPC by keyword (simulates mouse-click talk, no cursor needed)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.TalkToKeyword(_teleportKeyword2);
		}
		if (GUILayout.Button("Force Doctor dialogue (spawns a fresh Doctor if the original is gone)", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.ForceDoctorDialogue();
		}
		GUILayout.Space(8f);
		GUILayout.Label("Give item to your OWN inventory (keyword match, local only)", GUI.skin.box, Array.Empty<GUILayoutOption>());
		_giveItemKeyword = GUILayout.TextField(_giveItemKeyword, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Give 20x every item type matching keyword", Array.Empty<GUILayoutOption>()))
		{
			DebugCheats.GiveItemByKeyword(_giveItemKeyword, 20);
		}
		GUILayout.Label("Matches ANY real ItemsDatabase key containing the keyword (e.g. \"mushroom\" grabs all bio1/bio2/bio3 variants at once) - no guessing exact item names. Local only, no network packet.", Array.Empty<GUILayoutOption>());
		GUILayout.Space(6f);
		GUILayout.Label("~ closes this panel", GUI.skin.label, Array.Empty<GUILayoutOption>());
		GUILayout.EndScrollView();
		GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
	}

	private void StartAutoLoopback()
	{
		if (_session != null)
		{
			Log.LogWarning("Sessão já ativa — F10 para encerrar antes.");
		}
		else
		{
			if (_autoLoopbackPendingWorld)
			{
				return;
			}
			if ((Object)(object)Player.Instance == (Object)null)
			{
				if (TryAutoLoadSlot1())
				{
					_autoLoopbackPendingWorld = true;
					Log.LogInfo("[Auto] carregando save do slot 1 - conecta e equipa a lanterna sozinho assim que o mundo terminar.");
				}
			}
			else
			{
				DoStartAutoLoopback();
			}
		}
	}

	private void TickAutoLoopbackPendingWorld()
	{
		if (_autoLoopbackPendingWorld && !((Object)(object)Player.Instance == (Object)null) && Core.worldGenFinished())
		{
			_autoLoopbackPendingWorld = false;
			DoStartAutoLoopback();
		}
	}

	private bool TryAutoLoadSlot1()
	{
		SaveManager instance = Singleton<SaveManager>.Instance;
		UI instance2 = Singleton<UI>.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			Log.LogWarning("[Auto] SaveManager/UI ainda não prontos - espera o menu principal terminar de carregar antes de apertar F5.");
			return false;
		}
		MainMenu.SaveState val = instance.loadGameProfiles();
		if (val != null && val.profiles != null)
		{
			Core.profiles = val.profiles;
		}
		GameProfile val2 = Core.profiles?.Find((GameProfile p) => p != null && p.id == 1);
		if (val2 == null)
		{
			Log.LogWarning("[Auto] perfil de save #1 não encontrado - crie/salve no slot 1 antes de usar o F5 automático.");
			return false;
		}
		Core.currentProfile = val2;
		instance.updateFilePaths();
		((MonoBehaviour)instance2).StartCoroutine(instance2.initLoadGame());
		return true;
	}

	private void DoStartAutoLoopback()
	{
		NetRole role;
		LoopbackUdpTransport loopbackUdpTransport = LoopbackUdpTransport.CreateAuto(_localPort.Value, _remotePort.Value, out role);
		if (loopbackUdpTransport != null)
		{
			Log.LogInfo($"[Auto] esta instância será {role}.");
			_session = new CoopSession(loopbackUdpTransport, role);
			if (!_session.Start())
			{
				_session = null;
			}
		}
	}

	private void StartSession(NetRole role)
	{
		if (_session != null)
		{
			Log.LogWarning("Sessão já ativa — F10 para encerrar antes.");
			return;
		}
		ITransport transport = BuildTransport(role);
		if (transport == null)
		{
			return;
		}
		_session = new CoopSession(transport, role);
		if (!_session.Start())
		{
			_session = null;
			return;
		}
		LobbySync.Begin(role == NetRole.Host);
		if (role == NetRole.Host)
		{
			AutoJoin.SetHostRichPresence(_transportMode.Value);
			if (_transportMode.Value.Equals("LAN", StringComparison.OrdinalIgnoreCase))
			{
				LanSaveTransfer.StartServer(_lanPort.Value + 1);
			}
		}
	}

	internal bool StartClientSessionWith(ulong hostSteamId64)
	{
		if (_session != null)
		{
			Log.LogWarning("[AutoJoin] sessão já ativa — não reinicio.");
			return true;
		}
		if (!SteamAvailability.Available)
		{
			Log.LogError("[AutoJoin] convite Steam recebido, mas esta build do jogo não tem Steam (" + SteamAvailability.UnavailableReason + ") - use o modo LAN.");
			return false;
		}
		return StartClientSessionWithSteam(hostSteamId64);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool StartClientSessionWithSteam(ulong hostSteamId64)
	{
		_session = new CoopSession(new SteamP2PTransport(hostSteamId64), NetRole.Client);
		if (!_session.Start())
		{
			_session = null;
			return false;
		}
		LobbySync.Begin(isHost: false);
		Log.LogInfo($"[AutoJoin] sessão de cliente iniciada contra host {hostSteamId64}.");
		return true;
	}

	internal void StartHostSession()
	{
		if (!_transportMode.Value.Equals("LAN", StringComparison.OrdinalIgnoreCase))
		{
			if (SteamAvailability.Available)
			{
				_transportMode.Value = "Steam";
			}
			else
			{
				_transportMode.Value = "LAN";
				Log.LogWarning("[Modo] botão HOST: esta build do jogo não tem Steam (" + SteamAvailability.UnavailableReason + ") - modo trocado pra LAN. O CLIENTE precisa preencher LAN/PeerIP com o seu IP (painel `~`).");
			}
		}
		StartSession(NetRole.Host);
	}

	private static string SanitizeIpInput(string raw)
	{
		if (!string.IsNullOrEmpty(raw))
		{
			return raw.Replace(';', '.').Replace(',', '.');
		}
		return raw;
	}

	internal bool TryBeginLanJoin()
	{
		if (_session != null)
		{
			Log.LogWarning("Sessão já ativa — F10 para encerrar antes.");
			return true;
		}
		if (string.IsNullOrWhiteSpace(_lanPeerIp.Value))
		{
			LanSaveTransfer.ReportConfigError("LAN JOIN FAILED: no host IP configured. Press '0' to open the connection window and enter the host's IP first.");
			Log.LogWarning("[LAN] JOIN clicado mas LAN.PeerIP está vazio - abra a janela de conexão ('0') e digite o IP do host antes.");
			return false;
		}
		PrologueJoinSync.BeginLanJoin(_lanPeerIp.Value.Trim(), _lanPort.Value + 1);
		return true;
	}

	internal bool StartClientSessionLan()
	{
		Log.LogInfo($"[LanSaveTransfer] StartClientSessionLan() chamado (modo={_transportMode.Value}, LAN.PeerIP='{_lanPeerIp.Value}', sessão já existe={_session != null}).");
		if (_session != null)
		{
			Log.LogWarning("[LanSaveTransfer] sessão já ativa — não reinicio.");
			return true;
		}
		StartSession(NetRole.Client);
		return _session != null;
	}

	internal void StopSessionExternal()
	{
		StopSession();
	}

	private ITransport BuildTransport(NetRole role)
	{
		if (_transportMode.Value.Equals("Steam", StringComparison.OrdinalIgnoreCase))
		{
			if (!SteamAvailability.Available)
			{
				Log.LogError("Transporte 'Steam' pedido na config, mas esta build do jogo não tem Steam (" + SteamAvailability.UnavailableReason + "). Troque Transport/Mode para 'LAN' (e preencha LAN/PeerIP no cliente) no arquivo BepInEx/config/com.felipe.darkwoodcooponline.cfg, ou pelo painel `~` dentro do jogo.");
				return null;
			}
			return BuildSteamTransport(role);
		}
		return BuildNonSteamTransport(role);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private ITransport BuildSteamTransport(NetRole role)
	{
		ulong result;
		bool flag = ulong.TryParse(_peerSteamId.Value, out result) && result != 0;
		if (role == NetRole.Host)
		{
			return new SteamP2PTransport();
		}
		if (!flag)
		{
			Log.LogError("PeerSteamId64 inválido na config — defina o SteamID64 do outro jogador (ou use o botão JOIN do menu).");
			return null;
		}
		return new SteamP2PTransport(result);
	}

	private ITransport BuildNonSteamTransport(NetRole role)
	{
		if (_transportMode.Value.Equals("LAN", StringComparison.OrdinalIgnoreCase))
		{
			if (role == NetRole.Host)
			{
				return LoopbackUdpTransport.CreateLanHost(_lanPort.Value);
			}
			string text = SanitizeIpInput(_lanPeerIp.Value.Trim());
			if (string.IsNullOrWhiteSpace(text) || !IPAddress.TryParse(text, out IPAddress address))
			{
				Log.LogError("LAN.PeerIP inválido/vazio na config — defina o IP do host (o IP virtual do Radmin/Hamachi, por exemplo).");
				return null;
			}
			return LoopbackUdpTransport.CreateLanClient(_lanPort.Value + 2, _lanPort.Value, address);
		}
		return new LoopbackUdpTransport(_localPort.Value, _remotePort.Value);
	}

	private void StopSession()
	{
		_session?.Stop();
		_session = null;
		AutoJoin.ClearHostRichPresence();
		LanSaveTransfer.StopServer();
		UiPauseSuppress.Reset();
		Log.LogInfo("[Session] encerrada.");
	}

	private void OnDestroy()
	{
		StopSession();
	}
}
