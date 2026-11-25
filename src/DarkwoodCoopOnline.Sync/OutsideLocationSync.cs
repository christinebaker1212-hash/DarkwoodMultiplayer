using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class OutsideLocationSync
{
	private static bool _patched;

	private static bool _applyingRemote;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(OutsideLocations), "prepareLocation", new Type[2]
			{
				typeof(string),
				typeof(Transform)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(OutsideLocationSync), "AfterPrepareLocation", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(OutsideLocations), "returnToWorld", new Type[2]
			{
				typeof(bool),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(OutsideLocationSync), "AfterReturnToWorld", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterPrepareLocation(string locationName, Transform _objectThatTransportedMe)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive && (!((Object)(object)Singleton<Dreams>.Instance != (Object)null) || !Singleton<Dreams>.Instance.dreamPrepared) && !string.IsNullOrEmpty(locationName))
		{
			Plugin.Log.LogInfo("[OutsideLocationSync] entrei em Outside Location '" + locationName + "' - avisando o peer pra vir junto.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.OutsideLocationEntered).Str(locationName).Finish());
		}
	}

	public static void ApplyEntered(PacketReader r)
	{
		string text = r.Str();
		if (!string.IsNullOrEmpty(text))
		{
			Plugin.Log.LogInfo("[OutsideLocationSync] peer entrou em '" + text + "' - vindo junto.");
			_applyingRemote = true;
			Singleton<OutsideLocations>.Instance.prepareLocation(text, (Transform)null);
			_applyingRemote = false;
		}
	}

	private static void AfterReturnToWorld(bool teleportToPositionCopy, bool tweenScreens)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo("[OutsideLocationSync] voltei pro mundo normal - avisando o peer pra voltar junto.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.OutsideLocationReturned).Bool(teleportToPositionCopy).Bool(tweenScreens)
				.Finish());
		}
	}

	public static void ApplyReturned(PacketReader r)
	{
		bool flag = r.Bool();
		bool flag2 = r.Bool();
		Plugin.Log.LogInfo("[OutsideLocationSync] peer voltou pro mundo normal - voltando junto.");
		_applyingRemote = true;
		Singleton<OutsideLocations>.Instance.returnToWorld(flag, flag2);
		_applyingRemote = false;
	}
}
