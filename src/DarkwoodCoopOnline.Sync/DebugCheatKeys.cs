using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DebugCheatKeys
{
	private static bool _patched;

	public static bool GodMode;

	public static bool HitKillMode;

	public static bool FollowMode;

	private const float FollowInterval = 1f;

	private static float _followTimer;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Expected O, but got Unknown
		//IL_00ff: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Expected O, but got Unknown
		//IL_01b0: Unknown result type (might be due to invalid IL or missing references)
		//IL_01be: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "getHit", new Type[1] { typeof(float) }, (Type[])null), new HarmonyMethod(typeof(DebugCheatKeys), "BeforePlayerGetHitSimple", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "getHit", new Type[9]
			{
				typeof(float),
				typeof(Transform),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, (Type[])null), new HarmonyMethod(typeof(DebugCheatKeys), "BeforePlayerGetHitFull", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Character), "getHit", new Type[9]
			{
				typeof(float),
				typeof(Transform),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool),
				typeof(bool)
			}, (Type[])null), new HarmonyMethod(typeof(DebugCheatKeys), "BeforeCharacterGetHit", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	public static void Tick()
	{
		if (Input.GetKeyDown((KeyCode)283))
		{
			TeleportPeerToMeOnce();
		}
		if (Input.GetKeyDown((KeyCode)284))
		{
			GodMode = !GodMode;
			Plugin.Log.LogInfo($"[DebugCheatKeys] vida infinita (F3) = {GodMode}");
		}
		if (Input.GetKeyDown((KeyCode)285))
		{
			HitKillMode = !HitKillMode;
			Plugin.Log.LogInfo($"[DebugCheatKeys] dano máximo/hitkill (F4) = {HitKillMode}");
		}
		if (Input.GetKeyDown((KeyCode)287))
		{
			FollowMode = !FollowMode;
			_followTimer = 0f;
			Plugin.Log.LogInfo($"[DebugCheatKeys] puppet me segue (F6) = {FollowMode}");
		}
		if (FollowMode && CoopRuntime.SessionActive)
		{
			_followTimer += Time.deltaTime;
			if (_followTimer >= 1f)
			{
				_followTimer = 0f;
				TeleportPeerToMeOnce();
			}
		}
	}

	private static void TeleportPeerToMeOnce()
	{
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		if (!CoopRuntime.SessionActive || (Object)(object)Player.Instance == (Object)null)
		{
			Plugin.Log.LogInfo("[DebugCheatKeys] sem sessão/jogador local - ignorado.");
		}
		else
		{
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.PullPeerToPosition).Vec3(((Component)Player.Instance).transform.position).Finish());
		}
	}

	private static bool BeforePlayerGetHitSimple()
	{
		return !GodMode;
	}

	private static bool BeforePlayerGetHitFull()
	{
		return !GodMode;
	}

	private static bool BeforeCharacterGetHit(ref float Damage, Transform attackerTransform)
	{
		if (!HitKillMode)
		{
			return true;
		}
		if ((Object)(object)attackerTransform == (Object)null || (Object)(object)Player.Instance == (Object)null)
		{
			return true;
		}
		if ((Object)(object)attackerTransform != (Object)(object)((Component)Player.Instance).transform)
		{
			return true;
		}
		Damage = 999f;
		return true;
	}

	public static void Reset()
	{
		GodMode = false;
		HitKillMode = false;
		FollowMode = false;
		_followTimer = 0f;
	}
}
