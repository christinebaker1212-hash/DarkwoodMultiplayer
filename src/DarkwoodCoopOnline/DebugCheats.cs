using System;
using System.Collections.Generic;
using DarkwoodCoopOnline.Sync;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

public static class DebugCheats
{
	public struct SpawnEntry
	{
		public string Label;

		public string Prefab;

		public SpawnEntry(string label, string prefab)
		{
			Label = label;
			Prefab = prefab;
		}
	}

	public struct EventEntry
	{
		public string Label;

		public string Keyword;

		public EventEntry(string label, string keyword)
		{
			Label = label;
			Keyword = keyword;
		}
	}

	public static bool VanillaMode;

	public static readonly string[] SpawnableEnemies = new string[3] { "Dog", "Redneck02", "Rabbit" };

	private static int _spawnCycle;

	public static readonly SpawnEntry[] AllSpawnable = new SpawnEntry[32]
	{
		new SpawnEntry("Banshee (Baby)", "BansheeBaby"),
		new SpawnEntry("Burning Villager", "VillagerBurning"),
		new SpawnEntry("Centipede", "Centipede"),
		new SpawnEntry("Chomper (Black)", "ChomperBlack"),
		new SpawnEntry("Chomper (Red)", "ChomperRed"),
		new SpawnEntry("Elk", "Elk"),
		new SpawnEntry("Floor Gore", "FloorGore"),
		new SpawnEntry("Huge Dog", "DogHuge"),
		new SpawnEntry("Human Spider", "HumanSpider"),
		new SpawnEntry("Mushroom Man", "MushroomGranny"),
		new SpawnEntry("Shadow", "Shadow"),
		new SpawnEntry("Swamper", "Swamper1"),
		new SpawnEntry("Dog", "Dog"),
		new SpawnEntry("Pig", "Pig"),
		new SpawnEntry("Savage (Horned)", "SavageHorned"),
		new SpawnEntry("Savage (Rock)", "SavageRock"),
		new SpawnEntry("Savage (Stick)", "SavageStick"),
		new SpawnEntry("The Sow", "Sow"),
		new SpawnEntry("Vines", "Vines"),
		new SpawnEntry("Black Substance", "BlackSubstance"),
		new SpawnEntry("Bride", "Bride"),
		new SpawnEntry("Chicken", "Chicken"),
		new SpawnEntry("Crow/Raven", "Raven"),
		new SpawnEntry("Glare", "Glare"),
		new SpawnEntry("Growths", "Growths"),
		new SpawnEntry("Huge Bug", "BugHuge"),
		new SpawnEntry("Rabbit", "Rabbit"),
		new SpawnEntry("Savage (Hole)", "SavageHole"),
		new SpawnEntry("The Snout", "Snout"),
		new SpawnEntry("Worms", "Worms"),
		new SpawnEntry("Redneck (log real)", "Redneck02"),
		new SpawnEntry("Night Trader", "NightTrader")
	};

	public static readonly EventEntry[] KnownNightEvents = new EventEntry[6]
	{
		new EventEntry("Mysterious Benefactor (door knock)", "doorKnocking"),
		new EventEntry("The Wedding (invitation)", "wedding"),
		new EventEntry("Poltergeist", "poltergeist"),
		new EventEntry("Earthquake", "earthQuake"),
		new EventEntry("Bug Infestation", "infestation"),
		new EventEntry("Heartbeat / Flesh", "flesh")
	};

	private static bool _noclip;

	private static readonly string[] TrainWreckDreamKeys = new string[1] { "big_trainWreckDream_01" };

	private const string TrainWreckDreamPrefix = "dream_doctor";

	public static bool NoclipActive => _noclip;

