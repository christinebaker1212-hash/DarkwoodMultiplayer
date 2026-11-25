using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class FlagSync
{
	private static bool _patched;

	private static bool _applyingRemote;

	private static readonly HashSet<string> _localOnlyFlags = new HashSet<string> { "player_inFirstHideout", "player_inSecondHideout", "player_inThirdHideout" };

	private static readonly Dictionary<string, bool> _knownBool = new Dictionary<string, bool>();

	private static readonly Dictionary<string, int> _knownInt = new Dictionary<string, int>();

	private const float DumpRadius = 2500f;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0069: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00be: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Flags), "setFlag", new Type[2]
			{
				typeof(string),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(FlagSync), "AfterSetBool", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Flags), "setFlag", new Type[2]
			{
				typeof(string),
				typeof(int)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(FlagSync), "AfterSetInt", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterSetBool(string flagName, bool activeModifier)
	{
		if (!_applyingRemote && !string.IsNullOrEmpty(flagName) && CoopRuntime.SessionActive && !_localOnlyFlags.Contains(flagName))
		{
			bool flag = Singleton<Flags>.Instance.isFlagTrue(flagName, 0, (EqualsEnum)30);
			if (!_knownBool.TryGetValue(flagName, out var value) || value != flag)
			{
				_knownBool[flagName] = flag;
				SceneCache.ForceRescanCharacters();
				DumpNearbyIfQuestCharacterFlag(flagName);
				Plugin.Log.LogInfo($"[FlagSync] flag '{flagName}' = {flag} - avisando o peer.");
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.FlagBoolChanged).Str(flagName).Bool(flag)
					.Finish());
			}
		}
	}

	private static void AfterSetInt(string flagName, int amount)
	{
		if (!_applyingRemote && !string.IsNullOrEmpty(flagName) && CoopRuntime.SessionActive)
		{
			int flagInt = Singleton<Flags>.Instance.getFlagInt(flagName);
			if (!_knownInt.TryGetValue(flagName, out var value) || value != flagInt)
			{
				_knownInt[flagName] = flagInt;
				Plugin.Log.LogInfo($"[FlagSync] flag '{flagName}' (número) = {flagInt} - avisando o peer.");
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.FlagIntChanged).Str(flagName).Int(flagInt)
					.Finish());
			}
		}
	}

	public static void ApplyBoolChanged(PacketReader r)
	{
		string text = r.Str();
		bool flag = r.Bool();
		if (!string.IsNullOrEmpty(text) && !_localOnlyFlags.Contains(text))
		{
			_knownBool[text] = flag;
			_applyingRemote = true;
			Singleton<Flags>.Instance.setFlag(text, flag);
			_applyingRemote = false;
			SceneCache.ForceRescanCharacters();
			DumpNearbyIfQuestCharacterFlag(text);
			Plugin.Log.LogInfo($"[FlagSync] aplicando flag '{text}' = {flag} recebida do peer.");
		}
	}

	public static void ApplyIntChanged(PacketReader r)
	{
		string text = r.Str();
		int num = r.Int();
		if (!string.IsNullOrEmpty(text))
		{
			_knownInt[text] = num;
			_applyingRemote = true;
			Singleton<Flags>.Instance.setFlag(text, num);
			_applyingRemote = false;
			Plugin.Log.LogInfo($"[FlagSync] aplicando flag '{text}' (número) = {num} recebida do peer.");
		}
	}

	private static void DumpNearbyIfQuestCharacterFlag(string flagName)
	{
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_004a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0085: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0107: Unknown result type (might be due to invalid IL or missing references)
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_015f: Unknown result type (might be due to invalid IL or missing references)
		//IL_019a: Unknown result type (might be due to invalid IL or missing references)
		if (string.IsNullOrEmpty(flagName) || flagName.IndexOf("groom", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return;
		}
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Vector3 position = ((Component)instance).transform.position;
		Plugin.Log.LogInfo($"[FlagSync][GroomDiag] flag '{flagName}' - despejando Character/NPC num raio de {2500f:F0} da minha posição {position:F0}.");
		int num = 0;
		Character[] array = Object.FindObjectsOfType<Character>(true);
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null) && !(Vector3.Distance(((Component)val).transform.position, position) > 2500f))
			{
				Plugin.Log.LogInfo($"[FlagSync][GroomDiag] Character '{((Object)((Component)val).gameObject).name}' type={((CharBase)val).type} " + $"alive={((CharBase)val).alive} health={((CharBase)val).health} active={((Component)val).gameObject.activeSelf} pos={((Component)val).transform.position:F0}");
				num++;
			}
		}
		NPC[] array2 = Object.FindObjectsOfType<NPC>(true);
		foreach (NPC val2 in array2)
		{
			if (!((Object)(object)val2 == (Object)null) && !(Vector3.Distance(((Component)val2).transform.position, position) > 2500f))
			{
				Plugin.Log.LogInfo($"[FlagSync][GroomDiag] NPC '{((Object)((Component)val2).gameObject).name}' active={((Component)val2).gameObject.activeSelf} pos={((Component)val2).transform.position:F0}");
				num++;
			}
		}
		Plugin.Log.LogInfo($"[FlagSync][GroomDiag] fim do despejo - {num} objeto(s) encontrado(s).");
	}

	public static void Reset()
	{
		_knownBool.Clear();
		_knownInt.Clear();
	}
}
