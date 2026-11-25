using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

public sealed class CoopSession
{
	private readonly ITransport _transport;

	private readonly NetRole _role;

	private RemotePlayerPuppet _puppet;

	private bool _linked;

	private float _sendTimer;

	private const float SendInterval = 0.05f;

	private float _enemyTimer;

	private const float EnemyInterval = 0.05f;

	private float _doorTimer;

	private const float DoorInterval = 0.1f;

	private float _doorTimer2;

	private const float FurnInterval = 0.05f;

	private float _clockTimer;

	private const float ClockInterval = 5f;

	private float _shadowTimer;

	private const float ShadowInterval = 0.1f;

	private Queue<byte[]> _pendingSnapshotChunks;

	private const int SnapshotChunksPerTick = 4;

	private string _resolvedPeerIdentity;

	public bool PeerLeftSignaled { get; private set; }

	public NetRole Role => _role;

	public bool Linked => _linked;

	public RemotePlayerPuppet Puppet => _puppet;

	public string PeerIdentity => _transport?.PeerIdentity;

	public string ResolvedPeerIdentity
	{
		get
		{
			string text = _resolvedPeerIdentity;
			if (text == null)
			{
				ITransport transport = _transport;
				if (transport == null)
				{
					return null;
				}
				text = transport.PeerIdentity;
			}
			return text;
		}
	}

	public void SendReliable(byte[] data)
	{
		if (_transport != null && _transport.Connected)
		{
			_transport.Send(data, reliable: true);
		}
	}

	public void EnqueuePacedChunks(IEnumerable<byte[]> packets)
	{
		if (_pendingSnapshotChunks == null)
		{
			_pendingSnapshotChunks = new Queue<byte[]>();
		}
		foreach (byte[] packet in packets)
		{
			_pendingSnapshotChunks.Enqueue(packet);
		}
	}

	public CoopSession(ITransport transport, NetRole role)
	{
		_transport = transport;
		_role = role;
	}

	public bool Start()
	{
		CoopRuntime.SetSession(this);
		try
		{
			_transport.Start(_role);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError($"[Session] falha ao iniciar o transporte ({_role}): {ex.GetType().Name}: {ex.Message} - " + "se for LAN testando na MESMA máquina, confirme que HOST e CLIENTE não estão usando a MESMA porta (LAN.Port).");
			CoopRuntime.SetSession(null);
			return false;
		}
		if (_role == NetRole.Client)
		{
			_transport.Send(PacketWriter.Begin(PacketType.Hello).Byte(1).Str(StableIdentity.Resolve())
				.Finish(), reliable: true);
		}
		Plugin.Log.LogInfo($"[Session] iniciada como {_role}");
		return true;
	}

