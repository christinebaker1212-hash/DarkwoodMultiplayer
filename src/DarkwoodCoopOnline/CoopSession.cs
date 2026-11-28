using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

// One co-op game in progress, from this PC's point of view (up to Proto.MaxPlayers players).
// The host's world is the reference; every player reports what changes around them, and those
// reports go to everyone else (RELAY to all), exactly as the two-player version did with "the peer".
// Packets that are requests to the host go to slot 0 only; answers go back to the asking slot.
public sealed class CoopSession
{
	private readonly CoopConnection _net;

	private readonly NetRole _role;

	private readonly Dictionary<int, RemotePlayerPuppet> _puppets = new Dictionary<int, RemotePlayerPuppet>();

	private readonly List<RemotePlayerPuppet> _puppetList = new List<RemotePlayerPuppet>();

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

	// enemy target checks need line-of-sight raycasts per enemy and player: 5 times a second is plenty
	private float _aggroTimer;

	private const float AggroInterval = 0.2f;

	private const float ShadowInterval = 0.1f;

	// snapshot chunks paced per receiving slot (-1 = everyone)
	private readonly Dictionary<int, Queue<byte[]>> _paced = new Dictionary<int, Queue<byte[]>>();

	private const int SnapshotChunksPerTick = 4;

	// save requests that arrived before the host's world finished loading
	private readonly List<(int slot, float since)> _pendingSaveRequests = new List<(int, float)>();

	// newest-per-sender buffers for the high-frequency packets (only the latest one matters)
	private readonly Dictionary<int, byte[]> _latestState = new Dictionary<int, byte[]>();

	private readonly Dictionary<int, byte[]> _latestBySenderType = new Dictionary<int, byte[]>();

	private readonly List<int> _scratchKeys = new List<int>();

	public bool PeerLeftSignaled { get; private set; }

	public string EndReason { get; private set; }

	public NetRole Role => _role;

	public bool Linked => _linked;

	public CoopConnection Net => _net;

	public int MySlot => _net.MySlot;

	public IReadOnlyList<RemotePlayerPuppet> PuppetList => _puppetList;

	// The remote player closest to us (two-player code that asked for "the peer" now gets this one)
	public RemotePlayerPuppet Puppet
	{
		get
		{
			if (_puppetList.Count == 0)
			{
				return null;
			}
			if (_puppetList.Count == 1 || (Object)(object)Player.Instance == (Object)null)
			{
				return _puppetList[0];
			}
			return CoopPeers.Nearest(((Component)Player.Instance).transform.position) ?? _puppetList[0];
		}
	}

	public string PeerIdentity => ResolvedPeerIdentity;

	// Stable identity of the player whose packet is being handled (or of the first other player)
	public string ResolvedPeerIdentity
	{
		get
		{
			int sender = CoopRuntime.Sender;
			if (sender >= 0 && sender != MySlot)
			{
				return _net.StableIdOf(sender);
			}
			for (int i = 0; i < Proto.MaxPlayers; i++)
			{
				if (i != MySlot && _net.Roster[i] != null)
				{
					return _net.Roster[i].StableId;
				}
			}
			return null;
		}
	}

	public CoopSession(CoopConnection net, NetRole role)
	{
		_net = net;
		_role = role;
	}

	public RemotePlayerPuppet PuppetOf(int slot)
	{
		if (!_puppets.TryGetValue(slot, out var value))
		{
			return null;
		}
		return value;
	}

	public string PeerName(int slot)
	{
		return _net.NameOf(slot);
	}

	public int PlayersInGame
	{
		get
		{
			int num = 1;
			for (int i = 0; i < Proto.MaxPlayers; i++)
			{
				if (i != MySlot && _net.Roster[i] != null && _net.Roster[i].InGame)
				{
					num++;
				}
			}
			return num;
		}
	}

	// ---- sending ----

	public void SendReliable(byte[] data)
	{
		_net.SendGame(-1, data, reliable: true);
	}

