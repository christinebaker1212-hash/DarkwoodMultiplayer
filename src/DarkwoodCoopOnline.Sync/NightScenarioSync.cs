using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class NightScenarioSync
{
	private static bool _patched;

	private static string _lastAnnounced;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(NightScenarios), "setCurrentScenario", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(NightScenarioSync), "AfterSetCurrentScenario", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(NightScenario), "checkFrequencies", (Type[])null, (Type[])null), new HarmonyMethod(typeof(NightScenarioSync), "BeforeCheckFrequencies", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterSetCurrentScenario()
	{
		if (!_applyingRemote && CoopRuntime.SessionActive && CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
		{
			NightScenario currentScenario = Singleton<NightScenarios>.Instance.currentScenario;
			string text = (((Object)(object)currentScenario != (Object)null) ? ((Object)((Component)currentScenario).gameObject).name : "");
			if (!(text == _lastAnnounced))
			{
				_lastAnnounced = text;
				Plugin.Log.LogInfo("[NightScenarioSync] cenário escolhido: '" + text + "' - avisando o peer.");
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.NightScenarioSet).Str(text).Finish());
			}
		}
	}

	public static void ApplyScenario(PacketReader r)
	{
		string text = r.Str();
		NightScenarios instance = Singleton<NightScenarios>.Instance;
		if ((Object)(object)instance == (Object)null || ((Object)(object)instance.currentScenario != (Object)null && ((Object)((Component)instance.currentScenario).gameObject).name == text))
		{
			return;
		}
		NightScenario val = null;
		foreach (NightScenario scenario in instance.scenarios)
		{
			if ((Object)(object)scenario != (Object)null && ((Object)((Component)scenario).gameObject).name == text)
			{
				val = scenario;
				break;
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogWarning("[NightScenarioSync] host escolheu '" + text + "' mas não achei esse cenário na minha lista local.");
			return;
		}
		_applyingRemote = true;
		instance.currentScenario = val;
		_applyingRemote = false;
		Plugin.Log.LogInfo("[NightScenarioSync] forçando o MESMO cenário do host: '" + text + "'.");
	}

	private static bool BeforeCheckFrequencies()
	{
		if (!CoopRuntime.SessionActive)
		{
			return true;
		}
		if (CoopRuntime.Session != null)
		{
			return CoopRuntime.Session.Role == NetRole.Host;
		}
		return false;
	}

	public static void Reset()
	{
		_lastAnnounced = null;
	}
}