	public void Update()
	{
		//IL_01e0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e2: Unknown result type (might be due to invalid IL or missing references)
		//IL_02e4: Unknown result type (might be due to invalid IL or missing references)
		//IL_0227: Unknown result type (might be due to invalid IL or missing references)
		//IL_023c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0251: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0616: Unknown result type (might be due to invalid IL or missing references)
		//IL_0607: Unknown result type (might be due to invalid IL or missing references)
		//IL_061b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0627: Unknown result type (might be due to invalid IL or missing references)
		//IL_0629: Unknown result type (might be due to invalid IL or missing references)
		//IL_073d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0742: Unknown result type (might be due to invalid IL or missing references)
		//IL_065c: Unknown result type (might be due to invalid IL or missing references)
		//IL_065e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0761: Unknown result type (might be due to invalid IL or missing references)
		//IL_0752: Unknown result type (might be due to invalid IL or missing references)
		//IL_0766: Unknown result type (might be due to invalid IL or missing references)
		//IL_0772: Unknown result type (might be due to invalid IL or missing references)
		//IL_0774: Unknown result type (might be due to invalid IL or missing references)
		//IL_079e: Unknown result type (might be due to invalid IL or missing references)
		//IL_07a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_07ca: Unknown result type (might be due to invalid IL or missing references)
		//IL_07cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f6: Unknown result type (might be due to invalid IL or missing references)
		//IL_07f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0822: Unknown result type (might be due to invalid IL or missing references)
		//IL_0824: Unknown result type (might be due to invalid IL or missing references)
		//IL_084e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0850: Unknown result type (might be due to invalid IL or missing references)
		//IL_087a: Unknown result type (might be due to invalid IL or missing references)
		//IL_087c: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a6: Unknown result type (might be due to invalid IL or missing references)
		//IL_08a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_08d4: Unknown result type (might be due to invalid IL or missing references)
		//IL_095c: Unknown result type (might be due to invalid IL or missing references)
		//IL_095e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0988: Unknown result type (might be due to invalid IL or missing references)
		//IL_098a: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b4: Unknown result type (might be due to invalid IL or missing references)
		//IL_09b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a08: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a0a: Unknown result type (might be due to invalid IL or missing references)
		if (_transport == null || !_transport.Connected)
		{
			return;
		}
		_transport.Pump();
		PerfDiagnostics.Begin("Receive");
		byte[] array = null;
		byte[] array2 = null;
		byte[] array3 = null;
		byte[] array4 = null;
		byte[] data;
		while (_transport.TryReceive(out data))
		{
			if (data.Length != 0)
			{
				switch ((PacketType)data[0])
				{
				case PacketType.PlayerState:
					array = data;
					break;
				case PacketType.EnemyDelta:
					array2 = data;
					break;
				case PacketType.FurnitureDelta:
					array3 = data;
					break;
				case PacketType.DoorSwing:
					array4 = data;
					break;
				default:
					Handle(data);
					break;
				}
			}
		}
		if (array != null)
		{
			Handle(array);
		}
		if (array2 != null)
		{
			Handle(array2);
		}
		if (array3 != null)
		{
			Handle(array3);
		}
		if (array4 != null)
		{
			Handle(array4);
		}
		PerfDiagnostics.End();
		if (_pendingSnapshotChunks != null)
		{
			for (int i = 0; i < 4; i++)
			{
				if (_pendingSnapshotChunks.Count <= 0)
				{
					break;
				}
				_transport.Send(_pendingSnapshotChunks.Dequeue(), reliable: true);
			}
			if (_pendingSnapshotChunks.Count == 0)
			{
				_pendingSnapshotChunks = null;
			}
		}
		if (!_linked)
		{
			return;
		}
		_sendTimer += Time.deltaTime;
		if (_sendTimer >= 0.05f)
		{
			_sendTimer = 0f;
			PerfDiagnostics.Begin("PlayerState.Build");
			byte[] array5 = PlayerSync.BuildLocalState();
			if (array5 != null)
			{
				_transport.Send(array5, reliable: false);
			}
			PerfDiagnostics.End();
		}
		byte[] array6 = VoiceSync.PollOutgoing();
		if (array6 != null)
		{
			_transport.Send(array6, reliable: false);
		}
		VoiceSync.Tick(_puppet);
		_enemyTimer += Time.deltaTime;
		if (_enemyTimer >= 0.05f && (Object)(object)Player.Instance != (Object)null)
		{
			_enemyTimer = 0f;
			PerfDiagnostics.Begin("Enemy.Build");
			Vector3 position = ((Component)Player.Instance).transform.position;
			Vector3 p = (((Object)(object)_puppet != (Object)null) ? ((Component)_puppet).transform.position : position);
			if ((Object)(object)_puppet != (Object)null)
			{
				LocationOwnership.WakeNearbyInactiveTick(((Component)_puppet).transform.position);
				EnemySync.WakeOwnedNearPeer(((Component)_puppet).transform.position);
				EnemySync.WakePuppetsNearPeer(((Component)_puppet).transform.position);
				NightEventSync.SetPeerPosition(((Component)_puppet).transform.position);
				if (DownedSync.PeerReallyDead)
				{
					EnemySync.ReleaseAggroFromPuppet(((Component)_puppet).transform);
				}
				else if (!DownedSync.PeerDowned)
				{
					EnemySync.CheckAggroRedirect(((Component)Player.Instance).transform, ((Component)_puppet).transform);
					EnemySync.CheckAoeDamageToPeer(((Component)_puppet).transform);
					EnemySync.SyncCombatTensionForPeer(((Component)_puppet).transform);
				}
				else
				{
					EnemySync.ReleaseAggroFromPuppet(((Component)_puppet).transform);
				}
			}
			_transport.Send(EnemySync.BuildDelta(position, p), reliable: false);
			byte[] array7 = EnemySync.BuildDeaths();
			if (array7 != null)
			{
				_transport.Send(array7, reliable: true);
			}
			byte[] array8 = EnemySync.BuildRemovals();
			if (array8 != null)
			{
				_transport.Send(array8, reliable: true);
			}
			byte[] array9 = EnemySync.BuildSpawns();
			if (array9 != null)
			{
				_transport.Send(array9, reliable: true);
			}
			byte[] array10 = EnemyHitAnimSync.PollOutgoing();
			if (array10 != null)
			{
				_transport.Send(array10, reliable: true);
			}
			PerfDiagnostics.End();
		}
		EnemySync.PruneLocalOrphans();
		NightEventSync.Tick();
		byte[] array11 = CorpseSync.PollFoundCorpses();
		if (array11 != null)
		{
			_transport.Send(array11, reliable: true);
		}
		byte[] array12 = FlareSync.PollSettled();
		if (array12 != null)
		{
			_transport.Send(array12, reliable: true);
		}
		byte[] array13 = ThrownItemSync.PollSettled();
		if (array13 != null)
		{
			_transport.Send(array13, reliable: true);
		}
		byte[] array14 = CharacterMessageSync.PollNew();
		if (array14 != null)
		{
			_transport.Send(array14, reliable: true);
		}
		byte[] array15 = InSightSync.PollNew();
		if (array15 != null)
		{
			_transport.Send(array15, reliable: true);
		}
		_shadowTimer += Time.deltaTime;
		if (_shadowTimer >= 0.1f)
		{
			_shadowTimer = 0f;
			PerfDiagnostics.Begin("Shadow.Build");
			byte[] array16 = ShadowSync.BuildDelta();
			if (array16 != null)
			{
				_transport.Send(array16, reliable: false);
			}
			byte[] array17 = ShadowSync.BuildDeaths();
			if (array17 != null)
			{
				_transport.Send(array17, reliable: true);
			}
			byte[] array18 = ShadowSync.BuildSpawns();
			if (array18 != null)
			{
				_transport.Send(array18, reliable: true);
			}
			PerfDiagnostics.End();
		}
		LobbySync.Tick();
		PerfDiagnostics.Begin("Downed.Tick");
		DownedSync.Tick(Time.deltaTime, _puppet);
		_puppet?.SetDownedVisual(DownedSync.PeerDowned, DownedSync.Downed);
		PerfDiagnostics.End();
		byte[] array19 = EnemySync.BuildHitRequests();
		if (array19 != null)
		{
			_transport.Send(array19, reliable: true);
		}
		byte[] array20 = EnemySync.BuildBehaviorRequests();
		if (array20 != null)
		{
			_transport.Send(array20, reliable: true);
		}
		PerfDiagnostics.Begin("Clock.Poll");
		byte[] array21 = ClockSync.PollLocalResume();
		if (array21 != null)
		{
			_transport.Send(array21, reliable: true);
		}
		if (_role == NetRole.Host)
		{
			byte[] array22 = ClockSync.PollDayAdvance();
			if (array22 != null)
			{
				_transport.Send(array22, reliable: true);
			}
		}
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Pause.Poll");
		byte[] array23 = PauseSync.PollLocalChange();
		if (array23 != null)
		{
			_transport.Send(array23, reliable: true);
		}
		PerfDiagnostics.End();
		if (_role == NetRole.Host)
		{
			_clockTimer += Time.deltaTime;
			if (_clockTimer >= 5f)
			{
				_clockTimer = 0f;
				byte[] array24 = ClockSync.BuildHeartbeat();
				if (array24 != null)
				{
					_transport.Send(array24, reliable: true);
				}
				byte[] array25 = RainSync.BuildHeartbeat();
				if (array25 != null)
				{
					_transport.Send(array25, reliable: true);
				}
			}
		}
		_doorTimer2 += Time.deltaTime;
		if (_doorTimer2 >= 0.05f && (Object)(object)Player.Instance != (Object)null)
		{
			_doorTimer2 = 0f;
			Vector3 position2 = ((Component)Player.Instance).transform.position;
			Vector3 p2 = (((Object)(object)_puppet != (Object)null) ? ((Component)_puppet).transform.position : position2);
			PerfDiagnostics.Begin("Furniture.Poll");
			byte[] array26 = FurnitureSync.PollDrags(position2, p2, _role == NetRole.Host);
			if (array26 != null)
			{
				_transport.Send(array26, reliable: false);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("DoorSwing.Poll");
			byte[] array27 = DoorSwingSync.PollSwings(position2, p2);
			if (array27 != null)
			{
				_transport.Send(array27, reliable: false);
			}
			PerfDiagnostics.End();
		}
		PerfDiagnostics.Begin("Furniture.Tick");
		FurnitureSync.Tick();
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("DoorSwing.Tick");
		DoorSwingSync.Tick();
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Drop.PollDestroys");
		byte[] array28 = DropSync.PollDestroys();
		if (array28 != null)
		{
			_transport.Send(array28, reliable: true);
		}
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Trap.PollDestroys");
		byte[] array29 = TrapSync.PollDestroys();
		if (array29 != null)
		{
			_transport.Send(array29, reliable: true);
		}
		PerfDiagnostics.End();
		_doorTimer += Time.deltaTime;
		if (_doorTimer >= 0.1f && (Object)(object)Player.Instance != (Object)null)
		{
			_doorTimer = 0f;
			Vector3 position3 = ((Component)Player.Instance).transform.position;
			Vector3 p3 = (((Object)(object)_puppet != (Object)null) ? ((Component)_puppet).transform.position : position3);
			PerfDiagnostics.Begin("Door.Poll");
			byte[] array30 = PropSync.PollChanges(position3, p3);
			if (array30 != null)
			{
				_transport.Send(array30, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Lamp.Poll");
			byte[] array31 = LampSync.PollChanges(position3, p3);
			if (array31 != null)
			{
				_transport.Send(array31, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Trap.Poll");
			byte[] array32 = TrapSync.PollChanges(position3, p3);
			if (array32 != null)
			{
				_transport.Send(array32, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Window.Poll");
			byte[] array33 = WindowSync.PollChanges(position3, p3);
			if (array33 != null)
			{
				_transport.Send(array33, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("DoorBarricade.Poll");
			byte[] array34 = DoorBarricadeSync.PollChanges(position3, p3);
			if (array34 != null)
			{
				_transport.Send(array34, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Generator.Poll");
			byte[] array35 = GeneratorSync.PollChanges(position3, p3);
			if (array35 != null)
			{
				_transport.Send(array35, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Saw.Poll");
			byte[] array36 = SawSync.PollChanges(position3, p3);
			if (array36 != null)
			{
				_transport.Send(array36, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Drop.PollCreates");
			byte[] array37 = DropSync.PollCreates(position3, p3);
			if (array37 != null)
			{
				_transport.Send(array37, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Container.Poll");
			List<byte[]> list = ContainerSync.PollChanges(position3, p3, _role == NetRole.Host);
			if (list != null)
			{
				foreach (byte[] item in list)
				{
					_transport.Send(item, reliable: true);
				}
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("KeyItem.Poll");
			byte[] array38 = KeyItemSync.PollLocalPickups();
			if (array38 != null)
			{
				_transport.Send(array38, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Lock.Poll");
			byte[] array39 = LockSync.PollChanges(position3, p3);
			if (array39 != null)
			{
				_transport.Send(array39, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Constructible.Poll");
			byte[] array40 = ConstructibleSync.PollChanges(position3, p3);
			if (array40 != null)
			{
				_transport.Send(array40, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("Workbench.Poll");
			byte[] array41 = WorkbenchSync.PollChanges(position3, p3);
			if (array41 != null)
			{
				_transport.Send(array41, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("PlayerBurn.Poll");
			byte[] array42 = PlayerBurnVisualSync.PollChanges();
			if (array42 != null)
			{
				_transport.Send(array42, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("DialogueState.Poll");
			byte[] array43 = DialogueStateSync.PollChanges(position3, p3);
			if (array43 != null)
			{
				_transport.Send(array43, reliable: true);
			}
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("NpcState.Poll");
			byte[] array44 = NpcStateSync.PollChanges();
			if (array44 != null)
			{
				_transport.Send(array44, reliable: true);
			}
			PerfDiagnostics.End();
		}
		byte[] array45 = CharacterSaveSync.PollUpload(Time.deltaTime, _role == NetRole.Client);
		if (array45 != null)
		{
			_transport.Send(array45, reliable: true);
		}
	}

	private void EnsurePuppet()
	{
		if ((Object)(object)_puppet == (Object)null && (Object)(object)Player.Instance != (Object)null)
		{
			_puppet = RemotePlayerPuppet.Create();
		}
	}

	private void LogSessionIdentities()
	{
		string myName = "?";
		string myId = "?";
		string peerName = "?";
		string peerId = _transport?.PeerIdentity ?? "?";
		if (SteamAvailability.Available)
		{
			ReadSteamIdentities(ref myName, ref myId, ref peerName, ref peerId);
		}
		Plugin.Log.LogInfo($"[Session] EU='{myName}' (steamid={myId}) papel={_role} <-> PEER='{peerName}' (steamid={peerId}) " + "| identidadeEstávelMinha='" + StableIdentity.Resolve() + "' identidadeEstávelPeer='" + ResolvedPeerIdentity + "'");
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ReadSteamIdentities(ref string myName, ref string myId, ref string peerName, ref string peerId)
	{
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		if (SteamManager.Initialized)
		{
			myName = SteamFriends.GetPersonaName();
			myId = SteamUser.GetSteamID().m_SteamID.ToString();
			if (peerId.StartsWith("steam:") && ulong.TryParse(peerId.Substring(6), out var result))
			{
				peerName = SteamFriends.GetFriendPersonaName(new CSteamID(result));
				peerId = result.ToString();
			}
		}
	}

	private void Handle(byte[] data)
	{
		PacketReader packetReader = new PacketReader(data);
		switch (packetReader.Type)
		{
		case PacketType.PeerLeaving:
			Plugin.Log.LogInfo("[Session] peer avisou que voltou pro menu principal - encerrando minha sessão localmente.");
			PeerLeftSignaled = true;
			break;
		case PacketType.Hello:
		{
			NetRole netRole = (NetRole)packetReader.Byte();
			string text = packetReader.Str();
			_resolvedPeerIdentity = (string.IsNullOrEmpty(text) ? null : text);
			Plugin.Log.LogInfo($"[Session] HELLO do peer (role={netRole}, identidade estável='{text}')");
			EnsurePuppet();
			_linked = true;
			LogSessionIdentities();
			LobbySync.MarkPeerConnected();
			if (_role == NetRole.Host)
			{
				_transport.Send(PacketWriter.Begin(PacketType.Hello).Byte(0).Str(StableIdentity.Resolve())
					.Finish(), reliable: true);
				_transport.Send(CharacterSaveSync.BuildDownloadFor(ResolvedPeerIdentity), reliable: true);
				AutoJoin.ClearHostRichPresence();
				MorningRitualSync.CatchUpExistingNpcs();
			}
			else
			{
				_transport.Send(PacketWriter.Begin(PacketType.SnapshotRequest).Finish(), reliable: true);
			}
			break;
		}
		case PacketType.PlayerState:
			EnsurePuppet();
			PlayerSync.ApplyRemoteState(packetReader, _puppet);
			break;
		case PacketType.SnapshotRequest:
			Plugin.Log.LogInfo("[Session] cliente pediu snapshot — enviando (pausado por tick, ver _pendingSnapshotChunks)");
			_pendingSnapshotChunks = new Queue<byte[]>(WorldSync.BuildSnapshotChunks());
			break;
		case PacketType.SnapshotChunk:
		{
			if (WorldSync.ReceiveChunk(packetReader, out var fullBlob))
			{
				WorldSync.DeserializeWorld(fullBlob, ResolvedPeerIdentity);
				_transport.Send(PacketWriter.Begin(PacketType.Ready).Finish(), reliable: true);
			}
			break;
		}
		case PacketType.Ready:
			Plugin.Log.LogInfo("[Session] cliente pronto (snapshot aplicado)");
			break;
		case PacketType.EnemyDelta:
			EnemySync.ApplyDelta(packetReader);
			break;
		case PacketType.EnemyHit:
			EnsurePuppet();
			EnemySync.ApplyHitRequest(packetReader, ((Object)(object)_puppet != (Object)null) ? ((Component)_puppet).transform : null);
			break;
		case PacketType.EnemyBehaviorRequest:
			EnemySync.ApplyBehaviorRequest(packetReader);
			break;
		case PacketType.OutsideLocationEntered:
			OutsideLocationSync.ApplyEntered(packetReader);
			break;
		case PacketType.OutsideLocationReturned:
			OutsideLocationSync.ApplyReturned(packetReader);
			break;
		case PacketType.DreamStarted:
			DreamSync.ApplyStarted(packetReader);
			break;
		case PacketType.DreamEnded:
			DreamSync.ApplyEnded(packetReader);
			break;
		case PacketType.DoorLockEvent:
			DoorLockEventSync.ApplyLockEvent(packetReader);
			break;
		case PacketType.PullPeerToPosition:
			DoorLockEventSync.ApplyPullToPosition(packetReader);
			break;
		case PacketType.GameObjectAction:
			GameObjectActionSync.ApplyAction(packetReader);
			break;
		case PacketType.EnemyDeath:
			EnemySync.ApplyDeaths(packetReader);
			break;
		case PacketType.EnemyRemoved:
			EnemySync.ApplyRemovals(packetReader);
			break;
		case PacketType.PlayerActionSound:
			PlayerActionSoundSync.ApplyIncoming(packetReader);
			break;
		case PacketType.PlayerActionSoundStop:
			PlayerActionSoundSync.ApplyIncomingStop(packetReader);
			break;
		case PacketType.EnemySpawn:
			EnemySync.ApplySpawns(packetReader);
			break;
		case PacketType.EnemyHitAnim:
			EnemyHitAnimSync.ApplyIncoming(packetReader);
			break;
		case PacketType.LobbyStart:
			LobbySync.ApplyStart();
			break;
		case PacketType.PrologueIntroReady:
			LobbySync.ApplyPeerIntroDone();
			break;
		case PacketType.NpcDespawnedByLocation:
			MorningRitualSync.ApplyDespawn(packetReader);
			break;
		case PacketType.CutsceneForceEnded:
			CutsceneEscapeSync.ApplyForceEnded();
			break;
		case PacketType.LiquidTrailCreate:
			LiquidSync.ApplyTrailCreate(packetReader);
			break;
		case PacketType.LiquidIgnited:
			LiquidSync.ApplyIgnited(packetReader);
			break;
		case PacketType.ExplodableActivated:
			ExplodableSync.ApplyActivated(packetReader);
			break;
		case PacketType.CorpseFound:
			CorpseSync.ApplyFoundCorpses(packetReader);
			break;
		case PacketType.FlareThrown:
			FlareSync.ApplyThrown(packetReader);
			break;
		case PacketType.ChunkClaim:
			LocationOwnership.ApplyClaim(packetReader);
			break;
		case PacketType.DoorDelta:
			PropSync.ApplyDoors(packetReader);
			break;
		case PacketType.FurnitureDelta:
			FurnitureSync.ApplyDrags(packetReader, _role == NetRole.Host);
			break;
		case PacketType.DoorSwing:
			DoorSwingSync.ApplySwings(packetReader, _role == NetRole.Host);
			break;
		case PacketType.LampToggle:
			LampSync.ApplyToggles(packetReader);
			break;
		case PacketType.TrapTrigger:
			TrapSync.ApplyTriggers(packetReader);
			break;
		case PacketType.WindowState:
			WindowSync.ApplyChanges(packetReader);
			break;
		case PacketType.DoorBarricadeState:
			DoorBarricadeSync.ApplyChanges(packetReader);
			break;
		case PacketType.GeneratorState:
			GeneratorSync.ApplyChanges(packetReader);
			break;
		case PacketType.SawFuel:
			SawSync.ApplyChanges(packetReader);
			break;
		case PacketType.ClockResume:
			ClockSync.ApplyResume(packetReader);
			break;
		case PacketType.ClockHeartbeat:
			ClockSync.ApplyHeartbeat(packetReader);
			break;
		case PacketType.DayAdvance:
			ClockSync.ApplyDayAdvance(packetReader);
			break;
		case PacketType.RainState:
			RainSync.ApplyHeartbeat(packetReader);
			break;
		case PacketType.DropCreate:
			DropSync.ApplyCreate(packetReader);
			break;
		case PacketType.DropDestroy:
			DropSync.ApplyDestroy(packetReader);
			break;
		case PacketType.TrapDestroy:
			TrapSync.ApplyDestroy(packetReader);
			break;
		case PacketType.ContainerState:
		{
			byte[] array = ContainerSync.ApplyIncoming(packetReader, _role == NetRole.Host);
			if (array != null)
			{
				_transport.Send(array, reliable: true);
			}
			break;
		}
		case PacketType.DownedState:
			DownedSync.ApplyDownedState(packetReader);
			break;
		case PacketType.Revive:
			DownedSync.ApplyRevive();
			break;
		case PacketType.ForceDeath:
			DownedSync.ApplyForceDeath();
			break;
		case PacketType.PlayerDamage:
			PlayerDamageSync.ApplyDamage(packetReader);
			break;
		case PacketType.NightEventFire:
			NightEventSync.ApplyFire(packetReader);
			break;
		case PacketType.NightScenarioSet:
			NightScenarioSync.ApplyScenario(packetReader);
			break;
		case PacketType.FlagBoolChanged:
			FlagSync.ApplyBoolChanged(packetReader);
			break;
		case PacketType.FlagIntChanged:
			FlagSync.ApplyIntChanged(packetReader);
			break;
		case PacketType.BansheeScreamHit:
			BansheeScreamSync.ApplyRemote();
			break;
		case PacketType.PropSpawnPosition:
			PropSpawnSync.ApplyRemote(packetReader);
			break;
		case PacketType.ItemEmpDisabled:
			BansheeScreamSync.ApplyItemDisabled(packetReader);
			break;
		case PacketType.PlayerShadowHit:
			PlayerDamageSync.ApplyShadowHit();
			break;
		case PacketType.ChapterTransitionRequest:
			ChapterTransitionSync.ApplyRequest(packetReader);
			break;
		case PacketType.ChapterSaveChunk:
			ChapterTransitionSync.ApplyChapterSave(packetReader);
			break;
		case PacketType.VoiceData:
			EnsurePuppet();
			VoiceSync.ApplyVoiceData(packetReader, _puppet);
			break;
		case PacketType.TrapSpawnFired:
			TrapSpawnSync.ApplyFired(packetReader);
			break;
		case PacketType.ConstructibleBuilt:
			ConstructibleSync.ApplyChanges(packetReader);
			break;
		case PacketType.WorkbenchUpgraded:
			WorkbenchSync.ApplyChanges(packetReader);
			break;
		case PacketType.ItemIgnited:
			ItemBurnSync.ApplyIgnited(packetReader);
			break;
		case PacketType.ItemDestroyed:
			ItemBurnSync.ApplyDestroyed(packetReader);
			break;
		case PacketType.MonsterProjectileLanded:
			MonsterRangedAttackSync.ApplyLanded(packetReader);
			break;
		case PacketType.PlayerBurningState:
			EnsurePuppet();
			PlayerBurnVisualSync.ApplyState(packetReader, _puppet);
			break;
		case PacketType.TrapPlaced:
			TrapPlaceSync.ApplyPlaced(packetReader);
			break;
		case PacketType.FirearmFired:
			FirearmFxSync.ApplyFired(packetReader);
			break;
		case PacketType.BulletImpact:
			FirearmFxSync.ApplyImpact(packetReader);
			break;
		case PacketType.ThrownItemLanded:
			ThrownItemSync.ApplyLanded(packetReader);
			break;
		case PacketType.DialogueStateChanged:
			DialogueStateSync.ApplyChanges(packetReader);
			break;
		case PacketType.WorldEventFired:
			WorldEventSync.ApplyFired(packetReader);
			break;
		case PacketType.CharacterMessageShown:
			CharacterMessageSync.ApplyShown(packetReader);
			break;
		case PacketType.InSightTriggered:
			InSightSync.ApplyRemote(packetReader);
			break;
		case PacketType.MapMarkerShown:
			MapMarkerSync.ApplyShown(packetReader);
			break;
		case PacketType.LocationDiscovered:
			LocationDiscoverySync.ApplyDiscovered(packetReader);
			break;
		case PacketType.NpcStateChanged:
			NpcStateSync.ApplyChanges(packetReader);
			break;
		case PacketType.KeyItemPickup:
			KeyItemSync.ApplyPickup(packetReader);
			break;
		case PacketType.PauseState:
			PauseSync.ApplyPeerState(packetReader);
			break;
		case PacketType.LockState:
			LockSync.ApplyChanges(packetReader);
			break;
		case PacketType.ShadowDelta:
			ShadowSync.ApplyDelta(packetReader);
			break;
		case PacketType.ShadowSpawn:
			ShadowSync.ApplySpawns(packetReader);
			break;
		case PacketType.ShadowDeath:
			ShadowSync.ApplyDeaths(packetReader);
			break;
		case PacketType.CharacterSaveUpload:
			CharacterSaveSync.ApplyUpload(packetReader, ResolvedPeerIdentity);
			break;
		case PacketType.CharacterSaveDownload:
			CharacterSaveSync.ApplyDownload(packetReader);
			break;
		default:
			Plugin.Log.LogWarning($"[Session] pacote não tratado: {packetReader.Type}");
			break;
		}
	}

	public void Stop()
	{
		if (_role == NetRole.Client && _linked && _transport != null && _transport.Connected)
		{
			byte[] array = CharacterSaveSync.BuildUpload();
			if (array != null)
			{
				_transport.Send(array, reliable: true);
			}
		}
		_transport?.Stop();
		if ((Object)(object)_puppet != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_puppet).gameObject);
		}
		_puppet = null;
		_linked = false;
		_resolvedPeerIdentity = null;
		CoopRuntime.SetSession(null);
		EnemySync.Reset();
		EnemyHitAnimSync.Reset();
		LiquidSync.Reset();
		LobbySync.Reset();
		CorpseSync.Reset();
		FlareSync.Reset();
		NightScenarioSync.Reset();
		NightEventSync.Reset();
		PropSpawnSync.Reset();
		DebugCheatKeys.Reset();
		LocationDiscoverySync.Reset();
		FlagSync.Reset();
		ChapterTransitionSync.Reset();
		VoiceSync.Reset();
		TrapSpawnSync.Reset();
		ConstructibleSync.Reset();
		WorkbenchSync.Reset();
		MonsterRangedAttackSync.Reset();
		PlayerBurnVisualSync.Reset();
		SharedVisionSync.Reset();
		PeerNameTagSync.Reset();
		TrapPlaceSync.Reset();
		ThrownItemSync.Reset();
		DialogueStateSync.Reset();
		CharacterMessageSync.Reset();
		InSightSync.Reset();
		NpcStateSync.Reset();
		LocationOwnership.Reset();
		AnimationSyncDiagnostics.Reset();
		DayTransitionDiagnostics.Reset();
		DownedSync.Reset();
		PropSync.Reset();
		FurnitureSync.Reset();
		DoorSwingSync.Reset();
		LampSync.Reset();
		TrapSync.Reset();
		DropSync.Reset();
		ContainerSync.Reset();
		WindowSync.Reset();
		DoorBarricadeSync.Reset();
		GeneratorSync.Reset();
		SawSync.Reset();
		ClockSync.Reset();
		SceneCache.Reset();
		DropDiagnostics.Reset();
		LegsDiagnostics.Reset();
		VisionLightDiagnostics.Reset();
		TrapDiagnostics.Reset();
		ChestDiagnostics.Reset();
		NightEventDiagnostics.Reset();
		EnemyAiDiagnostics.Reset();
		KeyItemSync.Reset();
		CutsceneDiagnostics.Reset();
		WorldGenDiagnostics.Reset();
		DogAiDiagnostics.Reset();
		PauseSync.Reset();
		LockSync.Reset();
		ShadowSync.Reset();
		PlayerSync.Reset();
		CharacterSaveSync.Reset();
		PeerTeleport.Reset();
		DoorLockEventSync.Reset();
		PrologueEncounterPullSync.Reset();
		TrainWreckEncounterPullSync.Reset();
		ForbidInputsWatchdog.Reset();
		DreamSync.Reset();
		PrologueCutsceneHoldSync.Reset();
		CutscenePlayerVisibilitySync.Reset();
		GameObjectActionSync.Reset();
	}
}
