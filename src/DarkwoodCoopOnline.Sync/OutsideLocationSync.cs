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

	// Following someone into / out of an area: who, since when. If the game's own move didn't put us
	// next to them (the "black limbo": world hidden, player left where they were), we go to them.
	private static int _followSlot = -1;

	private static float _followSince;

	private static bool _followRescued;

	// where they were when they left: we act once they've really arrived somewhere else
	private static Vector3 _followFrom;

	private const float RescueAfter = 2.5f;

	private const float GiveUpAfter = 25f;

	private const float FarAway = 1500f;

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
			// the door / hole used: the others' games need it to put them inside (null left them in limbo)
			string v = "";
			Vector3 p = Vector3.zero;
			if ((Object)(object)_objectThatTransportedMe != (Object)null)
			{
				v = ((Object)_objectThatTransportedMe).name;
				p = _objectThatTransportedMe.position;
			}
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.OutsideLocationEntered).Str(locationName).Str(v)
				.Vec3(p)
				.Finish());
		}
	}

	public static void ApplyEntered(PacketReader r)
	{
		string text = r.Str();
		string doorName = r.Str();
		Vector3 pos = r.Vec3();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		OutsideLocations instance = Singleton<OutsideLocations>.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Transform transform = null;
		if (!string.IsNullOrEmpty(doorName))
		{
			transform = SceneCache.FindTransform((Transform t) => ((Object)t).name == doorName, pos, 300f);
		}
		Plugin.Log.LogInfo($"[Session] '{CoopPeers.NameOf(CoopRuntime.Sender)}' went into '{text}' (through '{doorName}', {(((Object)(object)transform != (Object)null) ? "found here" : "not found here")}) - following.");
		StartFollowing();
		if (instance.playerInOutsideLocation && (Object)(object)transform == (Object)null)
		{
			// already inside an area: the game can't stack another one; the rescue puts us next to them
			return;
		}
		_applyingRemote = true;
		try
		{
			instance.prepareLocation(text, transform);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[Session] following into '" + text + "' failed in the game (" + ex.Message + ") - will go to them directly.");
		}
		finally
		{
			_applyingRemote = false;
		}
	}

	private static void StartFollowing()
	{
		_followSlot = CoopRuntime.Sender;
		_followSince = Time.unscaledTime;
		_followRescued = false;
		RemotePlayerPuppet remotePlayerPuppet = CoopPeers.Of(_followSlot);
		_followFrom = (((Object)(object)remotePlayerPuppet != (Object)null) ? ((Component)remotePlayerPuppet).transform.position : Vector3.zero);
	}

	// Plugin.Update: the rescue
	public static void Tick()
	{
		if (_followSlot < 0 || _followRescued)
		{
			return;
		}
		if (!CoopRuntime.SessionActive || Time.unscaledTime - _followSince > GiveUpAfter)
		{
			_followSlot = -1;
			return;
		}
		OutsideLocations instance = Singleton<OutsideLocations>.Instance;
		if (Time.unscaledTime - _followSince < RescueAfter || ((Object)(object)instance != (Object)null && (instance.loading || instance.spawningLocations)) || Core.forbidInputs)
		{
			return;
		}
		Player instance2 = Player.Instance;
		RemotePlayerPuppet remotePlayerPuppet = CoopPeers.Of(_followSlot);
		if ((Object)(object)instance2 == (Object)null || (Object)(object)remotePlayerPuppet == (Object)null || !remotePlayerPuppet.HasState)
		{
			return;
		}
		// wait for their position from inside the new area (it arrives after their own load)
		if (Time.unscaledTime - remotePlayerPuppet.LastStateAt > 1f || Vector3.Distance(((Component)remotePlayerPuppet).transform.position, _followFrom) < 300f)
		{
			return;
		}
		Vector3 position = ((Component)remotePlayerPuppet).transform.position;
		float num = Vector3.Distance(((Component)instance2).transform.position, position);
		if (num <= FarAway)
		{
			_followRescued = true;
			_followSlot = -1;
			return;
		}
		Plugin.Log.LogWarning($"[Session] still {num:F0} away from '{remotePlayerPuppet.PeerName}' after the area change - moving next to them.");
		((Component)instance2).transform.position = position + new Vector3(40f, 0f, 0f);
		SceneCache.ForceRescanCharacters();
		CoopNotice.Show("Moved you to " + remotePlayerPuppet.PeerName + " (area change)", 5f);
		_followRescued = true;
		_followSlot = -1;
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
		Plugin.Log.LogInfo("[Session] '" + CoopPeers.NameOf(CoopRuntime.Sender) + "' went back to the world - following.");
		StartFollowing();
		OutsideLocations instance = Singleton<OutsideLocations>.Instance;
		if ((Object)(object)instance == (Object)null || !instance.playerInOutsideLocation)
		{
			// we never really got inside: nothing to undo here, the rescue walks us to them
			return;
		}
		_applyingRemote = true;
		try
		{
			instance.returnToWorld(flag, flag2);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[Session] following back to the world failed in the game (" + ex.Message + ") - will go to them directly.");
		}
		finally
		{
			_applyingRemote = false;
		}
	}
}
