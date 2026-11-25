using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class CharacterSaveSync
{
	private class CharacterBlob
	{
		public Player.SaveState player;

		public Inventory.SaveState inv;

		public Inventory.SaveState hotbar;
	}

	private static bool _guardingUpgrades;

	private static int _targetHealthUpgrades;

	private static int _targetStaminaUpgrades;

	private static int _targetHotbarUpgrades;

	private static int _targetInventoryUpgrades;

	private const float UploadInterval = 20f;

	private static float _uploadTimer;

	private static Dictionary<string, string> _records;

	private static readonly (string type, int amount)[] StarterKit = new(string, int)[5]
	{
		("lantern", 1),
		("wood", 5),
		("nail", 10),
		("torch", 1),
		("bandages", 1)
	};

	private static string FilePath => Path.Combine(Paths.PluginPath, "DarkwoodCoopOnline_CharacterSaves.json");

	public static void EnsurePatched(string modId)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_003b: Expected O, but got Unknown
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Expected O, but got Unknown
		//IL_0072: Unknown result type (might be due to invalid IL or missing references)
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a7: Expected O, but got Unknown
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Expected O, but got Unknown
		Harmony val = new Harmony(modId);
		val.Patch((MethodBase)AccessTools.Method(typeof(Player), "upgradeHealth", (Type[])null, (Type[])null), new HarmonyMethod(typeof(CharacterSaveSync), "HealthUpgradePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		val.Patch((MethodBase)AccessTools.Method(typeof(Player), "upgradeStamina", (Type[])null, (Type[])null), new HarmonyMethod(typeof(CharacterSaveSync), "StaminaUpgradePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		val.Patch((MethodBase)AccessTools.Method(typeof(Player), "upgradeHotbar", (Type[])null, (Type[])null), new HarmonyMethod(typeof(CharacterSaveSync), "HotbarUpgradePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		val.Patch((MethodBase)AccessTools.Method(typeof(Player), "upgradeInventory", (Type[])null, (Type[])null), new HarmonyMethod(typeof(CharacterSaveSync), "InventoryUpgradePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
	}

	private static bool HealthUpgradePrefix(Player __instance)
	{
		if (_guardingUpgrades)
		{
			return __instance.healthUpgrades < _targetHealthUpgrades;
		}
		return true;
	}

	private static bool StaminaUpgradePrefix(Player __instance)
	{
		if (_guardingUpgrades)
		{
			return __instance.staminaUpgrades < _targetStaminaUpgrades;
		}
		return true;
	}

	private static bool HotbarUpgradePrefix(Player __instance)
	{
		if (_guardingUpgrades)
		{
			return __instance.hotbarUpgrades < _targetHotbarUpgrades;
		}
		return true;
	}

	private static bool InventoryUpgradePrefix(Player __instance)
	{
		if (_guardingUpgrades)
		{
			return __instance.inventoryUpgrades < _targetInventoryUpgrades;
		}
		return true;
	}

	private static void EnsureLoaded()
	{
		if (_records != null)
		{
			return;
		}
		try
		{
			if (File.Exists(FilePath))
			{
				_records = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? new Dictionary<string, string>();
				Plugin.Log.LogInfo($"[CharSave] {_records.Count} personagem(ns) salvos carregados de disco.");
				return;
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[CharSave] falha lendo " + FilePath + ": " + ex.Message + " — começando vazio.");
		}
		_records = new Dictionary<string, string>();
	}

	private static void Persist()
	{
		try
		{
			File.WriteAllText(FilePath, JsonConvert.SerializeObject((object)_records));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[CharSave] falha salvando " + FilePath + ": " + ex.Message);
		}
	}

	public static byte[] BuildDownloadFor(string clientIdentity)
	{
		EnsureLoaded();
		_records.TryGetValue(clientIdentity, out var value);
		return PacketWriter.Begin(PacketType.CharacterSaveDownload).Str(value ?? "").Finish();
	}

	public static void ApplyDownload(PacketReader r)
	{
		string text = r.Str();
		if ((Object)(object)Player.Instance == (Object)null)
		{
			Plugin.Log.LogWarning("[CharSave] recebi retrato mas Player.Instance é null — ignorando.");
			return;
		}
		if (string.IsNullOrEmpty(text))
		{
			GrantFreshCharacter();
			return;
		}
		try
		{
			CharacterBlob characterBlob = JsonConvert.DeserializeObject<CharacterBlob>(text);
			if (characterBlob.player != null)
			{
				_targetHealthUpgrades = characterBlob.player.healthUpgrades;
				_targetStaminaUpgrades = characterBlob.player.staminaUpgrades;
				_targetHotbarUpgrades = characterBlob.player.hotbarUpgrades;
				_targetInventoryUpgrades = characterBlob.player.inventoryUpgrades;
				_guardingUpgrades = true;
			}
			try
			{
				Player.SaveState player = characterBlob.player;
				if (player != null)
				{
					player.loadValues(true);
				}
			}
			finally
			{
				_guardingUpgrades = false;
			}
			Inventory.SaveState inv = characterBlob.inv;
			if (inv != null)
			{
				inv.loadValues(Player.Instance.Inventory);
			}
			Inventory.SaveState hotbar = characterBlob.hotbar;
			if (hotbar != null)
			{
				hotbar.loadValues(Player.Instance.Hotbar);
			}
			ReactivateHotbarItems();
			Plugin.Log.LogInfo("[CharSave] personagem restaurado (inventário/hotbar/HP/skills).");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("[CharSave] falha aplicando retrato recebido: " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	private static void GrantFreshCharacter()
	{
		Player instance = Player.Instance;
		Plugin.Log.LogInfo("[CharSave] primeira vez neste host — começando com kit básico (limpando itens do host).");
		try
		{
			instance.Inventory.clear();
			instance.Hotbar.clear();
			instance.setHealth(((CharBase)instance).maxHealth);
			(string, int)[] starterKit = StarterKit;
			for (int i = 0; i < starterKit.Length; i++)
			{
				var (text, num) = starterKit[i];
				try
				{
					if (instance.Inventory.addItemTypeToPlayer(text, num, true) == null)
					{
						Plugin.Log.LogWarning("[CharSave] kit: item '" + text + "' não adicionou (nome inexistente na ItemsDatabase?).");
					}
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning("[CharSave] kit: falha adicionando '" + text + "': " + ex.GetType().Name + " — pulando.");
				}
			}
			ReactivateHotbarItems();
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError("[CharSave] falha montando personagem novo: " + ex2.GetType().Name + ": " + ex2.Message);
		}
	}

	private static void ReactivateHotbarItems()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		try
		{
			instance.Hotbar.checkForActiveSwitches(true);
			instance.Inventory.checkForActiveSwitches(true);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[CharSave] falha reativando itens do hotbar (ex.: lanterna): " + ex.GetType().Name + ": " + ex.Message);
		}
	}

	public static byte[] BuildUpload()
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Expected O, but got Unknown
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Expected O, but got Unknown
		//IL_0045: Unknown result type (might be due to invalid IL or missing references)
		//IL_004f: Expected O, but got Unknown
		if ((Object)(object)Player.Instance == (Object)null)
		{
			return null;
		}
		try
		{
			string v = JsonConvert.SerializeObject((object)new CharacterBlob
			{
				player = new Player.SaveState(Player.Instance),
				inv = new Inventory.SaveState(Player.Instance.Inventory),
				hotbar = new Inventory.SaveState(Player.Instance.Hotbar)
			});
			return PacketWriter.Begin(PacketType.CharacterSaveUpload).Str(v).Finish();
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[CharSave] falha montando retrato local: " + ex.GetType().Name + ": " + ex.Message);
			return null;
		}
	}

	public static void ApplyUpload(PacketReader r, string clientIdentity)
	{
		EnsureLoaded();
		string value = r.Str();
		if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(clientIdentity))
		{
			_records[clientIdentity] = value;
			Persist();
		}
	}

	public static byte[] PollUpload(float dt, bool amClient)
	{
		if (!amClient)
		{
			return null;
		}
		_uploadTimer += dt;
		if (_uploadTimer < 20f)
		{
			return null;
		}
		_uploadTimer = 0f;
		return BuildUpload();
	}

	public static void Reset()
	{
		_uploadTimer = 0f;
	}

	public static void ResetAllRecords()
	{
		EnsureLoaded();
		if (_records.Count != 0)
		{
			Plugin.Log.LogInfo($"[CharSave] mundo novo detectado - descartando {_records.Count} personagem(ns) salvos de sessões anteriores.");
			_records.Clear();
			Persist();
		}
	}
}
