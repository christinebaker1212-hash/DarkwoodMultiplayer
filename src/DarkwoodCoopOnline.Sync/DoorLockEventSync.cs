using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DoorLockEventSync
{
	private const float PullRange = 1200f;

	private const float MatchRadius = 400f;

	private static bool _patched;

	private static bool _applyingRemote;

	private static readonly HashSet<string> _announced = new HashSet<string>();

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_005a: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Door), "lockMe", new Type[1] { typeof(string) }, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(DoorLockEventSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(Door __instance, string keyType)
	{
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_008f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0134: Unknown result type (might be due to invalid IL or missing references)
		if (_applyingRemote || !CoopRuntime.SessionActive || (Object)(object)__instance == (Object)null)
		{
			return;
		}
		string name = ((Object)((Component)__instance).gameObject).name;
		Vector3 position = ((Component)__instance).transform.position;
		string item = KeyOf(name, position);
		if (!_announced.Add(item))
		{
			return;
		}
		Plugin.Log.LogInfo("[DoorLockEventSync] '" + name + "' trancada por script (keyType='" + keyType + "') - avisando o peer.");
		CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.DoorLockEvent).Str(name).Vec3(position)
			.Str(keyType)
			.Finish());
		CoopSession session = CoopRuntime.Session;
		RemotePlayerPuppet remotePlayerPuppet = session?.Puppet;
		if ((Object)(object)remotePlayerPuppet != (Object)null && remotePlayerPuppet.HasState && (Object)(object)Player.Instance != (Object)null)
		{
			float num = Vector3.Distance(((Component)remotePlayerPuppet).transform.position, ((Component)Player.Instance).transform.position);
			if (num > 1200f)
			{
				Plugin.Log.LogInfo($"[DoorLockEventSync] peer está longe (dist={num:F0}) - puxando ele pra cá.");
				session.SendReliable(PacketWriter.Begin(PacketType.PullPeerToPosition).Vec3(((Component)Player.Instance).transform.position).Finish());
			}
		}
	}

	public static void ApplyLockEvent(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0068: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 val = r.Vec3();
		string text2 = r.Str();
		_announced.Add(KeyOf(text, val));
		Door val2 = null;
		float num = 400f;
		Door[] array = SceneCache.Doors();
		foreach (Door val3 in array)
		{
			if (!((Object)(object)val3 == (Object)null) && !(((Object)((Component)val3).gameObject).name != text))
			{
				float num2 = Vector3.Distance(((Component)val3).transform.position, val);
				if (num2 < num)
				{
					num = num2;
					val2 = val3;
				}
			}
		}
		if ((Object)(object)val2 == (Object)null)
		{
			Plugin.Log.LogWarning("[DoorLockEventSync] peer trancou '" + text + "' mas não achei essa porta na minha cena.");
			return;
		}
		Plugin.Log.LogInfo("[DoorLockEventSync] aplicando trancamento de '" + text + "' recebido do peer.");
		_applyingRemote = true;
		val2.lockMe(text2);
		_applyingRemote = false;
	}

	public static void ApplyPullToPosition(PacketReader r)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		Vector3 val = r.Vec3();
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			Plugin.Log.LogInfo($"[DoorLockEventSync] peer trancou uma porta longe de mim - vindo pra {val:F0}.");
			((Component)instance).transform.position = val;
			SceneCache.ForceRescanCharacters();
		}
	}

	private static string KeyOf(string name, Vector3 p)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return $"{name}@{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void Reset()
	{
		_announced.Clear();
	}
}