	public void Broadcast(byte[] data, bool reliable)
	{
		_net.SendGame(-1, data, reliable);
	}

	public void SendTo(int slot, byte[] data, bool reliable = true)
	{
		_net.SendGame(slot, data, reliable);
	}

	public void SendToHost(byte[] data)
	{
		_net.SendGame(0, data, reliable: true);
	}

	// Reply to whoever sent the packet being handled
	public void Reply(byte[] data)
	{
		if (CoopRuntime.Sender >= 0)
		{
			_net.SendGame(CoopRuntime.Sender, data, reliable: true);
		}
	}

	public void EnqueuePacedChunks(IEnumerable<byte[]> packets, int target = -1)
	{
		if (!_paced.TryGetValue(target, out var value))
		{
			value = (_paced[target] = new Queue<byte[]>());
		}
		foreach (byte[] packet in packets)
		{
			value.Enqueue(packet);
		}
	}

	public bool Start()
	{
		CoopRuntime.SetSession(this);
		if (_role == NetRole.Client)
		{
			_net.Hello();
			_linked = true;
			LobbySync.MarkPeerConnected();
			SendToHost(PacketWriter.Begin(PacketType.SnapshotRequest).Finish());
		}
		// players already in the world (a client joining a game in progress)
		for (int i = 0; i < Proto.MaxPlayers; i++)
		{
			if (i != MySlot && _net.Roster[i] != null && _net.Roster[i].InGame)
			{
				EnsurePuppet(i);
			}
		}
		Plugin.Log.LogInfo($"[Session] started as {_role} (slot {MySlot}, room \"{_net.RoomName}\", {_net.TransportName}).");
		return true;
	}

	// ---- per frame ----

