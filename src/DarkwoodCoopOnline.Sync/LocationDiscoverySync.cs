using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class LocationDiscoverySync
{
	private static bool _patched;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Location), "discoverMe", new Type[3]
			{
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(LocationDiscoverySync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(Location __instance)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null))
		{
			string trueLocationName = Core.getTrueLocationName(((Object)((Component)__instance).gameObject).name);
			if (!string.IsNullOrEmpty(trueLocationName))
			{
				Plugin.Log.LogInfo("[LocationDiscoverySync] local '" + trueLocationName + "' descoberto aqui - anunciando pro peer.");
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.LocationDiscovered).Str(trueLocationName).Finish());
			}
		}
	}

	public static void ApplyDiscovered(PacketReader r)
	{
		string text = r.Str();
		if (!string.IsNullOrEmpty(text))
		{
			Location val = (((Object)(object)Singleton<WorldGenerator>.Instance != (Object)null) ? Singleton<WorldGenerator>.Instance.getLocation(text) : null);
			if ((Object)(object)val == (Object)null)
			{
				Plugin.Log.LogWarning("[LocationDiscoverySync] peer descobriu '" + text + "' mas não achei esse Location aqui.");
				return;
			}
			Plugin.Log.LogInfo("[LocationDiscoverySync] peer descobriu '" + text + "' - replicando diário+mapa aqui.");
			_applyingRemote = true;
			MapMarkerSync.SuppressAnnounce = true;
			val.discoverMe(true, false, false);
			MapMarkerSync.SuppressAnnounce = false;
			_applyingRemote = false;
		}
	}

	public static void Reset()
	{
		_applyingRemote = false;
	}
}
