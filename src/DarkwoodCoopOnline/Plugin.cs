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

[BepInPlugin("com.felipe.darkwoodcooponline", "Darkwood Co-op Online", "0.4.6")]
public class Plugin : BaseUnityPlugin
{
	public const string Version = "0.4.6";

	internal static TimestampedLogSource Log;

	private ConfigEntry<bool> _debugKeys;

	private ConfigEntry<bool> _voiceEnabled;

	private ConfigEntry<string> _voiceDevice;

	private CoopSession _session;

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

	internal static bool TransportModeIsLan => false;

	internal bool HasSession => _session != null;

	private void Awake()
	{
		Log = new TimestampedLogSource(Logger);
		Instance = this;
		Version version = Assembly.GetExecutingAssembly().GetName().Version;
		Version version2 = new Version("0.4.6.0");
		if (version != version2)
		{
			Log.LogError("[VersionCheck] DESSINCRONIA: Plugin.Version='0.4.6' mas AssemblyVersion " + $"compilada='{version}' - alguém bumpou só um dos dois (Properties/AssemblyInfo.cs " + "precisa acompanhar Plugin.Version SEMPRE). Corrija antes de publicar.");
		}
		else
		{
			Log.LogInfo("[VersionCheck] OK - Plugin.Version e AssemblyVersion batem (0.4.6).");
		}
		Application.runInBackground = true;
		ConfigFile cfg = ((BaseUnityPlugin)this).Config;
		SafeStart("Config", delegate { CoopConfig.Bind(cfg); });
		TimestampedLogSource.Verbose = ((BaseUnityPlugin)this).Config.Bind<bool>("Debug", "VerboseLog", false, "Write the mod's detailed diagnostics to the log (big log files, a little slower). Turn on only to report a bug.").Value;
		_debugKeys = ((BaseUnityPlugin)this).Config.Bind<bool>("Debug", "DiagnosticHotkeys", false, "Developer diagnostic keys (F11/F12/Insert/Home/End/Delete/PageUp/PageDown/Backspace/keypad). Several start per-frame logging that slows the game - leave off unless you are debugging.");
		_voiceEnabled = ((BaseUnityPlugin)this).Config.Bind<bool>("Voice", "Enabled", false, "Voice chat por proximidade (grava do microfone só durante sessão pareada)");
		_voiceDevice = ((BaseUnityPlugin)this).Config.Bind<string>("Voice", "Device", "", "Nome do dispositivo de microfone (Microphone.devices) — vazio usa o 1º disponível");
		Log.LogInfo($"=== Darkwood Co-op Online {Version} ({Proto.MaxPlayers} players) loaded ===  HOST/JOIN from the main menu, F10 = leave, 0 = co-op window, ~ = debug panel");
		SafeStart("DownedSync", delegate { DownedSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PlayerDamageSync", delegate { PlayerDamageSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("NightEventSync", delegate { NightEventSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("DamageDiagnostics", delegate { DamageDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("NpcDeathDiagnostics", delegate { NpcDeathDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PuppetAttackSuppress", delegate { PuppetAttackSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PuppetAnimSuppress", delegate { PuppetAnimSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("MainMenuNativeButtons", delegate { MainMenuNativeButtons.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("DurabilityDiagnostics", delegate { DurabilityDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("ItemBurnSync", delegate { ItemBurnSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("MonsterRangedAttackSync", delegate { MonsterRangedAttackSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("WildlifeRespawnSync", delegate { WildlifeRespawnSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("EnemyHitAnimSync", delegate { EnemyHitAnimSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("LiquidSync", delegate { LiquidSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("LocationOwnership", delegate { LocationOwnership.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("MapPauseSuppress", delegate { MapPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("JournalPauseSuppress", delegate { JournalPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("OvenPauseSuppress", delegate { OvenPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("UiPauseSuppress", delegate { UiPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("EscMenuPauseSuppress", delegate { EscMenuPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("DreamTransitionPauseSuppress", delegate { DreamTransitionPauseSuppress.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("FlareSync", delegate { FlareSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("NightScenarioSync", delegate { NightScenarioSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("FlagSync", delegate { FlagSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("ChapterTransitionSync", delegate { ChapterTransitionSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("WorldGenDiagnostics", delegate { WorldGenDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("TrapSpawnSync", delegate { TrapSpawnSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("TrapPlaceSync", delegate { TrapPlaceSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("FirearmFxSync", delegate { FirearmFxSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("ThrownItemSync", delegate { ThrownItemSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("WorldEventSync", delegate { WorldEventSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("CharacterMessageSync", delegate { CharacterMessageSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("MapMarkerSync", delegate { MapMarkerSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("LocationDiscoverySync", delegate { LocationDiscoverySync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("OutsideLocationSync", delegate { OutsideLocationSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("DreamSync", delegate { DreamSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("DoorLockEventSync", delegate { DoorLockEventSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("GameObjectActionSync", delegate { GameObjectActionSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("ReturnToMenuSync", delegate { ReturnToMenuSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("QuestEventDiagnostics", delegate { QuestEventDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("CutsceneDiagnostics", delegate { CutsceneDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("QuestSpawnSync", delegate { QuestSpawnSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("WildlifeActivationSync", delegate { WildlifeActivationSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PlayerActionSoundSync", delegate { PlayerActionSoundSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("MorningRitualSync", delegate { MorningRitualSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("ExplodableSync", delegate { ExplodableSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("DebugCheatKeys", delegate { DebugCheatKeys.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("BansheeScreamSync", delegate { BansheeScreamSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("InSightSync", delegate { InSightSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PropSpawnSync", delegate { PropSpawnSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("CharacterSaveSync", delegate { CharacterSaveSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PrologueDiagnostics", delegate { PrologueDiagnostics.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PrologueEncounterPullSync", delegate { PrologueEncounterPullSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("WorldSync", delegate { WorldSync.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("LanSaveTransfer", delegate { LanSaveTransfer.EnsurePatched("com.felipe.darkwoodcooponline"); });
		SafeStart("PrologueJoinSync", delegate { PrologueJoinSync.EnsurePatched(); });
		SafeStart("EnemySync", delegate { EnemySync.EnsurePatched("com.felipe.darkwoodcooponline"); });
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

	// One part failing to start (a game update changed a method it patches...) must not take the rest of
	// the mod - the HOST / JOIN buttons included - down with it
	private static void SafeStart(string what, Action start)
	{
		try
		{
			start();
		}
		catch (Exception ex)
		{
			Log.LogError("[Startup] " + what + " failed to start (the rest of the mod keeps going): " + ex);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void StartSteamOnlyServices()
	{
		SteamCarrier.Start();
		// "Join game" from the Steam friends list / an accepted invite
		SteamCarrier.OnJoinRequested = delegate(ulong host)
		{
			if (CoopConnection.Current == null)
			{
				CoopUI.Close();
				JoinFlow.JoinSteam(host, SteamCarrier.NameOf(host));
			}
		};
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

	private string _lastUpdateError;

	private void Update()
	{
		// the main menu's HOST / JOIN first, on their own: nothing else failing may hide them
		try
		{
			MainMenuNativeButtons.Tick();
			MainMenuNativeButtons.TickPendingLabels();
			MainMenuHostJoinUI.Tick();
			CoopMenuList.Tick();
		}
		catch (Exception ex)
		{
			ReportUpdateError("menu", ex);
		}
		try
		{
			UpdateCoop();
		}
		catch (Exception ex2)
		{
			ReportUpdateError("update", ex2);
		}
	}

	private void ReportUpdateError(string where, Exception ex)
	{
		string text = where + ": " + ex.GetType().Name + ": " + ex.Message;
		if (text != _lastUpdateError)
		{
			_lastUpdateError = text;
			Log.LogError("[Startup] " + where + " error (shown once): " + ex);
		}
	}

	private void UpdateCoop()
	{
		PauseSync.ReassertTimeScale();
		PerfDiagnostics.FrameStart();
		if (SteamAvailability.Available)
		{
			PumpSteam();
		}
		JoinFlow.Tick(Time.unscaledDeltaTime);
		HostFlow.Tick();
		CoopUI.Tick();
		if (Input.GetKeyDown((KeyCode)288) && _session == null && (Object)(object)Player.Instance != (Object)null && !CoopUI.IsOpen)
		{
			HostFlow.StartHosting();
		}
		if (Input.GetKeyDown((KeyCode)289) && _session == null && (Object)(object)Player.Instance == (Object)null && !CoopUI.IsOpen)
		{
			CoopUI.OpenJoin();
		}
		SkinExport.Tick();
		if (_debugKeys.Value)
		{
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
		}
		CutsceneDiagnostics.Tick();
		TrainWreckEncounterPullSync.Tick();
		ForbidInputsWatchdog.Tick();
		WorldGenDiagnostics.Tick();
		ChapterTransitionSync.Tick();
		CutscenePlayerVisibilitySync.Tick();
		PlayerVisionWatchdog.Tick();
		CharacterMessageSync.Tick();
		DreamSync.Tick();
		if (_debugKeys.Value)
		{
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
		}
		AnimationSyncDiagnostics.Tick();
		DayTransitionDiagnostics.Tick();
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
		if (_debugKeys.Value)
		{
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
			string text = _session.EndReason ?? "The host left the game.";
			StopSession();
			CoopNotice.Show(text + " You keep playing alone - REJOIN from the main menu when they host again.", 12f);
		}
		PerfDiagnostics.FrameEnd();
	}

	private void OnGUI()
	{
		try
		{
			MainMenuHostJoinUI.Draw();
		}
		catch (Exception ex)
		{
			ReportUpdateError("menu draw", ex);
		}
		try
		{
			DrawCoopGui();
		}
		catch (Exception ex2)
		{
			ReportUpdateError("draw", ex2);
		}
	}

	private void DrawCoopGui()
	{
		CoopUI.Draw();
		CoopNotice.Draw();
		SaveSlotPickerUI.Draw();
		VoiceSettingsUI.Draw();
		LobbySync.Draw();
		// co-op info only when you want it: in the pause menu, or while holding Tab
		bool flag = CoopUI.PauseMenuOpen;
		if (_showOverlay && _session != null && (flag || Input.GetKey((KeyCode)9)))
		{
			CoopUI.DrawHud(_session);
		}
		if (flag && _session != null)
		{
			CoopUI.DrawPauseBox(_session);
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
		if (Core.mainMenu || flag)
		{
			DrawCredit();
		}
		if (_session != null && _session.Linked)
		{
			DownedUI.Draw(CoopPeers.Nearest((Object)(object)Player.Instance != (Object)null ? ((Component)Player.Instance).transform.position : Vector3.zero, (RemotePlayerPuppet pp) => DownedSync.IsPeerDowned(pp.Slot)) ?? _session.Puppet);
			PauseUI.Draw(_session.Role);
			foreach (RemotePlayerPuppet puppet in _session.PuppetList)
			{
				PeerNameTagSync.Draw(_session, puppet);
			}
		}
	}

	private void DrawBottomStatus()
	{
		if (LanSaveTransfer.IsActive)
		{
			DrawStatusBanner(LanSaveTransfer.StatusText, LanSaveTransfer.IsFailed);
		}
	}

	private void DrawStatusBanner(string statusText, bool failed)
	{
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
		if (_creditLabel == null)
		{
			_creditLabel = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12
			};
			_creditLabel.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
		}
		GUI.Label(new Rect(8f, (float)Screen.height - 24f, 700f, 20f), "Developed by Felipesfx with AI help · 4-player networking modelled on Moemon Kai Co-op", _creditLabel);
		GUI.Label(new Rect(8f, (float)Screen.height - 40f, 400f, 20f), "Darkwood Co-op Online v" + Version + " (4 players)", _creditLabel);
	}

	private void DrawConnectionPanel(int id)
	{
		CoopConnection current = CoopConnection.Current;
		if (current == null)
		{
			GUILayout.Label("Not in a co-op game. Use HOST A GAME / JOIN A GAME on the main menu.", GUI.skin.box, Array.Empty<GUILayoutOption>());
		}
		else
		{
			GUILayout.Label((current.IsHost ? "Hosting" : "Playing in") + " \"" + current.RoomName + "\"", GUI.skin.box, Array.Empty<GUILayoutOption>());
			for (int i = 0; i < Proto.MaxPlayers; i++)
			{
				CoopConnection.Peer peer = current.Roster[i];
				if (peer != null || i == current.MySlot)
				{
					GUILayout.Label($"  {i + 1}. {((i == current.MySlot) ? (CoopConfig.MyName() + " (you)") : peer.Name)}{((i == 0) ? "  [host]" : "")}{((peer != null && !peer.InGame && i != current.MySlot) ? "  (loading)" : "")}", Array.Empty<GUILayoutOption>());
				}
			}
			if (!current.IsHost && current.RttMs >= 0)
			{
				GUILayout.Label($"Ping to host: {current.RttMs} ms", Array.Empty<GUILayoutOption>());
			}
			if (current.IsHost)
			{
				GUILayout.Space(4f);
				GUILayout.Label(HostFlow.InviteLine, Array.Empty<GUILayoutOption>());
				if (!string.IsNullOrEmpty(HostFlow.Code) && GUILayout.Button("Copy invite code " + HostFlow.Code, Array.Empty<GUILayoutOption>()))
				{
					GUIUtility.systemCopyBuffer = HostFlow.Code;
					CoopNotice.Show("Invite code copied");
				}
				if (HostFlow.BackupSteamCode != null && GUILayout.Button("Copy Steam invite code " + HostFlow.BackupSteamCode + " (if the other doesn't connect)", Array.Empty<GUILayoutOption>()))
				{
					GUIUtility.systemCopyBuffer = HostFlow.BackupSteamCode;
					CoopNotice.Show("Steam invite code copied");
				}
				string text = PortMapper.LanAddress();
				if (text != null)
				{
					GUILayout.Label("Same network: " + text + ((current.Server.Port != Proto.DefaultPort) ? (":" + current.Server.Port) : ""), Array.Empty<GUILayoutOption>());
				}
				if (PortMapper.FindVpnAddress(out var address, out var name))
				{
					GUILayout.Label(name + ": " + address, Array.Empty<GUILayoutOption>());
				}
			}
			if (GUILayout.Button(current.IsHost ? "Stop hosting (F10)" : "Leave the game (F10)", Array.Empty<GUILayoutOption>()))
			{
				StopSession();
			}
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
		_debugPanelScroll = GUILayout.BeginScrollView(_debugPanelScroll, (GUILayoutOption[])(object)new GUILayoutOption[2]
		{
			GUILayout.Width(460f),
			GUILayout.Height(Mathf.Min((float)Screen.height - 100f, 700f))
		});
		GUILayout.Label((_session != null) ? string.Format("Session: {0} - {1}", _session.Role, _session.Linked ? "connected" : "waiting...") : "Session: none", Array.Empty<GUILayoutOption>());
		GUILayout.Space(6f);
		GUILayout.Label("Menu UI (investigação botão nativo)", GUI.skin.box, Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Create+dump HOST/JOIN native buttons (must be at MAIN MENU, LogOutput.log)", Array.Empty<GUILayoutOption>()))
		{
			MainMenuNativeButtons.DumpDiagnostics();
		}
		GUILayout.Space(6f);
		GUILayout.Label("Connection", GUI.skin.box, Array.Empty<GUILayoutOption>());
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (GUILayout.Button("Host now (F7)", Array.Empty<GUILayoutOption>()) && _session == null && (Object)(object)Player.Instance != (Object)null)
		{
			HostFlow.StartHosting();
		}
		if (GUILayout.Button("Stop (F10)", Array.Empty<GUILayoutOption>()))
		{
			StopSession();
		}
		GUILayout.EndHorizontal();
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

	// HostFlow / JoinFlow hand the session they made to the plugin, which runs it every frame
	internal void AdoptSession(CoopSession session)
	{
		if (_session != null && _session != session)
		{
			StopSession();
		}
		_session = session;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void PumpSteam()
	{
		SteamCarrier.Pump();
	}

	internal void StopSessionExternal()
	{
		StopSession();
	}

	private void StopSession()
	{
		_session?.Stop();
		_session = null;
		CoopConnection.Current?.Close();
		CoopConnection.Current = null;
		HostFlow.Stop();
		LanSaveTransfer.StopServer();
		UiPauseSuppress.Reset();
		Log.LogInfo("[Session] encerrada.");
	}

	private void OnDestroy()
	{
		StopSession();
	}
}