	public void Update()
	{
		if (_net == null)
		{
			return;
		}
		_net.Tick(Time.unscaledDeltaTime);
		PerfDiagnostics.Begin("Receive");
		ReceiveAll();
		PerfDiagnostics.End();
		FlushPaced();
		TickPendingSaveRequests();
		if (!_linked)
		{
			return;
		}
		Vector3 local = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.position : Vector3.zero);
		_sendTimer += Time.deltaTime;
		if (_sendTimer >= SendInterval)
		{
			_sendTimer = 0f;
			PerfDiagnostics.Begin("PlayerState.Build");
			byte[] array = PlayerSync.BuildLocalState();
			if (array != null)
			{
				_net.SendState(array);
			}
			PerfDiagnostics.End();
		}
		byte[] array2 = VoiceSync.PollOutgoing();
		if (array2 != null)
		{
			Broadcast(array2, reliable: false);
		}
		VoiceSync.Tick(_puppetList);
		_enemyTimer += Time.deltaTime;
		if (_enemyTimer >= EnemyInterval && (Object)(object)Player.Instance != (Object)null)
		{
			_enemyTimer = 0f;
			PerfDiagnostics.Begin("Enemy.Build");
			List<Vector3> list = CoopPeers.Positions();
			if (list.Count > 0)
			{
				LocationOwnership.WakeNearbyInactiveTick(list);
				EnemySync.WakeOwnedNearPeers(list);
				EnemySync.WakePuppetsNearPeers(list);
				NightEventSync.SetPeerPosition(Puppet?.transform.position ?? local);
				Transform transform = ((Component)Player.Instance).transform;
				_aggroTimer += EnemyInterval;
				bool flag = _aggroTimer >= AggroInterval;
				if (flag)
				{
					_aggroTimer = 0f;
				}
				foreach (RemotePlayerPuppet puppet in _puppetList)
				{
					if ((Object)(object)puppet == (Object)null || !puppet.HasState)
					{
						continue;
					}
					if (DownedSync.IsPeerReallyDead(puppet.Slot) || DownedSync.IsPeerDowned(puppet.Slot))
					{
						EnemySync.ReleaseAggroFromPuppet(((Component)puppet).transform);
					}
					else if (flag)
					{
						EnemySync.CheckAggroRedirect(transform, ((Component)puppet).transform);
					}
				}
				EnemySync.CheckAoeDamageToPeers(_puppetList);
				EnemySync.SyncCombatTensionForPeers(_puppetList);
			}
			Broadcast(EnemySync.BuildDelta(local, local), reliable: false);
			SendIfAny(EnemySync.BuildDeaths(), reliable: true);
			SendIfAny(EnemySync.BuildRemovals(), reliable: true);
			SendIfAny(EnemySync.BuildSpawns(), reliable: true);
			SendIfAny(EnemyHitAnimSync.PollOutgoing(), reliable: true);
			PerfDiagnostics.End();
		}
		EnemySync.PruneLocalOrphans();
		NightEventSync.Tick();
		SendIfAny(CorpseSync.PollFoundCorpses(), reliable: true);
		SendIfAny(FlareSync.PollSettled(), reliable: true);
		SendIfAny(ThrownItemSync.PollSettled(), reliable: true);
		SendIfAny(CharacterMessageSync.PollNew(), reliable: true);
		SendIfAny(InSightSync.PollNew(), reliable: true);
		_shadowTimer += Time.deltaTime;
		if (_shadowTimer >= ShadowInterval)
		{
			_shadowTimer = 0f;
			PerfDiagnostics.Begin("Shadow.Build");
			SendIfAny(ShadowSync.BuildDelta(), reliable: false);
			SendIfAny(ShadowSync.BuildDeaths(), reliable: true);
			SendIfAny(ShadowSync.BuildSpawns(), reliable: true);
			PerfDiagnostics.End();
		}
		LobbySync.Tick();
		PerfDiagnostics.Begin("Downed.Tick");
		DownedSync.Tick(Time.deltaTime);
		foreach (RemotePlayerPuppet puppet2 in _puppetList)
		{
			puppet2?.SetDownedVisual(DownedSync.IsPeerDowned(puppet2.Slot), DownedSync.Downed);
		}
		PerfDiagnostics.End();
		SendIfAny(EnemySync.BuildHitRequests(), reliable: true);
		SendIfAny(EnemySync.BuildBehaviorRequests(), reliable: true);
		PerfDiagnostics.Begin("Clock.Poll");
		SendIfAny(ClockSync.PollLocalResume(), reliable: true);
		if (_role == NetRole.Host)
		{
			SendIfAny(ClockSync.PollDayAdvance(), reliable: true);
		}
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Pause.Poll");
		SendIfAny(PauseSync.PollLocalChange(), reliable: true);
		PerfDiagnostics.End();
		if (_role == NetRole.Host)
		{
			_clockTimer += Time.deltaTime;
			if (_clockTimer >= ClockInterval)
			{
				_clockTimer = 0f;
				SendIfAny(ClockSync.BuildHeartbeat(), reliable: true);
				SendIfAny(RainSync.BuildHeartbeat(), reliable: true);
			}
		}
		_doorTimer2 += Time.deltaTime;
		if (_doorTimer2 >= FurnInterval && (Object)(object)Player.Instance != (Object)null)
		{
			_doorTimer2 = 0f;
			PerfDiagnostics.Begin("Furniture.Poll");
			SendIfAny(FurnitureSync.PollDrags(local, local, _role == NetRole.Host), reliable: false);
			PerfDiagnostics.End();
			PerfDiagnostics.Begin("DoorSwing.Poll");
			SendIfAny(DoorSwingSync.PollSwings(local, local), reliable: false);
			PerfDiagnostics.End();
		}
		PerfDiagnostics.Begin("Furniture.Tick");
		FurnitureSync.Tick();
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("DoorSwing.Tick");
		DoorSwingSync.Tick();
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Drop.PollDestroys");
		SendIfAny(DropSync.PollDestroys(), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Trap.PollDestroys");
		SendIfAny(TrapSync.PollDestroys(), reliable: true);
		PerfDiagnostics.End();
		_doorTimer += Time.deltaTime;
		if (_doorTimer >= DoorInterval && (Object)(object)Player.Instance != (Object)null)
		{
			_doorTimer = 0f;
			PollWorld(local);
		}
		SendIfAny(CharacterSaveSync.PollUpload(Time.deltaTime, _role == NetRole.Client), reliable: true, toHostOnly: _role == NetRole.Client);
	}

