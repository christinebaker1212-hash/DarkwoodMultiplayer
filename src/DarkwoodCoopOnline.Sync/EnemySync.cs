using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class EnemySync
{
	private struct OwnedEnemy
	{
		public Character c;

		public string id;

		public tk2dSpriteAnimator body;
	}

	private struct DeathInfo
	{
		public string id;

		public bool cutInHalf;

		public string deathAnim;

		public int dayDied;
	}

	private const float OwnedRange = 4000f;

	private const float PairRadius = 800f;

	private const float WideMatchRadius = 3000f;

	private const float InactiveTwinWakeRadius = 300f;

	private const int MaxPerPacket = 60;

	private const float DuplicateRaceWindow = 8f;

	private const float AggroDetectRange = 300f;

	private static readonly List<OwnedEnemy> _ownedCache = new List<OwnedEnemy>();

	private static Character[] _ownedRawLast;

	private static readonly Dictionary<string, Character> _ownedById = new Dictionary<string, Character>();

	private static readonly Dictionary<string, float> _ownedSince = new Dictionary<string, float>();

	private static readonly Dictionary<string, string> _ownedEventKey = new Dictionary<string, string>();

	private static readonly HashSet<string> _announcedIds = new HashSet<string>();

	private static readonly Dictionary<string, string> _lastSentBodyClip = new Dictionary<string, string>();

	private static readonly FieldInfo DeathAnimField = typeof(Character).GetField("deathAnim", BindingFlags.Instance | BindingFlags.NonPublic);

	private static readonly HashSet<string> _aliveSent = new HashSet<string>();

	private static List<DeathInfo> _pendingDeaths;

	private static Dictionary<string, OwnedEnemy> _pendingSpawns;

	private const float ResendRosterInterval = 3f;

	private static float _resendTimer;

	public static bool AggroRedirectEnabled = true;

	private const float AoeDamageInterval = 1f;

	private static float _aoeDamageTimer;

	private static readonly HashSet<Character> _countedAsAttackingPeer = new HashSet<Character>();

	private static readonly Dictionary<string, Character> _puppetById = new Dictionary<string, Character>();

	private static readonly Dictionary<string, float> _puppetSince = new Dictionary<string, float>();

	// which player's game runs each enemy we show as a puppet (slot), from who sent its spawn / movement
	private static readonly Dictionary<string, int> _puppetOwner = new Dictionary<string, int>();

	private static readonly Dictionary<string, float> _puppetLastHp = new Dictionary<string, float>();

	private static int _lastMatched = -1;

	private static List<(string id, float damage)> _pendingHits;

	private const float PuppetWakeRange = 1200f;

	private const float PuppetWakeInterval = 1f;

	private static float _puppetWakeTimer;

	private const float OwnedWakeRange = 1200f;

	private const float OwnedWakeInterval = 1f;

	private static float _ownedWakeTimer;

	private static readonly Dictionary<string, Aggressiveness> _puppetLastAggressiveness = new Dictionary<string, Aggressiveness>();

	private static readonly Dictionary<string, bool> _puppetLastSleeping = new Dictionary<string, bool>();

	private static readonly Dictionary<string, bool> _puppetLastDontMove = new Dictionary<string, bool>();

	private static List<(string id, Aggressiveness aggressiveness, bool sleeping, bool dontMove)> _pendingBehaviorRequests;

	private const float OrphanGraceSeconds = 8f;

	private static readonly Dictionary<Character, float> _unmatchedFirstSeen = new Dictionary<Character, float>();

	private static List<string> _pendingRemovals;

	private static readonly Dictionary<string, string> _lastRecvBodyClip = new Dictionary<string, string>();

	private static string MyTag
	{
		get
		{
			if (CoopRuntime.Session == null || CoopRuntime.Session.Role != 0)
			{
				return "C";
			}
			return "H";
		}
	}

	public static tk2dSpriteAnimator DebugBodyAnim(Character c)
	{
		return BodyAnim(c);
	}

	private static tk2dSpriteAnimator BodyAnim(Character c)
	{
		return c.animator;
	}

	public static bool IsTrackedOnHost(Character c)
	{
		foreach (OwnedEnemy item in _ownedCache)
		{
			if ((Object)(object)item.c == (Object)(object)c)
			{
				return true;
			}
		}
		return false;
	}

	public static bool IsClaimed(Character c)
	{
		if ((Object)(object)c == (Object)null)
		{
			return false;
		}
		foreach (KeyValuePair<string, Character> item in _ownedById)
		{
			if ((Object)(object)item.Value == (Object)(object)c)
			{
				return true;
			}
		}
		foreach (KeyValuePair<string, Character> item2 in _puppetById)
		{
			if ((Object)(object)item2.Value == (Object)(object)c)
			{
				return true;
			}
		}
		return false;
	}

	public static string ResolveOwnedId(Character c)
	{
		if ((Object)(object)c == (Object)null)
		{
			return null;
		}
		foreach (KeyValuePair<string, Character> item in _ownedById)
		{
			if ((Object)(object)item.Value == (Object)(object)c)
			{
				return item.Key;
			}
		}
		return null;
	}

	public static Character ResolvePuppetById(string id)
	{
		if (string.IsNullOrEmpty(id))
		{
			return null;
		}
		if (!_puppetById.TryGetValue(id, out var value))
		{
			return null;
		}
		return value;
	}

	public static (int alive, int visible) GetOwnedStats()
	{
		int num = 0;
		int num2 = 0;
		foreach (OwnedEnemy item in _ownedCache)
		{
			if (!((Object)(object)item.c == (Object)null) && ((CharBase)item.c).alive)
			{
				num++;
				Renderer componentInChildren = ((Component)item.c).GetComponentInChildren<Renderer>();
				if ((Object)(object)componentInChildren != (Object)null && componentInChildren.isVisible)
				{
					num2++;
				}
			}
		}
		return (alive: num, visible: num2);
	}

	public static (int paired, int visible) GetPuppetStats()
	{
		int num = 0;
		int num2 = 0;
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			Character value = item.Value;
			if (!((Object)(object)value == (Object)null))
			{
				num++;
				Renderer componentInChildren = ((Component)value).GetComponentInChildren<Renderer>();
				if ((Object)(object)componentInChildren != (Object)null && componentInChildren.isVisible)
				{
					num2++;
				}
			}
		}
		return (paired: num, visible: num2);
	}

	public static List<(Character c, string id)> GetOwnedList()
	{
		List<(Character, string)> list = new List<(Character, string)>();
		foreach (OwnedEnemy item in _ownedCache)
		{
			if ((Object)(object)item.c != (Object)null)
			{
				list.Add((item.c, item.id));
			}
		}
		return list;
	}

	public static List<(Character c, string id)> GetPuppetList()
	{
		List<(Character, string)> list = new List<(Character, string)>();
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			if ((Object)(object)item.Value != (Object)null)
			{
				list.Add((item.Value, item.Key));
			}
		}
		return list;
	}

	private static string GetDeathAnim(Character c)
	{
		if (!((Object)(object)c != (Object)null))
		{
			return "";
		}
		return (string)DeathAnimField.GetValue(c);
	}

	private static void SetDeathAnim(Character c, string value)
	{
		if ((Object)(object)c != (Object)null && !string.IsNullOrEmpty(value))
		{
			DeathAnimField.SetValue(c, value);
		}
	}

	private static string CleanName(string name)
	{
		if (name == null || !name.EndsWith("(Clone)"))
		{
			return name;
		}
		return name.Substring(0, name.Length - 7);
	}

	private static void ClaimNativeOrDestroyIfDuplicate(Character c, string eventKey = null)
	{
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			if (!((Object)(object)item.Value != (Object)(object)c))
			{
				Plugin.Log.LogWarning("[EnemySync] '" + ((Object)((Component)c).gameObject).name + "' já é puppet do peer (corrida entre EnemySpawn recebido e scan periódico) - NÃO reivindicando como meu.");
				return;
			}
		}
		string text = CleanName(((Object)((Component)c).gameObject).name);
		Character val = null;
		foreach (KeyValuePair<string, Character> item2 in _puppetById)
		{
			Character value = item2.Value;
			if (!((Object)(object)value == (Object)null) && ((CharBase)value).type == ((CharBase)c).type && !(CleanName(((Object)((Component)value).gameObject).name) != text) && _puppetSince.TryGetValue(item2.Key, out var value2) && !(Time.time - value2 >= 8f) && !(Vector3.Distance(((Component)value).transform.position, ((Component)c).transform.position) >= 800f))
			{
				val = value;
				break;
			}
		}
		if ((Object)(object)val != (Object)null)
		{
			Plugin.Log.LogWarning("[EnemySync] '" + text + "' nativo bem perto de um puppet já existente do mesmo tipo, adotado há pouco - provável duplicata de evento independente (ex. dança das noivas) - destruindo o extra local em vez de adotar.");
			Object.Destroy((Object)(object)((Component)c).gameObject);
			return;
		}
		string text2 = MyTag + ":" + ((Object)((Component)c).gameObject).GetInstanceID();
		_ownedCache.Add(new OwnedEnemy
		{
			c = c,
			id = text2,
			body = BodyAnim(c)
		});
		_ownedById[text2] = c;
		_ownedSince[text2] = Time.time;
		if (!string.IsNullOrEmpty(eventKey))
		{
			_ownedEventKey[text2] = eventKey;
		}
	}

	public static void RegisterQuestSpawn(Character c, string eventKey = null)
	{
		if ((Object)(object)c == (Object)null || !CoopRuntime.SessionActive)
		{
			return;
		}
		foreach (KeyValuePair<string, Character> item in _ownedById)
		{
			if ((Object)(object)item.Value == (Object)(object)c)
			{
				return;
			}
		}
		foreach (KeyValuePair<string, Character> item2 in _puppetById)
		{
			if ((Object)(object)item2.Value == (Object)(object)c)
			{
				return;
			}
		}
		Plugin.Log.LogInfo("[EnemySync][QuestSpawn] registrando personagem de quest AGORA (sem esperar scan): " + $"'{CleanName(((Object)((Component)c).gameObject).name)}' type={((CharBase)c).type} pos={((Component)c).transform.position:F0}" + ((!string.IsNullOrEmpty(eventKey)) ? (" eventKey='" + eventKey + "'") : ""));
		ClaimNativeOrDestroyIfDuplicate(c, eventKey);
	}

	public static byte[] BuildDelta(Vector3 p1, Vector3 p2)
	{
		Character[] array = SceneCache.Characters();
		if (array != _ownedRawLast)
		{
			_ownedRawLast = array;
			HashSet<Character> hashSet = new HashSet<Character>();
			foreach (OwnedEnemy item in _ownedCache)
			{
				if ((Object)(object)item.c != (Object)null)
				{
					hashSet.Add(item.c);
				}
			}
			HashSet<Character> hashSet2 = new HashSet<Character>();
			foreach (KeyValuePair<string, Character> item2 in _puppetById)
			{
				if ((Object)(object)item2.Value != (Object)null)
				{
					hashSet2.Add(item2.Value);
				}
			}
			Character[] array2 = array;
			foreach (Character val in array2)
			{
				if (!((Object)(object)val == (Object)null) && !hashSet.Contains(val) && !hashSet2.Contains(val))
				{
					ClaimNativeOrDestroyIfDuplicate(val);
				}
			}
		}
		HashSet<string> hashSet3 = new HashSet<string>();
		List<int> list = new List<int>();
		Dictionary<string, Character> dictionary = new Dictionary<string, Character>();
		for (int j = 0; j < _ownedCache.Count; j++)
		{
			OwnedEnemy ownedEnemy = _ownedCache[j];
			if ((Object)(object)ownedEnemy.c != (Object)null && ((CharBase)ownedEnemy.c).alive)
			{
				hashSet3.Add(ownedEnemy.id);
			}
			else if (_aliveSent.Contains(ownedEnemy.id))
			{
				list.Add(j);
				dictionary[ownedEnemy.id] = ownedEnemy.c;
			}
		}
		foreach (string item3 in _aliveSent)
		{
			if (!hashSet3.Contains(item3))
			{
				if (_pendingDeaths == null)
				{
					_pendingDeaths = new List<DeathInfo>();
				}
				dictionary.TryGetValue(item3, out var value);
				_pendingDeaths.Add(new DeathInfo
				{
					id = item3,
					cutInHalf = ((Object)(object)value != (Object)null && value.cutInHalf),
					deathAnim = GetDeathAnim(value),
					dayDied = (((Object)(object)value != (Object)null) ? value.dayDied : 0)
				});
			}
		}
		_aliveSent.Clear();
		_aliveSent.UnionWith(hashSet3);
		for (int num = list.Count - 1; num >= 0; num--)
		{
			string id = _ownedCache[list[num]].id;
			_ownedCache.RemoveAt(list[num]);
			_ownedSince.Remove(id);
		}
		List<OwnedEnemy> list2 = new List<OwnedEnemy>();
		foreach (OwnedEnemy item4 in _ownedCache)
		{
			if (!((Object)(object)item4.c == (Object)null) && ((CharBase)item4.c).alive)
			{
				if (CoopPeers.MinDistance(((Component)item4.c).transform.position, p1) <= 4000f)
				{
					list2.Add(item4);
				}
				if (list2.Count >= 60)
				{
					break;
				}
			}
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemyDelta).UShort((ushort)list2.Count);
		foreach (OwnedEnemy item5 in list2)
		{
			tk2dSpriteAnimator val2 = (((Object)(object)item5.body != (Object)null) ? item5.body : BodyAnim(item5.c));
			Quaternion q = (((Object)(object)val2 != (Object)null) ? ((Component)val2).transform.rotation : ((Component)item5.c).transform.rotation);
			string text = (((Object)(object)val2 != (Object)null && val2.CurrentClip != null) ? val2.CurrentClip.name : "");
			string v = (((Object)(object)item5.c.legsAnimator != (Object)null && item5.c.legsAnimator.CurrentClip != null) ? item5.c.legsAnimator.CurrentClip.name : "");
			if (!string.IsNullOrEmpty(item5.id) && !_announcedIds.Contains(item5.id))
			{
				_announcedIds.Add(item5.id);
				if (_pendingSpawns == null)
				{
					_pendingSpawns = new Dictionary<string, OwnedEnemy>();
				}
				_pendingSpawns[item5.id] = item5;
				Plugin.Log.LogInfo("[EnemySync][" + MyTag + "] anunciando NOVO (reliable): id=" + item5.id + " " + $"name='{((Object)((Component)item5.c).gameObject).name}' type={((CharBase)item5.c).type} pos={((Component)item5.c).transform.position:F0} " + $"health={((CharBase)item5.c).health:F0} instanceId={((Object)((Component)item5.c).gameObject).GetInstanceID()}");
			}
			if (!_lastSentBodyClip.TryGetValue(item5.id, out var value2) || value2 != text)
			{
				_lastSentBodyClip[item5.id] = text;
				Plugin.Log.LogInfo($"[EnemySync][ClipSend] id={item5.id} pos=({((Component)item5.c).transform.position.x:F0},{((Component)item5.c).transform.position.z:F0}) clipeTorso que EU mando mudou pra '{text}'");
			}
			packetWriter.Str(item5.id).Int((int)((CharBase)item5.c).type).Vec2XZ(((Component)item5.c).transform.position)
				.Float(((CharBase)item5.c).health)
				.Quat(q)
				.Str(text)
				.Str(v)
				.Int((int)item5.c.aggressiveness)
				.Bool(item5.c.sleeping)
				.Bool(item5.c.dontMove)
				.Str(item5.c.AnimationLibraryOverride ?? "");
		}
		_resendTimer += Time.deltaTime;
		if (_resendTimer >= 3f)
		{
			_resendTimer = 0f;
			foreach (OwnedEnemy item6 in _ownedCache)
			{
				if (!((Object)(object)item6.c == (Object)null) && ((CharBase)item6.c).alive && !string.IsNullOrEmpty(item6.id))
				{
					if (_pendingSpawns == null)
					{
						_pendingSpawns = new Dictionary<string, OwnedEnemy>();
					}
					_pendingSpawns[item6.id] = item6;
				}
			}
		}
		return packetWriter.Finish();
	}

	public static byte[] BuildSpawns()
	{
		if (_pendingSpawns == null || _pendingSpawns.Count == 0)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemySpawn).UShort((ushort)_pendingSpawns.Count);
		foreach (OwnedEnemy value2 in _pendingSpawns.Values)
		{
			_ownedEventKey.TryGetValue(value2.id, out var value);
			packetWriter.Str(value2.id).Int((int)((CharBase)value2.c).type).Vec2XZ(((Component)value2.c).transform.position)
				.Str(CleanName(((Object)((Component)value2.c).gameObject).name))
				.Str(value ?? "");
		}
		_pendingSpawns = null;
		return packetWriter.Finish();
	}

	public static void CheckAggroRedirect(Transform localPlayer, Transform peerPuppet)
	{
		if (!AggroRedirectEnabled || (Object)(object)peerPuppet == (Object)null || (Object)(object)localPlayer == (Object)null)
		{
			return;
		}
		foreach (OwnedEnemy item in _ownedCache)
		{
			Character c = item.c;
			if ((Object)(object)c == (Object)null || !((CharBase)c).alive || (bool)(Object)(object)c.npc || (int)c.aggressiveness == 300 || (int)c.aggressiveness == 600 || (int)c.behaviour == 600 || (Object)(object)c.target == (Object)(object)peerPuppet || (c.veryHungry && (c.headingForLure || c.eating)))
			{
				continue;
			}
			float num = Vector3.Distance(((Component)c).transform.position, localPlayer.position);
			float num2 = Vector3.Distance(((Component)c).transform.position, peerPuppet.position);
			float num3 = ((c.farViewDistance > 0) ? ((float)c.farViewDistance) : 300f);
			float num4 = ((c.nearViewDistance > 0) ? ((float)c.nearViewDistance) : (num3 * 0.4f));
			if (!(num2 > num3) && !(num2 >= num) && (Core.canSee(((Component)c).transform, peerPuppet) || Core.canSee(((Component)c).transform, localPlayer)))
			{
				if ((int)c.aggressiveness == 400 || (int)c.aggressiveness == 500 || (int)c.cowardiness == 50)
				{
					c.runAway(peerPuppet.position);
				}
				else if (num2 > num4)
				{
					c.stopAndListenTo(peerPuppet.position);
				}
				else
				{
					c.attackCharacter(peerPuppet);
				}
			}
		}
	}

	public static void CheckAoeDamageToPeers(IReadOnlyList<RemotePlayerPuppet> puppets)
	{
		_aoeDamageTimer += Time.deltaTime;
		if (_aoeDamageTimer < 1f)
		{
			return;
		}
		_aoeDamageTimer = 0f;
		foreach (RemotePlayerPuppet aoeTarget in puppets)
		{
		if ((Object)(object)aoeTarget == (Object)null || !aoeTarget.HasState || DownedSync.IsPeerDowned(aoeTarget.Slot) || DownedSync.IsPeerReallyDead(aoeTarget.Slot))
		{
			continue;
		}
		Transform peerPuppet = ((Component)aoeTarget).transform;
		foreach (OwnedEnemy item in _ownedCache)
		{
			Character c = item.c;
			if ((Object)(object)c == (Object)null || !((CharBase)c).alive || c.aroundMeDamageRange <= 0f || c.aroundMeDamage <= 0)
			{
				continue;
			}
			float num = Vector3.Distance(((Component)c).transform.position, peerPuppet.position);
			if (!(num > c.aroundMeDamageRange))
			{
				float num2 = (c.aroundMeDamageRange / 2f - num) / c.aroundMeDamageRange;
				if (!(num2 <= 0f))
				{
					float num3 = (float)c.aroundMeDamage * num2;
					Plugin.Log.LogInfo("[EnemySync][AoE] '" + ((Object)((Component)c).gameObject).name + "' dano em área acertaria o peer " + $"(dist={num:F0} range={c.aroundMeDamageRange:F0} dmg={num3:F1}) - repassando pro peer.");
					PlayerDamageSync.RelayHit(num3, normalHit: false, aoeTarget.Slot);
				}
			}
		}
		}
	}

	public static void SyncCombatTensionForPeers(IReadOnlyList<RemotePlayerPuppet> puppets)
	{
		Player p = Player.Instance;
		if ((Object)(object)p == (Object)null)
		{
			return;
		}
		bool changed = false;
		_countedAsAttackingPeer.RemoveWhere(delegate(Character c)
		{
			if ((Object)(object)c != (Object)null && ((CharBase)c).alive && TargetsAnyPuppet(c, puppets))
			{
				return false;
			}
			if ((Object)(object)c != (Object)null)
			{
				p.charactersAttackingMe.Remove(c);
			}
			changed = true;
			return true;
		});
		if (puppets.Count > 0)
		{
			foreach (OwnedEnemy item in _ownedCache)
			{
				Character c2 = item.c;
				if (!((Object)(object)c2 == (Object)null) && ((CharBase)c2).alive && TargetsAnyPuppet(c2, puppets) && _countedAsAttackingPeer.Add(c2))
				{
					if (!p.charactersAttackingMe.Contains(c2))
					{
						p.charactersAttackingMe.Add(c2);
					}
					changed = true;
				}
			}
		}
		if (changed)
		{
			p.checkInCombatChars();
		}
	}

	private static bool NearAny(Vector3 x, List<Vector3> positions, float range)
	{
		for (int i = 0; i < positions.Count; i++)
		{
			if (Vector3.Distance(x, positions[i]) <= range)
			{
				return true;
			}
		}
		return false;
	}

	private static bool TargetsAnyPuppet(Character c, IReadOnlyList<RemotePlayerPuppet> puppets)
	{
		Transform target = c.target;
		if ((Object)(object)target == (Object)null)
		{
			return false;
		}
		for (int i = 0; i < puppets.Count; i++)
		{
			if ((Object)(object)puppets[i] != (Object)null && (Object)(object)target == (Object)(object)((Component)puppets[i]).transform)
			{
				return true;
			}
		}
		return false;
	}

	public static void ReleaseAggroFromPuppet(Transform peerPuppet)
	{
		if ((Object)(object)peerPuppet == (Object)null)
		{
			return;
		}
		foreach (OwnedEnemy item in _ownedCache)
		{
			Character c = item.c;
			if (!((Object)(object)c == (Object)null) && ((CharBase)c).alive && !((Object)(object)c.target != (Object)(object)peerPuppet))
			{
				c.stopAttacking(peerPuppet);
			}
		}
	}

	public static byte[] BuildDeaths()
	{
		if (_pendingDeaths == null || _pendingDeaths.Count == 0)
		{
			return null;
		}
		Plugin.Log.LogInfo($"[EnemySync][{MyTag}] anunciando {_pendingDeaths.Count} morte(s).");
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemyDeath).UShort((ushort)_pendingDeaths.Count);
		foreach (DeathInfo pendingDeath in _pendingDeaths)
		{
			packetWriter.Str(pendingDeath.id).Bool(pendingDeath.cutInHalf).Str(pendingDeath.deathAnim ?? "")
				.Int(pendingDeath.dayDied);
		}
		_pendingDeaths = null;
		return packetWriter.Finish();
	}

	public static void ApplyHitRequest(PacketReader r, Transform attacker)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			float num2 = r.Float();
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			if (!_ownedById.TryGetValue(text, out var value) || (Object)(object)value == (Object)null || !((CharBase)value).alive)
			{
				Plugin.Log.LogWarning($"[EnemySync][Hit] id='{text}' dmg={num2:F1} NÃO encontrado em _ownedById " + $"(cache tem {_ownedById.Count} ids) — ignorado.");
				continue;
			}
			Plugin.Log.LogInfo($"[EnemySync][Hit] aplicando dano cruzado: id='{text}' dmg={num2:F1} " + string.Format("alvo='{0}' hpAntes={1:F1} attacker={2}", ((Object)value).name, ((CharBase)value).health, ((Object)(object)attacker != (Object)null) ? ((Object)attacker).name : "null"));
			float num3 = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.saturation : 0f);
			bool alive = ((CharBase)value).alive;
			((CharBase)value).getHit(num2, attacker, false, true, true, true, false, false, false);
			if (alive && !((CharBase)value).alive && (Object)(object)Player.Instance != (Object)null && Player.Instance.saturation != num3)
			{
				Plugin.Log.LogInfo("[EnemySync][Hit] morte por dano cruzado deu saturação errada pro meu jogador " + $"local ({num3:F1}->{Player.Instance.saturation:F1}) - desfazendo (quem matou foi o peer).");
				Player.Instance.saturation = num3;
			}
			Plugin.Log.LogInfo($"[EnemySync][Hit] hpDepois={((CharBase)value).health:F1} alive={((CharBase)value).alive}");
			if (!(((CharBase)value).health <= 0f) || !((CharBase)value).alive)
			{
				continue;
			}
			Plugin.Log.LogWarning("[EnemySync][Hit] '" + ((Object)value).name + "' (id=" + text + ") zerou a vida mas alive continua True — forçando morte manualmente pro peer não travar.");
			if (_pendingDeaths == null)
			{
				_pendingDeaths = new List<DeathInfo>();
			}
			_pendingDeaths.Add(new DeathInfo
			{
				id = text,
				cutInHalf = value.cutInHalf,
				deathAnim = GetDeathAnim(value),
				dayDied = value.dayDied
			});
			_aliveSent.Remove(text);
			_ownedSince.Remove(text);
			for (int num4 = _ownedCache.Count - 1; num4 >= 0; num4--)
			{
				if (_ownedCache[num4].id == text)
				{
					_ownedCache.RemoveAt(num4);
					break;
				}
			}
		}
	}

	public static void WakePuppetsNearPeers(List<Vector3> peerPositions)
	{
		_puppetWakeTimer += Time.deltaTime;
		if (_puppetWakeTimer < 1f)
		{
			return;
		}
		_puppetWakeTimer = 0f;
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			Character value = item.Value;
			if (!((Object)(object)value == (Object)null) && !((Component)value).gameObject.activeSelf && NearAny(((Component)value).transform.position, peerPositions, 1200f))
			{
				((Component)value).gameObject.SetActive(true);
				Collider component = ((Component)value).GetComponent<Collider>();
				if ((Object)(object)component != (Object)null)
				{
					component.enabled = true;
				}
				Collider2D component2 = ((Component)value).GetComponent<Collider2D>();
				if ((Object)(object)component2 != (Object)null)
				{
					((Behaviour)component2).enabled = true;
				}
				Plugin.Log.LogInfo("[EnemySync] acordei puppet inativo perto do peer (WorldGrid só ativa por proximidade a MIM, e eu não estava perto): '" + ((Object)((Component)value).gameObject).name + "' (id=" + item.Key + ", " + $"alive={((CharBase)value).alive}) - colisor religado: 3D={(Object)(object)component != (Object)null} 2D={(Object)(object)component2 != (Object)null}.");
			}
		}
	}

	public static void WakeOwnedNearPeers(List<Vector3> peerPositions)
	{
		_ownedWakeTimer += Time.deltaTime;
		if (_ownedWakeTimer < 1f)
		{
			return;
		}
		_ownedWakeTimer = 0f;
		foreach (KeyValuePair<string, Character> item in _ownedById)
		{
			Character value = item.Value;
			if (!((Object)(object)value == (Object)null) && !((Component)value).gameObject.activeSelf && NearAny(((Component)value).transform.position, peerPositions, 1200f))
			{
				((Component)value).gameObject.SetActive(true);
				Collider component = ((Component)value).GetComponent<Collider>();
				if ((Object)(object)component != (Object)null)
				{
					component.enabled = true;
				}
				Collider2D component2 = ((Component)value).GetComponent<Collider2D>();
				if ((Object)(object)component2 != (Object)null)
				{
					((Behaviour)component2).enabled = true;
				}
				Plugin.Log.LogInfo("[EnemySync] acordei personagem MEU (dono de verdade) desativado pelo WorldGrid nativo (só ativa por proximidade a MIM) perto do peer: '" + ((Object)((Component)value).gameObject).name + "' (id=" + item.Key + ", " + $"alive={((CharBase)value).alive}) - colisor religado: 3D={(Object)(object)component != (Object)null} 2D={(Object)(object)component2 != (Object)null}.");
			}
		}
	}

	public static void ApplyDelta(PacketReader r)
	{
		int num = r.UShort();
		int num2 = 0;
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			r.Int();
			Vector3 pos = r.Vec2XZ(0f);
			float num3 = r.Float();
			Quaternion rot = r.Quat();
			string bodyClip = r.Str();
			string legsClip = r.Str();
			int num4 = r.Int();
			bool flag = r.Bool();
			bool flag2 = r.Bool();
			string text2 = r.Str();
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			if (!_puppetById.TryGetValue(text, out var value) || (Object)(object)value == (Object)null)
			{
				if ((Object)(object)value == (Object)null)
				{
					_puppetById.Remove(text);
					RemovePuppetTracking(text);
				}
				continue;
			}
			if (CoopRuntime.Sender >= 0)
			{
				_puppetOwner[text] = CoopRuntime.Sender;
			}
			DetectLocalDamage(text, value);
			DetectLocalBehaviorChange(text, value);
			Drive(text, value, pos, num3, rot, bodyClip, legsClip);
			value.aggressiveness = (Aggressiveness)num4;
			value.sleeping = flag;
			value.dontMove = flag2;
			if (!string.IsNullOrEmpty(text2) && value.AnimationLibraryOverride != text2)
			{
				value.animationLibraryOverride = text2;
			}
			_puppetLastHp[text] = num3;
			_puppetLastAggressiveness[text] = (Aggressiveness)num4;
			_puppetLastSleeping[text] = flag;
			_puppetLastDontMove[text] = flag2;
			num2++;
		}
		if (num2 != _lastMatched)
		{
			Plugin.Log.LogInfo($"[EnemySync] {num2}/{num} inimigos pareados" + ((num2 == 0 && num > 0) ? " (0 — aguardando EnemySpawn reliable?)" : ""));
			_lastMatched = num2;
		}
	}

	public static void ApplySpawns(PacketReader r)
	{
		int num = r.UShort();
		HashSet<Character> hashSet = new HashSet<Character>();
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			if ((Object)(object)item.Value != (Object)null)
			{
				hashSet.Add(item.Value);
			}
		}
		foreach (KeyValuePair<string, Character> item2 in _ownedById)
		{
			if ((Object)(object)item2.Value != (Object)null)
			{
				hashSet.Add(item2.Value);
			}
		}
		Character[] array = null;
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			int num2 = r.Int();
			Vector3 val = r.Vec2XZ(0f);
			string text2 = r.Str();
			string text3 = r.Str();
			if (string.IsNullOrEmpty(text) || _puppetById.ContainsKey(text))
			{
				continue;
			}
			if (array == null)
			{
				array = SceneCache.Characters();
			}
			Character val2 = null;
			float num3 = 800f;
			Character[] array2 = array;
			foreach (Character val3 in array2)
			{
				if (!((Object)(object)val3 == (Object)null) && ((CharBase)val3).alive && (int)((CharBase)val3).type == num2 && !hashSet.Contains(val3))
				{
					float num4 = Vector3.Distance(((Component)val3).transform.position, val);
					if (num4 < num3)
					{
						num3 = num4;
						val2 = val3;
					}
				}
			}
			if ((Object)(object)val2 == (Object)null && !string.IsNullOrEmpty(text2))
			{
				float num5 = 3000f;
				array2 = array;
				foreach (Character val4 in array2)
				{
					if (!((Object)(object)val4 == (Object)null) && ((CharBase)val4).alive && (int)((CharBase)val4).type == num2 && !hashSet.Contains(val4) && !(CleanName(((Object)((Component)val4).gameObject).name) != text2))
					{
						float num6 = Vector3.Distance(((Component)val4).transform.position, val);
						if (num6 < num5)
						{
							num5 = num6;
							val2 = val4;
						}
					}
				}
				if ((Object)(object)val2 != (Object)null)
				{
					Plugin.Log.LogInfo("[EnemySync] casado por NOME em raio largo (evita duplicata de outside " + $"location): '{text2}' id={text} dist={num5:F0}");
				}
			}
			if ((Object)(object)val2 == (Object)null && !string.IsNullOrEmpty(text2))
			{
				float num7 = 300f;
				array2 = Object.FindObjectsOfType<Character>(true);
				foreach (Character val5 in array2)
				{
					if (!((Object)(object)val5 == (Object)null) && ((CharBase)val5).alive && !((Component)val5).gameObject.activeSelf && (int)((CharBase)val5).type == num2 && !hashSet.Contains(val5) && !(CleanName(((Object)((Component)val5).gameObject).name) != text2))
					{
						float num8 = Vector3.Distance(((Component)val5).transform.position, val);
						if (num8 < num7)
						{
							num7 = num8;
							val2 = val5;
						}
					}
				}
				if ((Object)(object)val2 != (Object)null)
				{
					((Component)val2).gameObject.SetActive(true);
					Collider component = ((Component)val2).GetComponent<Collider>();
					if ((Object)(object)component != (Object)null)
					{
						component.enabled = true;
					}
					Collider2D component2 = ((Component)val2).GetComponent<Collider2D>();
					if ((Object)(object)component2 != (Object)null)
					{
						((Behaviour)component2).enabled = true;
					}
					Plugin.Log.LogInfo("[EnemySync] casado com gêmeo INATIVO (esperando WorldGrid ativar) - " + $"ativado manualmente pra virar puppet: '{text2}' id={text} dist={num7:F0} " + $"(colisor religado: 3D={(Object)(object)component != (Object)null} 2D={(Object)(object)component2 != (Object)null})");
				}
			}
			if ((Object)(object)val2 == (Object)null && !string.IsNullOrEmpty(text2))
			{
				string text4 = null;
				Character val6 = null;
				foreach (KeyValuePair<string, Character> item3 in _ownedById)
				{
					Character value = item3.Value;
					if ((Object)(object)value == (Object)null || (int)((CharBase)value).type != num2 || !_ownedSince.TryGetValue(item3.Key, out var value2) || Time.time - value2 >= 8f)
					{
						continue;
					}
					string value3;
					bool flag = !string.IsNullOrEmpty(text3) && _ownedEventKey.TryGetValue(item3.Key, out value3) && value3 == text3;
					bool flag2 = Vector3.Distance(((Component)value).transform.position, val) < 800f;
					if (flag || flag2)
					{
						if (flag && !flag2)
						{
							Plugin.Log.LogWarning("[EnemySync] casado por IDENTIDADE DE EVENTO (não por posição - " + $"divergiu {Vector3.Distance(((Component)value).transform.position, val):F0}un, provável RNG não " + "semeado entre os 2 PCs): '" + text2 + "' eventKey='" + text3 + "'");
						}
						text4 = item3.Key;
						val6 = value;
						break;
					}
				}
				if ((Object)(object)val6 != (Object)null)
				{
					bool num9 = text.StartsWith("H:");
					bool flag3 = CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host;
					if (!num9 || flag3)
					{
						Plugin.Log.LogWarning("[EnemySync] ignorando spawn anunciado '" + text2 + "' id=" + text + " - já possuo um do mesmo tipo bem perto, reivindicado há pouco (provável eco de corrida) - não duplica.");
						continue;
					}
					Plugin.Log.LogWarning("[EnemySync] desempate de spawn simultâneo: cedendo '" + text2 + "' que eu possuía (id local='" + text4 + "') pro anúncio do HOST (id=" + text + ") - host sempre manda.");
					_ownedById.Remove(text4);
					_ownedSince.Remove(text4);
					_ownedEventKey.Remove(text4);
					_aliveSent.Remove(text4);
					for (int num10 = _ownedCache.Count - 1; num10 >= 0; num10--)
					{
						if (_ownedCache[num10].id == text4)
						{
							_ownedCache.RemoveAt(num10);
							break;
						}
					}
					val2 = val6;
				}
			}
			if ((Object)(object)val2 == (Object)null && !string.IsNullOrEmpty(text2))
			{
				GameObject val7 = Core.AddPrefab("Characters/" + text2, val, Quaternion.Euler(90f, 0f, 0f), (GameObject)null, true);
				val2 = (((Object)(object)val7 != (Object)null) ? val7.GetComponent<Character>() : null);
				if ((Object)(object)val2 != (Object)null)
				{
					Plugin.Log.LogInfo("[EnemySync] spawn novo criado: '" + text2 + "' id=" + text);
				}
				else
				{
					Plugin.Log.LogWarning("[EnemySync] falhou ao criar '" + text2 + "' (Characters/" + text2 + " não encontrado?)");
				}
			}
			if ((Object)(object)val2 != (Object)null)
			{
				_puppetById[text] = val2;
				_puppetSince[text] = Time.time;
				_puppetOwner[text] = CoopRuntime.Sender;
				hashSet.Add(val2);
				((Behaviour)val2).enabled = false;
				_puppetLastHp[text] = ((CharBase)val2).health;
			}
		}
	}

	public static void PruneLocalOrphans()
	{
		HashSet<Character> hashSet = new HashSet<Character>();
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			if ((Object)(object)item.Value != (Object)null)
			{
				hashSet.Add(item.Value);
			}
		}
		foreach (KeyValuePair<string, Character> item2 in _ownedById)
		{
			if ((Object)(object)item2.Value != (Object)null)
			{
				hashSet.Add(item2.Value);
			}
		}
		HashSet<Character> hashSet2 = new HashSet<Character>();
		Character[] array = SceneCache.Characters();
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null) && ((CharBase)val).alive && !hashSet.Contains(val))
			{
				hashSet2.Add(val);
				if (!_unmatchedFirstSeen.ContainsKey(val))
				{
					_unmatchedFirstSeen[val] = Time.time;
				}
			}
		}
		List<Character> list = new List<Character>();
		foreach (KeyValuePair<Character, float> item3 in _unmatchedFirstSeen)
		{
			Character key = item3.Key;
			if (!hashSet2.Contains(key))
			{
				list.Add(key);
			}
			else if (!(Time.time - item3.Value < 8f))
			{
				list.Add(key);
				if (!((Object)(object)key == (Object)null))
				{
					Plugin.Log.LogWarning("[EnemySync] '" + ((Object)((Component)key).gameObject).name + "' nunca foi confirmado pelo peer em " + $"{8f:F0}s (pos={((Component)key).transform.position:F0}) - provável " + "duplicata local (geração de mundo independente) - removendo.");
					Object.Destroy((Object)(object)((Component)key).gameObject);
				}
			}
		}
		foreach (Character item4 in list)
		{
			_unmatchedFirstSeen.Remove(item4);
		}
	}

	public static void RegisterForcedRemoval(Character c)
	{
		if ((Object)(object)c == (Object)null)
		{
			return;
		}
		string text = ResolveOwnedId(c);
		if (!string.IsNullOrEmpty(text))
		{
			Plugin.Log.LogInfo("[EnemySync] '" + ((Object)((Component)c).gameObject).name + "' (id=" + text + ") vai ser destruído por limpeza NATIVA (wipe pós-morte ou amanhecer) - avisando o peer pra remover o puppet dele também.");
			if (_pendingRemovals == null)
			{
				_pendingRemovals = new List<string>();
			}
			_pendingRemovals.Add(text);
		}
	}

	public static byte[] BuildRemovals()
	{
		if (_pendingRemovals == null || _pendingRemovals.Count == 0)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemyRemoved).UShort((ushort)_pendingRemovals.Count);
		foreach (string pendingRemoval in _pendingRemovals)
		{
			packetWriter.Str(pendingRemoval);
		}
		_pendingRemovals = null;
		return packetWriter.Finish();
	}

	public static void ApplyRemovals(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			if (!string.IsNullOrEmpty(text))
			{
				if (!_puppetById.TryGetValue(text, out var value) || (Object)(object)value == (Object)null)
				{
					_puppetById.Remove(text);
					RemovePuppetTracking(text);
					continue;
				}
				Plugin.Log.LogInfo("[EnemySync] peer destruiu de vez '" + ((Object)((Component)value).gameObject).name + "' (id=" + text + ", limpeza nativa do lado dele) - destruindo o puppet local também.");
				Object.Destroy((Object)(object)((Component)value).gameObject);
				_puppetById.Remove(text);
				RemovePuppetTracking(text);
			}
		}
	}

	public static void ApplyDeaths(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			bool flag = r.Bool();
			string text2 = r.Str();
			int dayDied = r.Int();
			if (string.IsNullOrEmpty(text))
			{
				continue;
			}
			if (!_puppetById.TryGetValue(text, out var value) || (Object)(object)value == (Object)null)
			{
				_puppetById.Remove(text);
				RemovePuppetTracking(text);
				continue;
			}
			EnemyPuppet component = ((Component)value).GetComponent<EnemyPuppet>();
			if ((Object)(object)component != (Object)null)
			{
				component.SnapToTarget();
			}
			if ((Object)(object)component != (Object)null)
			{
				Object.Destroy((Object)(object)component);
			}
			((Behaviour)value).enabled = true;
			if (!((CharBase)value).alive)
			{
				((CharBase)value).alive = true;
				((CharBase)value).health = 1f;
			}
			value.dayDied = dayDied;
			SetDeathAnim(value, text2);
			_applyingDeath = true;
			try
			{
				((CharBase)value).getHit(99999f, (Transform)null, flag, false, false, true, false, true, true);
			}
			finally
			{
				_applyingDeath = false;
			}
			Plugin.Log.LogInfo($"[EnemySync] morte aplicada no puppet: id={text} name='{((Object)value).name}' cutInHalf={flag} deathAnim='{text2}'");
			RemovePuppetTracking(text);
		}
	}

	// ---- a puppet never dies on its own ----
	// Our copy of an enemy someone else's game runs used to die here when we landed the killing blow,
	// while the real one (with a little more health there) lived on: a corpse that kept getting moved
	// around and couldn't be hit any more. Now the hit's damage beyond "1 health left" goes straight to
	// the owner as a hit request, and the death comes back from the owner like any other.
	private static bool _patched;

	private static bool _applyingDeath;

	public static void EnsurePatched(string modId)
	{
		if (_patched)
		{
			return;
		}
		_patched = true;
		try
		{
			MethodInfo method = HarmonyLib.AccessTools.Method(typeof(CharBase), "getHit", new System.Type[9]
			{
				typeof(float),
				typeof(Transform),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, (System.Type[])null);
			HarmonyLib.Harmony harmony = new HarmonyLib.Harmony(modId + ".puppetdeath");
			HarmonyLib.HarmonyMethod prefix = new HarmonyLib.HarmonyMethod(typeof(EnemySync), "PuppetGetHitPrefix", (System.Type[])null);
			harmony.Patch((System.Reflection.MethodBase)method, prefix, (HarmonyLib.HarmonyMethod)null, (HarmonyLib.HarmonyMethod)null, (HarmonyLib.HarmonyMethod)null, (HarmonyLib.HarmonyMethod)null);
			// enemies may have their own getHit: guard that one too
			MethodInfo methodInfo = HarmonyLib.AccessTools.DeclaredMethod(typeof(Character), "getHit", method.GetParameters().Select((System.Reflection.ParameterInfo p) => p.ParameterType).ToArray(), (System.Type[])null);
			if (methodInfo != null && methodInfo != method)
			{
				harmony.Patch((System.Reflection.MethodBase)methodInfo, prefix, (HarmonyLib.HarmonyMethod)null, (HarmonyLib.HarmonyMethod)null, (HarmonyLib.HarmonyMethod)null, (HarmonyLib.HarmonyMethod)null);
			}
		}
		catch (System.Exception ex)
		{
			Plugin.Log.LogWarning("[EnemySync] puppet death guard not installed: " + ex.Message);
		}
	}

	private static void PuppetGetHitPrefix(CharBase __instance, ref float damage)
	{
		if (_applyingDeath || damage <= 0f || !CoopRuntime.SessionActive || !(__instance is Character c) || !__instance.alive)
		{
			return;
		}
		string text = FindPuppetId(c);
		if (text == null)
		{
			return;
		}
		float num = Mathf.Max(0f, __instance.health - 1f);
		if (!(damage <= num))
		{
			float item = damage - num;
			damage = num;
			if (_pendingHits == null)
			{
				_pendingHits = new List<(string, float)>();
			}
			_pendingHits.Add((text, item));
		}
	}

	public static string FindOwnedId(Character c)
	{
		if ((Object)(object)c == (Object)null)
		{
			return null;
		}
		foreach (KeyValuePair<string, Character> item in _ownedById)
		{
			if ((Object)(object)item.Value == (Object)(object)c)
			{
				return item.Key;
			}
		}
		return null;
	}

	public static string FindPuppetId(Character c)
	{
		if ((Object)(object)c == (Object)null)
		{
			return null;
		}
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			if ((Object)(object)item.Value == (Object)(object)c)
			{
				return item.Key;
			}
		}
		return null;
	}

	public static void PlayHitReactionOnPuppet(string id, string clip)
	{
		if (_puppetById.TryGetValue(id, out var value) && !((Object)(object)value == (Object)null))
		{
			((Component)value).GetComponent<EnemyPuppet>()?.PlayHitReaction(clip);
		}
	}

	private static void Drive(string id, Character c, Vector3 pos, float hp, Quaternion rot, string bodyClip, string legsClip)
	{
		((CharBase)c).health = hp;
		EnemyPuppet enemyPuppet = ((Component)c).GetComponent<EnemyPuppet>();
		if ((Object)(object)enemyPuppet == (Object)null)
		{
			enemyPuppet = ((Component)c).gameObject.AddComponent<EnemyPuppet>();
			enemyPuppet.Bind(c, BodyAnim(c));
		}
		enemyPuppet.SetTarget(pos, rot, bodyClip, legsClip);
		if (!_lastRecvBodyClip.TryGetValue(id, out var value) || value != bodyClip)
		{
			_lastRecvBodyClip[id] = bodyClip;
			Plugin.Log.LogInfo($"[EnemySync][ClipRecv] id={id} pos=({pos.x:F0},{pos.z:F0}) clipeTorso da rede mudou pra '{bodyClip}'");
		}
	}

	private static void DetectLocalDamage(string id, Character c)
	{
		if (!_puppetLastHp.TryGetValue(id, out var value))
		{
			return;
		}
		float num = value - ((CharBase)c).health;
		if (!(num <= 0.5f))
		{
			Plugin.Log.LogInfo("[EnemySync][Hit] detectei dano local num puppet: id='" + id + "' alvo='" + ((Object)c).name + "' " + $"prevHp={value:F1} nowHp={((CharBase)c).health:F1} dmg={num:F1}");
			if (_pendingHits == null)
			{
				_pendingHits = new List<(string, float)>();
			}
			_pendingHits.Add((id, num));
		}
	}

	private static void DetectLocalBehaviorChange(string id, Character c)
	{
		if (!_puppetLastAggressiveness.TryGetValue(id, out var value))
		{
			return;
		}
		bool value2;
		bool flag = _puppetLastSleeping.TryGetValue(id, out value2) && value2;
		bool value3;
		bool flag2 = _puppetLastDontMove.TryGetValue(id, out value3) && value3;
		if (c.aggressiveness != value || c.sleeping != flag || c.dontMove != flag2)
		{
			Plugin.Log.LogInfo("[EnemySync][Behavior] detectei mudança local num puppet (provável emboscada numa " + $"Location do peer): id='{id}' alvo='{((Object)c).name}' aggressiveness {value}->{c.aggressiveness} " + $"sleeping {flag}->{c.sleeping} dontMove {flag2}->{c.dontMove}");
			if (_pendingBehaviorRequests == null)
			{
				_pendingBehaviorRequests = new List<(string, Aggressiveness, bool, bool)>();
			}
			_pendingBehaviorRequests.Add((id, c.aggressiveness, c.sleeping, c.dontMove));
		}
	}

	public static byte[] BuildBehaviorRequests()
	{
		if (_pendingBehaviorRequests == null || _pendingBehaviorRequests.Count == 0)
		{
			return null;
		}
		Plugin.Log.LogInfo($"[EnemySync][Behavior] mandando {_pendingBehaviorRequests.Count} pedido(s) de comportamento ao peer.");
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemyBehaviorRequest).UShort((ushort)_pendingBehaviorRequests.Count);
		foreach (var pendingBehaviorRequest in _pendingBehaviorRequests)
		{
			packetWriter.Str(pendingBehaviorRequest.id).Int((int)pendingBehaviorRequest.aggressiveness).Bool(pendingBehaviorRequest.sleeping)
				.Bool(pendingBehaviorRequest.dontMove);
		}
		_pendingBehaviorRequests = null;
		return packetWriter.Finish();
	}

	public static void ApplyBehaviorRequest(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			int num2 = r.Int();
			bool flag = r.Bool();
			bool flag2 = r.Bool();
			if (!string.IsNullOrEmpty(text) && _ownedById.TryGetValue(text, out var value) && !((Object)(object)value == (Object)null))
			{
				Plugin.Log.LogInfo("[EnemySync][Behavior] aplicando pedido do peer (emboscada numa Location minha): " + $"id='{text}' alvo='{((Object)value).name}' aggressiveness={(object)(Aggressiveness)num2} sleeping={flag} dontMove={flag2}");
				value.aggressiveness = (Aggressiveness)num2;
				value.sleeping = flag;
				value.dontMove = flag2;
			}
		}
	}

	private static void RemovePuppetTracking(string id)
	{
		_puppetSince.Remove(id);
		_puppetLastHp.Remove(id);
		_puppetLastAggressiveness.Remove(id);
		_puppetLastSleeping.Remove(id);
		_puppetLastDontMove.Remove(id);
	}

	public static byte[] BuildHitRequests()
	{
		if (_pendingHits == null || _pendingHits.Count == 0)
		{
			return null;
		}
		Plugin.Log.LogInfo($"[EnemySync][Hit] mandando {_pendingHits.Count} pedido(s) de dano ao peer.");
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.EnemyHit).UShort((ushort)_pendingHits.Count);
		foreach (var (v, v2) in _pendingHits)
		{
			packetWriter.Str(v).Float(v2);
		}
		_pendingHits = null;
		return packetWriter.Finish();
	}

	// The player running these enemies left: nobody can move them or take hits for them any more, so they
	// go (on every PC alike). They come back when the area is set up again.
	public static void OnPeerLeft(int slot)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, int> item in _puppetOwner)
		{
			if (item.Value == slot)
			{
				list.Add(item.Key);
			}
		}
		foreach (string item2 in list)
		{
			_puppetOwner.Remove(item2);
			if (_puppetById.TryGetValue(item2, out var value))
			{
				_puppetById.Remove(item2);
				RemovePuppetTracking(item2);
				if ((Object)(object)value != (Object)null)
				{
					Object.Destroy((Object)(object)((Component)value).gameObject);
				}
			}
		}
		if (list.Count > 0)
		{
			Plugin.Log.LogInfo($"[EnemySync] slot {slot} left - removed {list.Count} enemy puppet(s) they were running.");
		}
	}

	public static void Reset()
	{
		_puppetOwner.Clear();
		foreach (KeyValuePair<string, Character> item in _puppetById)
		{
			Character value = item.Value;
			if (!((Object)(object)value == (Object)null))
			{
				EnemyPuppet component = ((Component)value).GetComponent<EnemyPuppet>();
				if ((Object)(object)component != (Object)null)
				{
					Object.Destroy((Object)(object)component);
				}
				((Behaviour)value).enabled = true;
				Rigidbody component2 = ((Component)value).GetComponent<Rigidbody>();
				if ((Object)(object)component2 != (Object)null)
				{
					component2.isKinematic = false;
				}
			}
		}
		_puppetById.Clear();
		_puppetSince.Clear();
		_puppetLastHp.Clear();
		_puppetLastAggressiveness.Clear();
		_puppetLastSleeping.Clear();
		_puppetLastDontMove.Clear();
		_pendingHits = null;
		_pendingBehaviorRequests = null;
		_lastMatched = -1;
		_ownedCache.Clear();
		_ownedById.Clear();
		_ownedSince.Clear();
		_ownedEventKey.Clear();
		_announcedIds.Clear();
		_countedAsAttackingPeer.Clear();
		_aoeDamageTimer = 0f;
		_puppetWakeTimer = 0f;
		_ownedWakeTimer = 0f;
		_aliveSent.Clear();
		_pendingDeaths = null;
		_pendingRemovals = null;
		_pendingSpawns = null;
		_resendTimer = 0f;
		_unmatchedFirstSeen.Clear();
		_ownedRawLast = null;
		_lastRecvBodyClip.Clear();
		_lastSentBodyClip.Clear();
	}
}
