using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class LocationOwnership
{
	private static bool _patched;

	private static readonly Dictionary<string, Location> _claimedByMe = new Dictionary<string, Location>();

	private static readonly HashSet<string> _claimedByPeer = new HashSet<string>();

	private const float WakeRange = 1200f;

	private const float WakeInterval = 1f;

	private static float _wakeTimer;

	public static void WakeNearbyInactiveTick(Vector3 peerPos)
	{
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_009c: Unknown result type (might be due to invalid IL or missing references)
		_wakeTimer += Time.deltaTime;
		if (_wakeTimer < 1f)
		{
			return;
		}
		_wakeTimer = 0f;
		foreach (KeyValuePair<string, Location> item in _claimedByMe)
		{
			Location value = item.Value;
			if ((Object)(object)value == (Object)null || value.charactersList == null)
			{
				continue;
			}
			foreach (Character characters in value.charactersList)
			{
				if (!((Object)(object)characters == (Object)null) && !((Component)characters).gameObject.activeSelf && !(Vector3.Distance(((Component)characters).transform.position, peerPos) > 1200f))
				{
					((Component)characters).gameObject.SetActive(true);
					Plugin.Log.LogInfo("[LocationOwnership] acordei personagem inativo perto do peer (WorldGrid só ativa por proximidade a MIM, e eu não estava perto): '" + ((Object)((Component)characters).gameObject).name + "' na Location '" + item.Key + "'.");
				}
			}
		}
	}

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Unknown result type (might be due to invalid IL or missing references)
		//IL_007b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Expected O, but got Unknown
		//IL_0088: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			MethodInfo methodInfo = AccessTools.Method(typeof(Location), "initializeLocation", (Type[])null, (Type[])null);
			Plugin.Log.LogInfo("[LocationOwnership] método encontrado pra patch: " + ((methodInfo != null) ? methodInfo.ToString() : "NULO - AccessTools não achou initializeLocation"));
			val.Patch((MethodBase)methodInfo, new HarmonyMethod(typeof(LocationOwnership), "Prefix", (Type[])null), new HarmonyMethod(typeof(LocationOwnership), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			Plugin.Log.LogInfo("[LocationOwnership] patch aplicado com sucesso.");
		}
	}

	private static string LocationId(Location loc)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_000b: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_002c: Unknown result type (might be due to invalid IL or missing references)
		Vector3 position = ((Component)loc).transform.position;
		return $"{((Object)((Component)loc).gameObject).name}@{Mathf.RoundToInt(position.x)}_{Mathf.RoundToInt(position.z)}";
	}

	private static bool IsDreamLocation(Location loc)
	{
		if ((Object)(object)loc != (Object)null && (Object)(object)((Component)loc).gameObject != (Object)null)
		{
			return ((Object)((Component)loc).gameObject).name.StartsWith("dream_", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}

	private static bool Prefix(Location __instance)
	{
		if (!CoopRuntime.SessionActive)
		{
			return true;
		}
		if (IsDreamLocation(__instance))
		{
			return true;
		}
		string text = LocationId(__instance);
		if (_claimedByPeer.Contains(text))
		{
			Plugin.Log.LogInfo("[LocationOwnership] Location '" + text + "' já é do peer - pulando inicialização nativa aqui.");
			return false;
		}
		return true;
	}

	private static void Postfix(Location __instance)
	{
		if (CoopRuntime.SessionActive && !IsDreamLocation(__instance))
		{
			string text = LocationId(__instance);
			if (!_claimedByPeer.Contains(text) && !_claimedByMe.ContainsKey(text))
			{
				_claimedByMe[text] = __instance;
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ChunkClaim).Str(text).Finish());
				Plugin.Log.LogInfo("[LocationOwnership] reivindiquei a Location '" + text + "' - personagens dali são meus, avisando o peer.");
			}
		}
	}

	public static void ApplyClaim(PacketReader r)
	{
		string text = r.Str();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (_claimedByMe.TryGetValue(text, out var value))
		{
			if (CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
			{
				return;
			}
			Plugin.Log.LogWarning("[LocationOwnership] corrida de reivindicação na Location '" + text + "' - cedendo pro host (destruindo meus personagens já spawnados ali).");
			_claimedByMe.Remove(text);
			if ((Object)(object)value != (Object)null)
			{
				foreach (Character characters in value.charactersList)
				{
					if ((Object)(object)characters != (Object)null)
					{
						Object.Destroy((Object)(object)((Component)characters).gameObject);
					}
				}
			}
			_claimedByPeer.Add(text);
		}
		else
		{
			_claimedByPeer.Add(text);
		}
	}

	public static void Reset()
	{
		_claimedByMe.Clear();
		_claimedByPeer.Clear();
		_wakeTimer = 0f;
	}
}
