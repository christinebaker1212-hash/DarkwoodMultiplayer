using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class WorldEventSync
{
	private static bool _patched;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Events), "fireWorldEvent", new Type[1] { typeof(string) }, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(WorldEventSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(string type)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo("[WorldEventSync] '" + type + "' disparado aqui - anunciando pro peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.WorldEventFired).Str(type).Finish());
		}
	}

	public static void ApplyFired(PacketReader r)
	{
		string text = r.Str();
		Plugin.Log.LogInfo("[WorldEventSync] peer disparou '" + text + "' - replicando aqui.");
		_applyingRemote = true;
		Singleton<Events>.Instance.fireWorldEvent(text);
		_applyingRemote = false;
	}
}
