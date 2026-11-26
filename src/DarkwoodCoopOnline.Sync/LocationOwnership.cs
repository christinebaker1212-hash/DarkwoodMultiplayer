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

	// location id -> slot of the player who owns it
	private static readonly Dictionary<string, int> _claimedByPeer = new Dictionary<string, int>();

	private const float WakeRange = 1200f;

	private const float WakeInterval = 1f;

	private static float _wakeTimer;

	public static void WakeNearbyInactiveTick(List<Vector3> peerPositions)
	{
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
				if (!((Object)(object)characters == (Object)null) && !((Component)characters).gameObject.activeSelf && WithinAny(((Component)characters).transform.position, peerPositions, 1200f))
				{
					((Component)characters).gameObject.SetActive(true);
					Plugin.Log.LogInfo("[LocationOwnership] acordei personagem inativo perto do peer (WorldGrid só ativa por proximidade a MIM, e eu não estava perto): '" + ((Object)((Component)characters).gameObject).name + "' na Location '" + item.Key + "'.");
				}
			}
		}
	}

	public static void EnsurePatched(string modId)
	{
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
		if (_claimedByPeer.ContainsKey(text))
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
			if (!_claimedByPeer.ContainsKey(text) && !_claimedByMe.ContainsKey(text))
			{
				_claimedByMe[text] = __instance;
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ChunkClaim).Str(text).Finish());
				Plugin.Log.LogInfo("[LocationOwnership] reivindiquei a Location '" + text + "' - personagens dali são meus, avisando o peer.");
			}
		}
	}

	public static void ApplyClaim(PacketReader r, int from)
	{
		string text = r.Str();
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		if (_claimedByMe.TryGetValue(text, out var value))
		{
			// both of us set the place up at once: the lower slot keeps it (the host is slot 0)
			int num = CoopRuntime.Session?.MySlot ?? 0;
			if (num < from)
			{
				return;
			}
			Plugin.Log.LogWarning($"[LocationOwnership] claim race on '{text}' with slot {from} - giving it to them (destroying my characters there).");
			_claimedByMe.Remove(text);
			if ((Object)(object)value != (Object)null)
			{
				foreach (Character characters in value.charactersList)
				{
					if ((Object)(object)characters != (Object)null)
					{
						// tell everyone to drop their copies too (they'd stay frozen otherwise)
						EnemySync.RegisterForcedRemoval(characters);
						Object.Destroy((Object)(object)((Component)characters).gameObject);
					}
				}
			}
			_claimedByPeer[text] = from;
		}
		else if (!_claimedByPeer.TryGetValue(text, out var value2) || from < value2)
		{
			_claimedByPeer[text] = from;
		}
	}

	// A player left: the places they owned are nobody's now (the next player to set one up claims it)
	public static void OnPeerLeft(int slot)
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, int> item in _claimedByPeer)
		{
			if (item.Value == slot)
			{
				list.Add(item.Key);
			}
		}
		foreach (string item2 in list)
		{
			_claimedByPeer.Remove(item2);
		}
		if (list.Count > 0)
		{
			Plugin.Log.LogInfo($"[LocationOwnership] slot {slot} left - released {list.Count} location(s) they owned.");
		}
	}

	private static bool WithinAny(Vector3 x, List<Vector3> positions, float range)
	{
		for (int i = 0; i < positions.Count; i++)
		{
			if (Vector3.Distance(x, positions[i]) <= range)
			{
				return true;
			}
		}
		return false;
	}

	public static void Reset()
	{
		_claimedByMe.Clear();
		_claimedByPeer.Clear();
		_wakeTimer = 0f;
	}
}
