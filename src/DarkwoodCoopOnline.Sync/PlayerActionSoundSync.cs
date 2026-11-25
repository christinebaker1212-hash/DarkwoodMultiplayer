using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PlayerActionSoundSync
{
	private static bool _patched;

	private static bool _applyingRemote;

	private const float PositionalMinDistance = 1f;

	private const float PositionalMaxDistance = 500f;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_004f: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_00a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b1: Expected O, but got Unknown
		//IL_00ec: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(AudioController), "Play", new Type[1] { typeof(string) }, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PlayerActionSoundSync), "AfterPlay", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(AudioController), "Play", new Type[2]
			{
				typeof(string),
				typeof(Transform)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PlayerActionSoundSync), "AfterPlay", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(AudioObject), "Stop", new Type[1] { typeof(float) }, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PlayerActionSoundSync), "AfterStop", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool IsActionSound(string audioID)
	{
		if (string.IsNullOrEmpty(audioID))
		{
			return false;
		}
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null || InvItemClass.isNull(instance.currentItem))
		{
			return false;
		}
		InvItem baseClass = instance.currentItem.baseClass;
		if ((Object)(object)baseClass == (Object)null)
		{
			return false;
		}
		if (!(audioID == baseClass.emptyClipSound) && !(audioID == baseClass.attackSound) && !(audioID == baseClass.attack2Sound) && !(audioID == baseClass.reloadSound) && !(audioID == baseClass.aimSound) && !(audioID == baseClass.aimReturnSound) && !(audioID == baseClass.activateSound) && !(audioID == baseClass.deactivateSound) && !(audioID == baseClass.hideSound))
		{
			return audioID == baseClass.getSound;
		}
		return true;
	}

	private static void AfterPlay(string audioID)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive && IsActionSound(audioID))
		{
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.PlayerActionSound).Str(audioID).Finish());
		}
	}

	private static void AfterStop(AudioObject __instance, float fadeOutLength)
	{
		if (!_applyingRemote && CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null))
		{
			string audioID = __instance.audioID;
			if (IsActionSound(audioID))
			{
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.PlayerActionSoundStop).Str(audioID).Float(fadeOutLength)
					.Finish());
			}
		}
	}

	public static void ApplyIncoming(PacketReader r)
	{
		string text = r.Str();
		if (!string.IsNullOrEmpty(text))
		{
			RemotePlayerPuppet remotePlayerPuppet = ((CoopRuntime.Session != null) ? CoopRuntime.Session.Puppet : null);
			_applyingRemote = true;
			AudioObject val = (((Object)(object)remotePlayerPuppet != (Object)null) ? AudioController.Play(text, ((Component)remotePlayerPuppet).transform) : AudioController.Play(text));
			if ((Object)(object)val != (Object)null && (Object)(object)remotePlayerPuppet != (Object)null && (Object)(object)val.primaryAudioSource != (Object)null)
			{
				val.primaryAudioSource.spatialBlend = 1f;
				val.primaryAudioSource.minDistance = 1f;
				val.primaryAudioSource.maxDistance = 500f;
			}
			_applyingRemote = false;
		}
	}

	public static void ApplyIncomingStop(PacketReader r)
	{
		string text = r.Str();
		float num = r.Float();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		_applyingRemote = true;
		List<AudioObject> playingAudioObjects = AudioController.GetPlayingAudioObjects(text, false);
		if (playingAudioObjects != null)
		{
			foreach (AudioObject item in playingAudioObjects)
			{
				if (item != null)
				{
					item.Stop(num);
				}
			}
		}
		_applyingRemote = false;
	}
}
