using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PlayerDamageSync
{
	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(CharBase), "getHit", new Type[9]
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
			}, (Type[])null);
			val.Patch((MethodBase)methodInfo, (HarmonyMethod)null, new HarmonyMethod(typeof(PlayerDamageSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "getHitByShadow", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PlayerDamageSync), "ShadowHitPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void ShadowHitPostfix()
	{
		if (CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo("[PlayerDamage] levei dano de Sombra de verdade - avisando o peer pra mostrar a reação no meu puppet.");
			CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.PlayerShadowHit).Finish());
		}
	}

	public static void ApplyShadowHit()
	{
		CoopPeers.SenderPuppet?.PlayHitReaction();
	}

	private static void Postfix(CharBase __instance, float damage, bool normalHit)
	{
		if (__instance is RemotePlayerPuppet remotePlayerPuppet)
		{
			RelayHit(damage, normalHit, remotePlayerPuppet.Slot);
		}
	}

	// The hit lands on the player whose puppet took it, on their own PC
	public static void RelayHit(float damage, bool normalHit, int targetSlot)
	{
		if (!(damage <= 0f) && CoopRuntime.SessionActive)
		{
			Plugin.Log.LogInfo($"[PlayerDamage] puppet local levou {damage} (normalHit={normalHit}) — repassando pro peer.");
			CoopRuntime.Session?.SendTo(targetSlot, PacketWriter.Begin(PacketType.PlayerDamage).Float(damage).Bool(normalHit)
				.Finish());
		}
	}

	public static void ApplyDamage(PacketReader r)
	{
		float num = r.Float();
		bool flag = r.Bool();
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			((CharBase)instance).getHit(num, (Transform)null, false, false, true, flag, false, false, false);
		}
	}
}
