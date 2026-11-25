using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PrologueEncounterPullSync
{
	private const float PullRange = 1200f;

	private static bool _patched;

	private static bool _wasForbidding;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		//IL_0046: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.PropertySetter(typeof(Core), "forbidInputs"), (HarmonyMethod)null, new HarmonyMethod(typeof(PrologueEncounterPullSync), "AfterSetForbidInputs", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool InPrologue()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.whereAmI != (Object)null && (Object)(object)instance.whereAmI.bigLocation != (Object)null)
		{
			return instance.whereAmI.bigLocation.isPrologueLocation;
		}
		return false;
	}

	private static void AfterSetForbidInputs(bool value)
	{
		//IL_0061: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b2: Unknown result type (might be due to invalid IL or missing references)
		bool num = value && !_wasForbidding;
		_wasForbidding = value;
		if (!num || !CoopRuntime.SessionActive || !InPrologue())
		{
			return;
		}
		CoopSession session = CoopRuntime.Session;
		RemotePlayerPuppet remotePlayerPuppet = session?.Puppet;
		Player instance = Player.Instance;
		if (!((Object)(object)remotePlayerPuppet == (Object)null) && remotePlayerPuppet.HasState && !((Object)(object)instance == (Object)null))
		{
			float num2 = Vector3.Distance(((Component)remotePlayerPuppet).transform.position, ((Component)instance).transform.position);
			if (!(num2 <= 1200f))
			{
				Plugin.Log.LogInfo("[PrologueEncounterPullSync] fiquei travado num momento scriptado do prólogo (forbidInputs) " + $"e o peer está longe (dist={num2:F0}) - puxando ele pra cá.");
				session.SendReliable(PacketWriter.Begin(PacketType.PullPeerToPosition).Vec3(((Component)instance).transform.position).Finish());
			}
		}
	}

	public static void Reset()
	{
		_wasForbidding = false;
	}
}