	public static void SetOwnHealthCritical()
	{
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			instance.setHealth(1f);
			Plugin.Log.LogInfo("[DebugCheats] vida do jogador local setada pra 1.");
		}
	}

	public static void NudgeOwnSortingOrder(int delta)
	{
		Player instance = Player.Instance;
		tk2dBaseSprite val = (((Object)(object)instance != (Object)null && (Object)(object)instance.torsoAnimator != (Object)null) ? instance.torsoAnimator.Sprite : null);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] torso/sprite não encontrado.");
			return;
		}
		val.SortingOrder += delta;
		Plugin.Log.LogInfo($"[DebugCheats] SortingOrder do torso local = {val.SortingOrder}.");
	}

	public static void GiveItemByKeyword(string keyword, int amount)
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance.Inventory == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Player.Instance/Inventory nulo - ignorado.");
			return;
		}
		ItemsDatabase instance2 = Singleton<ItemsDatabase>.Instance;
		if ((Object)(object)instance2 == (Object)null || instance2.itemsDict == null)
		{
			Plugin.Log.LogWarning("[DebugCheats] ItemsDatabase indisponível.");
			return;
		}
		int num = 0;
		foreach (string key in instance2.itemsDict.Keys)
		{
			if (!string.IsNullOrEmpty(key) && key.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				num++;
				InvItemClass val = instance.Inventory.addItemType(key, amount);
				Plugin.Log.LogInfo((val != null) ? $"[DebugCheats] dei {amount}x '{key}' pro inventário local." : $"[DebugCheats] addItemType('{key}', {amount}) falhou (inventário cheio?).");
			}
		}
		if (num == 0)
		{
			Plugin.Log.LogWarning("[DebugCheats] nenhum item com '" + keyword + "' na ItemsDatabase - tenta DumpItemTypes() pra ver os nomes reais e ajustar o keyword.");
		}
	}

	public static void DumpItemTypes()
	{
		ItemsDatabase instance = Singleton<ItemsDatabase>.Instance;
		if ((Object)(object)instance == (Object)null || instance.itemsDict == null)
		{
			Plugin.Log.LogWarning("[DebugCheats] ItemsDatabase indisponível.");
			return;
		}
		List<string> list = new List<string>(instance.itemsDict.Keys);
		list.Sort();
		Plugin.Log.LogInfo($"[DebugCheats] === {list.Count} itens na ItemsDatabase ===");
		for (int i = 0; i < list.Count; i += 12)
		{
			Plugin.Log.LogInfo("[ItemTypes] " + string.Join(", ", list.GetRange(i, Math.Min(12, list.Count - i)).ToArray()));
		}
		Plugin.Log.LogInfo("[DebugCheats] === fim da lista de itens ===");
	}

	public static void SpawnEnemyNearby()
	{
		SpawnEnemyByName(SpawnableEnemies[_spawnCycle++ % SpawnableEnemies.Length]);
	}

	public static void SpawnEnemyByName(string name)
	{
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			Vector3 val = ((Component)instance).transform.position + ((Component)instance).transform.forward * 400f;
			GameObject val2 = Core.AddPrefab("Characters/" + name, val, Quaternion.Euler(90f, 0f, 0f), (GameObject)null, true);
			Plugin.Log.LogInfo(((Object)(object)val2 != (Object)null) ? $"[DebugCheats] spawnou '{name}' perto do jogador (pos={val})." : ("[DebugCheats] falhou ao spawnar '" + name + "' — prefab 'Characters/" + name + "' não encontrado?"));
		}
	}

	public static void AdvanceTime(int minutes)
	{
		Controller instance = Singleton<Controller>.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			int num = instance.CurrentTime + minutes;
			while (num >= 1440)
			{
				num -= 1440;
				instance.day++;
			}
			instance.CurrentTime = num;
			instance.refreshTime(false);
			Plugin.Log.LogInfo($"[DebugCheats] relógio avançado {minutes}min → day={instance.day} time={instance.CurrentTime}.");
		}
	}

	public static void ForceNearestEvent()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		CustomEvent val = null;
		float num = float.MaxValue;
		CustomEvent[] array = Object.FindObjectsOfType<CustomEvent>();
		foreach (CustomEvent val2 in array)
		{
			if (!((Object)(object)val2 == (Object)null) && !val2.started)
			{
				float num2 = Vector3.Distance(((Component)val2).transform.position, ((Component)instance).transform.position);
				if (num2 < num)
				{
					num = num2;
					val = val2;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[DebugCheats] nenhum CustomEvent pendente (não disparado) encontrado na cena.");
			return;
		}
		Plugin.Log.LogInfo($"[DebugCheats] forçando '{((Object)((Component)val).gameObject).name}' (dist={num:F0}) a disparar.");
		val.fire(true);
	}

	public static void ForceEventByKeyword(string keyword)
	{
		CustomEvent[] array = Object.FindObjectsOfType<CustomEvent>();
		foreach (CustomEvent val in array)
		{
			if (!((Object)(object)val == (Object)null) && !val.started)
			{
				string text = ((val.theEvent != null) ? val.theEvent.Value : "");
				if ((!string.IsNullOrEmpty(text) && text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0) || ((Object)((Component)val).gameObject).name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					Plugin.Log.LogInfo("[DebugCheats] forçando evento '" + ((Object)((Component)val).gameObject).name + "' (value='" + text + "') a disparar.");
					val.fire(true);
					return;
				}
			}
		}
		Plugin.Log.LogInfo("[DebugCheats] nenhum CustomEvent pendente com '" + keyword + "' na cena atual (pode não existir neste hideout/noite, ou o nome interno é diferente do esperado).");
	}

	public static void ForceEndDream()
	{
		Dreams instance = Singleton<Dreams>.Instance;
		if ((Object)(object)instance == (Object)null || !instance.dreaming)
		{
			Plugin.Log.LogInfo("[DebugCheats] não estou sonhando agora - nada a fazer.");
			return;
		}
		Plugin.Log.LogWarning("[DebugCheats] EMERGÊNCIA: forçando saída do sonho (escolhe um desfecho automático - 'default' ou o 1º da lista do preset atual). Isso deve avisar o peer pra sair junto (ver DreamSync.AfterInitiateEndDreaming).");
		instance.initiateEndDreaming();
	}

	public static void DumpNearbyEnemyPositions()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Plugin.Log.LogInfo("========================= [EnemyPosDump] início =========================");
		int num = 0;
		Character[] array = SceneCache.Characters();
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null) && ((CharBase)val).alive)
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, ((Component)instance).transform.position);
				if (!(num2 > 2000f))
				{
					num++;
					string text = ((EnemySync.FindOwnedId(val) != null) ? "DONO(meu, real)" : ((EnemySync.FindPuppetId(val) != null) ? "PUPPET(réplica de rede)" : "NAO-RASTREADO"));
					EnemyPuppet component = ((Component)val).GetComponent<EnemyPuppet>();
					string text2 = (((Object)(object)component != (Object)null) ? ($"TEM EnemyPuppet, alvoLerp=({component.DebugTargetPos.x:F0},{component.DebugTargetPos.z:F0}) " + $"distRealVsAlvo={Vector3.Distance(((Component)val).transform.position, component.DebugTargetPos):F0}") : "sem EnemyPuppet");
					Plugin.Log.LogInfo($"[EnemyPosDump] '{((Object)((Component)val).gameObject).name}' pos=({((Component)val).transform.position.x:F0},{((Component)val).transform.position.z:F0}) " + $"distDoJogador={num2:F0} posse={text} behaviour={val.behaviour} | {text2}");
				}
			}
		}
		Plugin.Log.LogInfo($"[EnemyPosDump] {num} personagem(ns) vivo(s) dentro de 2000un do jogador. =========================");
	}

	public static void KillNearestEnemy()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Character val = null;
		float num = float.MaxValue;
		Character[] array = SceneCache.Characters();
		foreach (Character val2 in array)
		{
			if (!((Object)(object)val2 == (Object)null) && ((CharBase)val2).alive)
			{
				float num2 = Vector3.Distance(((Component)val2).transform.position, ((Component)instance).transform.position);
				if (num2 < num)
				{
					num = num2;
					val = val2;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[DebugCheats] nenhum inimigo vivo encontrado na cena (cache até 5s desatualizado).");
			return;
		}
		Plugin.Log.LogInfo($"[DebugCheats] zerando vida de '{((Object)((Component)val).gameObject).name}' (dist={num:F0}).");
		((CharBase)val).getHit(99999f, (Transform)null, false, true, true, true, false, true, false);
	}

	public static void TeleportToKeyword(string keyword)
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Player.Instance nulo - ignorado.");
			return;
		}
		Character[] array = Object.FindObjectsOfType<Character>();
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null) && ((Object)((Component)val).gameObject).name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Vector3 position = ((Component)val).transform.position;
				Plugin.Log.LogInfo($"[DebugCheats] achei Character '{((Object)((Component)val).gameObject).name}' (alive={((CharBase)val).alive}) em {position:F0} - teleportando pra lá (gerando chunk na hora).");
				TeleportAndGenerateChunk(instance, position);
				return;
			}
		}
		Location[] array2 = Object.FindObjectsOfType<Location>();
		foreach (Location val2 in array2)
		{
			if (!((Object)(object)val2 == (Object)null) && ((Object)((Component)val2).gameObject).name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Vector3 position2 = ((Component)val2).transform.position;
				Plugin.Log.LogInfo($"[DebugCheats] achei Location '{((Object)((Component)val2).gameObject).name}' em {position2:F0} - teleportando pra lá (gerando chunk na hora).");
				TeleportAndGenerateChunk(instance, position2);
				return;
			}
		}
		Plugin.Log.LogWarning("[DebugCheats] nenhum Character/Location com '" + keyword + "' no nome encontrado na cena atual (rode DumpAllCharacters/DumpAllLocations pra ver o que existe de verdade).");
	}

	private static void TeleportAndGenerateChunk(Player p, Vector3 dest)
	{
		p.teleportTo(dest, ((Component)p).transform.rotation);
		WorldGrid instance = Singleton<WorldGrid>.Instance;
		if ((Object)(object)instance != (Object)null)
		{
			instance.refreshPosition(dest, true, true);
		}
		SceneCache.ForceRescanCharacters();
	}

	public static void DumpAllCharacters()
	{
		Plugin.Log.LogInfo("========================= [CharacterDump] início =========================");
		int num = 0;
		Character[] array = Object.FindObjectsOfType<Character>(true);
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null))
			{
				num++;
				Plugin.Log.LogInfo($"[CharacterDump] '{((Object)((Component)val).gameObject).name}' alive={((CharBase)val).alive} activeInHierarchy={((Component)val).gameObject.activeInHierarchy} " + $"pos=({((Component)val).transform.position.x:F0},{((Component)val).transform.position.z:F0})");
			}
		}
		Plugin.Log.LogInfo($"[CharacterDump] {num} Character(s) na cena atual. =========================");
	}

	public static void DumpAllLocations()
	{
		Plugin.Log.LogInfo("========================= [LocationDump] início =========================");
		int num = 0;
		Location[] array = Object.FindObjectsOfType<Location>();
		foreach (Location val in array)
		{
			if (!((Object)(object)val == (Object)null))
			{
				num++;
				Plugin.Log.LogInfo($"[LocationDump] '{((Object)((Component)val).gameObject).name}' pos=({((Component)val).transform.position.x:F0},{((Component)val).transform.position.z:F0})");
			}
		}
		Plugin.Log.LogInfo($"[LocationDump] {num} Location(s) na cena atual. =========================");
	}

	public static void UnlockNearestLock()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Locked val = null;
		float num = float.MaxValue;
		Locked[] array = SceneCache.Locks();
		foreach (Locked val2 in array)
		{
			if (!((Object)(object)val2 == (Object)null) && val2.locked)
			{
				float num2 = Vector3.Distance(((Component)val2).transform.position, ((Component)instance).transform.position);
				if (num2 < num)
				{
					num = num2;
					val = val2;
				}
			}
		}
		Padlock val3 = null;
		float num3 = float.MaxValue;
		Padlock[] array2 = SceneCache.Padlocks();
		foreach (Padlock val4 in array2)
		{
			if (!((Object)(object)val4 == (Object)null) && val4.locked)
			{
				float num4 = Vector3.Distance(((Component)val4).transform.position, ((Component)instance).transform.position);
				if (num4 < num3)
				{
					num3 = num4;
					val3 = val4;
				}
			}
		}
		if ((Object)(object)val != (Object)null && ((Object)(object)val3 == (Object)null || num <= num3))
		{
			Plugin.Log.LogInfo($"[DebugCheats] destrancando '{((Object)((Component)val).gameObject).name}' (dist={num:F0}) - LockSync avisa o peer sozinho no próximo poll.");
			val.unlock();
		}
		else if ((Object)(object)val3 != (Object)null)
		{
			Plugin.Log.LogInfo($"[DebugCheats] destrancando cadeado '{((Object)((Component)val3).gameObject).name}' (dist={num3:F0}) - LockSync avisa o peer sozinho no próximo poll.");
			val3.unlock(false);
		}
		else
		{
			Plugin.Log.LogInfo("[DebugCheats] nenhuma fechadura TRANCADA encontrada na cena atual (SceneCache pode levar até 5s pra reconhecer algo novo).");
		}
	}

	public static void ToggleNoclip()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Player.Instance nulo - ignorado.");
			return;
		}
		_noclip = !_noclip;
		instance.modifyCollider(!_noclip);
		if ((Object)(object)instance.Rigidbody != (Object)null)
		{
			instance.Rigidbody.useGravity = !_noclip;
		}
		Plugin.Log.LogInfo($"[DebugCheats] noclip = {_noclip} (collider enabled={!_noclip}, gravity enabled={!_noclip}). " + "Ande até um chão sólido e desligue de novo.");
	}

	public static void DumpDreamPresets()
	{
		Dreams instance = Singleton<Dreams>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Singleton<Dreams>.Instance nulo - ignorado.");
			return;
		}
		Dictionary<string, DreamPreset> value = Traverse.Create((object)instance).Field("allPresetDict").GetValue<Dictionary<string, DreamPreset>>();
		if (value == null)
		{
			Plugin.Log.LogWarning("[DebugCheats] allPresetDict nulo/inacessível.");
			return;
		}
		Plugin.Log.LogInfo($"========================= [DreamPresetDump] {value.Count} preset(s) =========================");
		foreach (KeyValuePair<string, DreamPreset> item in value)
		{
			Plugin.Log.LogInfo("[DreamPresetDump] chave='" + item.Key + "' preset.name='" + (((Object)(object)item.Value != (Object)null) ? ((Object)item.Value).name : "NULL") + "'");
		}
		Plugin.Log.LogInfo("[DreamPresetDump] fim =========================");
	}

	public static void DumpOutsideLocationsState()
	{
		OutsideLocations instance = Singleton<OutsideLocations>.Instance;
		Dreams instance2 = Singleton<Dreams>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Singleton<OutsideLocations>.Instance nulo - ignorado.");
			return;
		}
		Plugin.Log.LogInfo("========================= [OutsideLocationsDump] =========================");
		Plugin.Log.LogInfo($"[OutsideLocationsDump] loading={instance.loading} spawningLocations={instance.spawningLocations} " + $"playerInOutsideLocation={instance.playerInOutsideLocation}");
		if ((Object)(object)instance2 != (Object)null)
		{
			Plugin.Log.LogInfo($"[OutsideLocationsDump] Dreams.dreaming={instance2.dreaming} dreamPrepared={instance2.dreamPrepared} " + "dreamLocation=" + (((Object)(object)instance2.dreamLocation != (Object)null) ? ((Object)((Component)instance2.dreamLocation).gameObject).name : "NULL"));
		}
		Plugin.Log.LogInfo($"[OutsideLocationsDump] {instance.spawnedLocations.Count} location(s) já em spawnedLocations:");
		foreach (KeyValuePair<string, Location> spawnedLocation in instance.spawnedLocations)
		{
			Plugin.Log.LogInfo("[OutsideLocationsDump]   '" + spawnedLocation.Key + "' -> " + (((Object)(object)spawnedLocation.Value != (Object)null) ? ((Object)((Component)spawnedLocation.Value).gameObject).name : "NULL"));
		}
		Plugin.Log.LogInfo("[OutsideLocationsDump] fim =========================");
	}

	public static void ResetTrainWreckDreamState()
	{
		OutsideLocations instance = Singleton<OutsideLocations>.Instance;
		Dreams instance2 = Singleton<Dreams>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Singleton<OutsideLocations>.Instance nulo - ignorado.");
			return;
		}
		string[] trainWreckDreamKeys = TrainWreckDreamKeys;
		foreach (string text in trainWreckDreamKeys)
		{
			bool flag = instance.spawnedLocations.Remove(text);
			Plugin.Log.LogInfo($"[DebugCheats] spawnedLocations['{text}'] removido={flag}.");
		}
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Location> spawnedLocation in instance.spawnedLocations)
		{
			if (spawnedLocation.Key != null && spawnedLocation.Key.StartsWith("dream_doctor"))
			{
				list.Add(spawnedLocation.Key);
			}
		}
		foreach (string item in list)
		{
			instance.spawnedLocations.Remove(item);
			Plugin.Log.LogInfo("[DebugCheats] spawnedLocations['" + item + "'] removido=True (casou prefixo 'dream_doctor').");
		}
		if ((Object)(object)instance2 != (Object)null)
		{
			Plugin.Log.LogInfo($"[DebugCheats] Dreams antes: dreaming={instance2.dreaming} dreamPrepared={instance2.dreamPrepared}");
			if (!instance2.dreaming)
			{
				instance2.dreamPrepared = false;
			}
			Plugin.Log.LogInfo($"[DebugCheats] Dreams depois: dreaming={instance2.dreaming} dreamPrepared={instance2.dreamPrepared}");
		}
	}

	public static void TalkToKeyword(string keyword)
	{
		try
		{
			TalkToKeywordInner(keyword);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("[DebugCheats] TalkToKeyword EXPLODIU: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	private static void TalkToKeywordInner(string keyword)
	{
		NPC[] array = Object.FindObjectsOfType<NPC>(true);
		foreach (NPC val in array)
		{
			if (!((Object)(object)val == (Object)null) && (((Object)((Component)val).gameObject).name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 || (val.name != null && val.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)))
			{
				Plugin.Log.LogInfo("[DebugCheats] chamando talkTo() em '" + ((Object)((Component)val).gameObject).name + "' (NPC.name='" + val.name + "', " + $"activeInHierarchy={((Component)val).gameObject.activeInHierarchy}, wantsToTalk={val.wantsToTalk}, " + "characterDialogue=" + (((Object)(object)val.characterDialogue != (Object)null) ? ((Object)val.characterDialogue).name : "NULL") + ") direto (sem precisar do cursor).");
				val.talkTo();
				return;
			}
		}
		Character[] array2 = Object.FindObjectsOfType<Character>(true);
		foreach (Character val2 in array2)
		{
			if (!((Object)(object)val2 == (Object)null) && ((Object)((Component)val2).gameObject).name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
			{
				NPC component = ((Component)val2).GetComponent<NPC>();
				Plugin.Log.LogWarning($"[DebugCheats] achei Character '{((Object)((Component)val2).gameObject).name}' (alive={((CharBase)val2).alive}, " + $"activeInHierarchy={((Component)val2).gameObject.activeInHierarchy}) mas NPC component = " + (((Object)(object)component != (Object)null) ? "existe (não bateu no 1º loop?!)" : "NULO - este Character não tem NPC."));
				if ((Object)(object)component != (Object)null)
				{
					component.talkTo();
					return;
				}
			}
		}
		Plugin.Log.LogWarning("[DebugCheats] nenhum NPC nem Character com '" + keyword + "' no nome encontrado na cena atual (rode DumpAllCharacters pra ver todos os Character existentes agora).");
	}

	public static void ForceDoctorDialogue()
	{
		try
		{
			ForceDoctorDialogueInner();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("[DebugCheats] ForceDoctorDialogue EXPLODIU: " + ex.GetType().Name + ": " + ex.Message + "\n" + ex.StackTrace);
		}
	}

	private static void ForceDoctorDialogueInner()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogWarning("[DebugCheats] Player.Instance nulo - ignorado.");
			return;
		}
		NPC[] array = Object.FindObjectsOfType<NPC>(true);
		foreach (NPC val in array)
		{
			if (!((Object)(object)val == (Object)null) && ((Object)((Component)val).gameObject).name.IndexOf("doctor", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				Plugin.Log.LogInfo("[DebugCheats] Doutor já existe na cena ('" + ((Object)((Component)val).gameObject).name + "') - chamando talkTo() nele.");
				val.talkTo();
				return;
			}
		}
		string[] obj = new string[4] { "NPC/Doctor_trapSet", "NPC/Doctor_idle", "NPC/Doctor_confronted", "NPC/Doctor_follower" };
		Vector3 val2 = ((Component)instance).transform.position + ((Component)instance).transform.forward * 300f;
		string[] array2 = obj;
		foreach (string text in array2)
		{
			Plugin.Log.LogInfo("[DebugCheats] Doutor não existe mais na cena - tentando spawnar '" + text + "'...");
			GameObject val3 = null;
			try
			{
				val3 = Core.AddPrefab(text, val2, Quaternion.Euler(90f, 0f, 0f), (GameObject)null, true);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[DebugCheats] '" + text + "' falhou: " + ex.GetType().Name + ": " + ex.Message);
				continue;
			}
			if ((Object)(object)val3 == (Object)null)
			{
				Plugin.Log.LogWarning("[DebugCheats] '" + text + "' retornou null - tentando próximo.");
				continue;
			}
			NPC component = val3.GetComponent<NPC>();
			if ((Object)(object)component == (Object)null)
			{
				Plugin.Log.LogWarning("[DebugCheats] '" + text + "' spawnou mas sem componente NPC - tentando próximo.");
				continue;
			}
			Plugin.Log.LogInfo($"[DebugCheats] spawnou '{text}' (pos={val2:F0}) - chamando talkTo() nele.");
			component.talkTo();
			return;
		}
		Plugin.Log.LogError("[DebugCheats] NENHUMA variante de prefab do Doutor funcionou.");
		Plugin.Log.LogInfo("[DebugCheats] talkTo() retornou sem exceção.");
	}
}