	private void PollWorld(Vector3 p)
	{
		PerfDiagnostics.Begin("Door.Poll");
		SendIfAny(PropSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Lamp.Poll");
		SendIfAny(LampSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Trap.Poll");
		SendIfAny(TrapSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Window.Poll");
		SendIfAny(WindowSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("DoorBarricade.Poll");
		SendIfAny(DoorBarricadeSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Generator.Poll");
		SendIfAny(GeneratorSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Saw.Poll");
		SendIfAny(SawSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Drop.PollCreates");
		SendIfAny(DropSync.PollCreates(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Container.Poll");
		List<byte[]> list = ContainerSync.PollChanges(p, p, _role == NetRole.Host);
		if (list != null)
		{
			foreach (byte[] item in list)
			{
				// the host's containers are the truth: players ask the host, the host tells everyone
				if (_role == NetRole.Host)
				{
					SendReliable(item);
				}
				else
				{
					SendToHost(item);
				}
			}
		}
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("KeyItem.Poll");
		SendIfAny(KeyItemSync.PollLocalPickups(), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Lock.Poll");
		SendIfAny(LockSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Constructible.Poll");
		SendIfAny(ConstructibleSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("Workbench.Poll");
		SendIfAny(WorkbenchSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("PlayerBurn.Poll");
		SendIfAny(PlayerBurnVisualSync.PollChanges(), reliable: true);
		PerfDiagnostics.End();
		SendIfAny(SkinSync.Poll(), reliable: true);
		PerfDiagnostics.Begin("DialogueState.Poll");
		SendIfAny(DialogueStateSync.PollChanges(p, p), reliable: true);
		PerfDiagnostics.End();
		PerfDiagnostics.Begin("NpcState.Poll");
		SendIfAny(NpcStateSync.PollChanges(), reliable: true);
		PerfDiagnostics.End();
	}

	private void SendIfAny(byte[] data, bool reliable, bool toHostOnly = false)
	{
		if (data != null && data.Length != 0)
		{
			_net.SendGame(toHostOnly ? 0 : (-1), data, reliable);
		}
	}

	private void FlushPaced()
	{
		if (_paced.Count == 0)
		{
			return;
		}
		_scratchKeys.Clear();
		_scratchKeys.AddRange(_paced.Keys);
		foreach (int scratchKey in _scratchKeys)
		{
			Queue<byte[]> queue = _paced[scratchKey];
			for (int i = 0; i < SnapshotChunksPerTick; i++)
			{
				if (queue.Count <= 0)
				{
					break;
				}
				_net.SendGame(scratchKey, queue.Dequeue(), reliable: true);
			}
			if (queue.Count == 0)
			{
				_paced.Remove(scratchKey);
			}
		}
	}

	// ---- receiving ----

	private static bool IsLatestOnly(PacketType t)
	{
		if (t != PacketType.EnemyDelta && t != PacketType.FurnitureDelta)
		{
			return t == PacketType.DoorSwing;
		}
		return true;
	}

	private void ReceiveAll()
	{
		CoopConnection.Ev ev;
		while (_net.Poll(out ev))
		{
			switch (ev.Kind)
			{
			case CoopConnection.EvKind.State:
				_latestState[ev.Slot] = ev.Payload;
				break;
			case CoopConnection.EvKind.Game:
				if (ev.Payload.Length != 0)
				{
					if (IsLatestOnly((PacketType)ev.Payload[0]))
					{
						_latestBySenderType[(ev.Slot << 8) | ev.Payload[0]] = ev.Payload;
					}
					else
					{
						Dispatch(ev.Slot, ev.Payload);
					}
				}
				break;
			case CoopConnection.EvKind.PeerJoined:
				Plugin.Log.LogInfo($"[Session] '{_net.NameOf(ev.Slot)}' (slot {ev.Slot}) connected - loading the world.");
				CoopNotice.Show(_net.NameOf(ev.Slot) + " is joining...");
				break;
			case CoopConnection.EvKind.PeerInGame:
				OnPeerInGame(ev.Slot);
				break;
			case CoopConnection.EvKind.PeerLeft:
				OnPeerLeft(ev.Slot, _net.NameOf(ev.Slot));
				break;
			case CoopConnection.EvKind.SaveRequest:
				_pendingSaveRequests.Add((ev.Slot, Time.unscaledTime));
				break;
			case CoopConnection.EvKind.Disconnected:
				EndReason = _net.DisconnectText ?? "The host closed the game.";
				Plugin.Log.LogWarning("[Session] " + EndReason);
				PeerLeftSignaled = true;
				break;
			}
		}
		if (_latestState.Count > 0)
		{
			foreach (KeyValuePair<int, byte[]> item in _latestState)
			{
				Dispatch(item.Key, item.Value);
			}
			_latestState.Clear();
		}
		if (_latestBySenderType.Count > 0)
		{
			foreach (KeyValuePair<int, byte[]> item2 in _latestBySenderType)
			{
				Dispatch(item2.Key >> 8, item2.Value);
			}
			_latestBySenderType.Clear();
		}
	}

	private void Dispatch(int sender, byte[] data)
	{
		if (sender == MySlot)
		{
			return;
		}
		CoopRuntime.Sender = sender;
		try
		{
			Handle(data, sender);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning($"[Session] error handling {(PacketType)data[0]} from slot {sender}: {ex.GetType().Name}: {ex.Message}");
		}
		finally
		{
			CoopRuntime.Sender = -1;
		}
	}

	private RemotePlayerPuppet EnsurePuppet(int slot)
	{
		if (slot == MySlot || slot < 0)
		{
			return null;
		}
		if (_puppets.TryGetValue(slot, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		// only players who are actually here (no ghost from a packet that arrived with their leave)
		if ((Object)(object)Player.Instance == (Object)null || _net.Roster[slot] == null)
		{
			return null;
		}
		value = RemotePlayerPuppet.Create();
		if ((Object)(object)value == (Object)null)
		{
			return null;
		}
		value.Slot = slot;
		value.PeerName = _net.NameOf(slot) ?? ("Player " + (slot + 1));
		_puppets[slot] = value;
		RebuildPuppetList();
		return value;
	}

	private void RebuildPuppetList()
	{
		_puppetList.Clear();
		foreach (KeyValuePair<int, RemotePlayerPuppet> puppet in _puppets)
		{
			if ((Object)(object)puppet.Value != (Object)null)
			{
				_puppetList.Add(puppet.Value);
			}
		}
	}

	private void OnPeerInGame(int slot)
	{
		string text = _net.NameOf(slot) ?? ("Player " + (slot + 1));
		Plugin.Log.LogInfo($"[Session] '{text}' (slot {slot}) is in the world ({PlayersInGame}/{Proto.MaxPlayers}).");
		CoopNotice.Show(text + " joined the game");
		EnsurePuppet(slot);
		_linked = true;
		LobbySync.MarkPeerConnected();
		if (_role == NetRole.Host)
		{
			SendTo(slot, CharacterSaveSync.BuildDownloadFor(_net.StableIdOf(slot)));
			MorningRitualSync.CatchUpExistingNpcs();
			if (LobbySync.Started)
			{
				// joining a game that's already going: no lobby for them, and bring their world up to date
				SendTo(slot, PacketWriter.Begin(PacketType.LobbyStart).Finish());
				LobbySync.ReconcileAll();
			}
			HostFlow.OnPlayerCountChanged();
		}
	}

	private void OnPeerLeft(int slot, string name)
	{
		if (_net.Roster[slot] == null && !_puppets.ContainsKey(slot))
		{
			return;
		}
		name = name ?? ("Player " + (slot + 1));
		_net.RemovePeer(slot);
		_latestState.Remove(slot);
		_scratchKeys.Clear();
		foreach (int key in _latestBySenderType.Keys)
		{
			if (key >> 8 == slot)
			{
				_scratchKeys.Add(key);
			}
		}
		foreach (int scratchKey in _scratchKeys)
		{
			_latestBySenderType.Remove(scratchKey);
		}
		_paced.Remove(slot);
		EnemySync.OnPeerLeft(slot);
		if (_puppets.TryGetValue(slot, out var value))
		{
			if ((Object)(object)value != (Object)null)
			{
				EnemySync.ReleaseAggroFromPuppet(((Component)value).transform);
				Object.Destroy((Object)(object)((Component)value).gameObject);
			}
			_puppets.Remove(slot);
			RebuildPuppetList();
		}
		DownedSync.OnPeerLeft(slot);
		PauseSync.OnPeerLeft(slot);
		LobbySync.OnPeerLeft(slot);
		VoiceSync.OnPeerLeft(slot);
		SkinSync.Forget(slot);
		LocationOwnership.OnPeerLeft(slot);
		Plugin.Log.LogInfo($"[Session] '{name}' (slot {slot}) left.");
		if (_role == NetRole.Client && slot == 0)
		{
			EndReason = "The host left the game.";
			PeerLeftSignaled = true;
			return;
		}
		CoopNotice.Show(name + " left the game");
		if (_role == NetRole.Host)
		{
			HostFlow.OnPlayerCountChanged();
			if (_puppets.Count == 0 && PlayersInGame <= 1)
			{
				// nobody left: back to playing alone until someone joins again
				_linked = false;
				Plugin.Log.LogInfo("[Session] everyone left - playing alone, still hosting.");
			}
		}
	}

	// The host's world, for a player at the main menu (in the same form LAN mode always used:
	// u32 identity length, identity, world blob)
	private void TickPendingSaveRequests()
	{
		if (_pendingSaveRequests.Count == 0 || _role != NetRole.Host)
		{
			return;
		}
		for (int num = _pendingSaveRequests.Count - 1; num >= 0; num--)
		{
			(int slot, float since) tuple = _pendingSaveRequests[num];
			if (!LanSaveTransfer.HostWorldReady && Time.unscaledTime - tuple.since < 60f)
			{
				continue;
			}
			_pendingSaveRequests.RemoveAt(num);
			try
			{
				byte[] array = WorldSync.SerializeWorld();
				byte[] bytes = Encoding.UTF8.GetBytes(StableIdentity.Resolve());
				using MemoryStream memoryStream = new MemoryStream();
				using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
				binaryWriter.Write(bytes.Length);
				binaryWriter.Write(bytes);
				binaryWriter.Write(array);
				binaryWriter.Flush();
				_net.Server.SendSave(tuple.slot, memoryStream.ToArray());
				Plugin.Log.LogInfo($"[Session] sent the world save ({memoryStream.Length} bytes) to '{_net.NameOf(tuple.slot)}'.");
			}
			catch (Exception ex)
			{
				Plugin.Log.LogError("[Session] could not build the world save: " + ex);
			}
		}
	}

	private void Handle(byte[] data, int from)
	{
		PacketReader packetReader = new PacketReader(data);
		switch (packetReader.Type)
		{
		case PacketType.PeerLeaving:
			OnPeerLeft(from, _net.NameOf(from));
			break;
		case PacketType.Hello:
			break;
		case PacketType.PlayerState:
		{
			RemotePlayerPuppet remotePlayerPuppet = EnsurePuppet(from);
			if ((Object)(object)remotePlayerPuppet != (Object)null)
			{
				PlayerSync.ApplyRemoteState(packetReader, remotePlayerPuppet);
			}
			break;
		}
		case PacketType.SnapshotRequest:
			if (_role == NetRole.Host)
			{
				Plugin.Log.LogInfo($"[Session] '{_net.NameOf(from)}' asked for the world snapshot - sending (paced).");
				_paced.Remove(from);
				EnqueuePacedChunks(WorldSync.BuildSnapshotChunks(), from);
			}
			break;
		case PacketType.SnapshotChunk:
		{
			if (from == 0 && WorldSync.ReceiveChunk(packetReader, out var fullBlob))
			{
				WorldSync.DeserializeWorld(fullBlob, _net.StableIdOf(0));
				SendToHost(PacketWriter.Begin(PacketType.Ready).Finish());
			}
			break;
		}
		case PacketType.Ready:
			Plugin.Log.LogInfo($"[Session] '{_net.NameOf(from)}' is ready (snapshot applied).");
			break;
		case PacketType.EnemyDelta:
			EnemySync.ApplyDelta(packetReader);
			break;
		case PacketType.EnemyHit:
		{
			RemotePlayerPuppet remotePlayerPuppet2 = EnsurePuppet(from);
			EnemySync.ApplyHitRequest(packetReader, ((Object)(object)remotePlayerPuppet2 != (Object)null) ? ((Component)remotePlayerPuppet2).transform : null);
			break;
		}
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
			if (from == 0)
			{
				LobbySync.ApplyStart();
			}
			break;
		case PacketType.PrologueIntroReady:
			LobbySync.ApplyPeerIntroDone(from);
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
			LocationOwnership.ApplyClaim(packetReader, from);
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
			if (from == 0)
			{
				ClockSync.ApplyHeartbeat(packetReader);
			}
			break;
		case PacketType.DayAdvance:
			if (from == 0)
			{
				ClockSync.ApplyDayAdvance(packetReader);
			}
			break;
		case PacketType.RainState:
			if (from == 0)
			{
				RainSync.ApplyHeartbeat(packetReader);
			}
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
				SendReliable(array);
			}
			break;
		}
		case PacketType.DownedState:
			DownedSync.ApplyDownedState(packetReader, from);
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
			VoiceSync.ApplyVoiceData(packetReader, EnsurePuppet(from));
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
		case PacketType.PlayerSkin:
			SkinSync.Apply(packetReader, from, _net.NameOf(from) ?? ("Player " + (from + 1)));
			break;
		case PacketType.PlayerBurningState:
			PlayerBurnVisualSync.ApplyState(packetReader, EnsurePuppet(from));
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
			PauseSync.ApplyPeerState(packetReader, from);
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
			if (_role == NetRole.Host)
			{
				CharacterSaveSync.ApplyUpload(packetReader, _net.StableIdOf(from));
			}
			break;
		case PacketType.CharacterSaveDownload:
			if (from == 0)
			{
				CharacterSaveSync.ApplyDownload(packetReader);
			}
			break;
		default:
			Plugin.Log.LogWarning($"[Session] unhandled packet: {packetReader.Type}");
			break;
		}
	}

	public void Stop()
	{
		if (_role == NetRole.Client && _linked && _net.Alive)
		{
			byte[] array = CharacterSaveSync.BuildUpload();
			if (array != null)
			{
				SendToHost(array);
			}
		}
		_net.Close();
		foreach (RemotePlayerPuppet puppet in _puppetList)
		{
			if ((Object)(object)puppet != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)puppet).gameObject);
			}
		}
		_puppets.Clear();
		_puppetList.Clear();
		_linked = false;
		CoopRuntime.SetSession(null);
		ResetAllSync();
	}

	public static void ResetAllSync()
	{
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
		SkinSync.Reset();
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
