using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class MapMarkerSync
{
	private static bool _patched;

	private static bool _applyingRemote;

	public static bool SuppressAnnounce;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Map), "showElement", new Type[1] { typeof(MapElement) }, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MapMarkerSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(MapElement element)
	{
		if (!_applyingRemote && !SuppressAnnounce && CoopRuntime.SessionActive && !((Object)(object)element == (Object)null) && !string.IsNullOrEmpty(element.elementName))
		{
			Plugin.Log.LogInfo("[MapMarkerSync] marcador '" + element.elementName + "' revelado aqui - anunciando pro peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.MapMarkerShown).Str(element.elementName).Finish());
		}
	}

	public static void ApplyShown(PacketReader r)
	{
		string text = r.Str();
		if (!string.IsNullOrEmpty(text))
		{
			MapElement element = Singleton<Map>.Instance.getElement(text);
			if ((Object)(object)element == (Object)null)
			{
				Plugin.Log.LogWarning("[MapMarkerSync] peer revelou marcador '" + text + "' mas não achei esse elemento no MEU mapa (área ainda não carregada aqui?).");
				return;
			}
			Plugin.Log.LogInfo("[MapMarkerSync] peer revelou marcador '" + text + "' - replicando aqui.");
			_applyingRemote = true;
			Singleton<Map>.Instance.showElement(element);
			_applyingRemote = false;
		}
	}
}
