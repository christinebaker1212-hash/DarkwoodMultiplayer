using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PropSpawnSync
{
	private const float ContextWindowSeconds = 12f;

	private const float PendingExpirySeconds = 20f;

	private static bool _patched;

	private static readonly List<(string name, float time)> _recentEvents = new List<(string, float)>();

	private static readonly Dictionary<string, Queue<(GameObject go, float time)>> _pendingLocalSpawns = new Dictionary<string, Queue<(GameObject, float)>>();

	private static readonly Dictionary<string, Queue<(Vector3 pos, float time)>> _pendingHostPositions = new Dictionary<string, Queue<(Vector3, float)>>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Core), "AddPrefab", new Type[5]
			{
				typeof(Object),
				typeof(Vector3),
				typeof(Quaternion),
				typeof(GameObject),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(PropSpawnSync), "AfterAddPrefab", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	public static void NoteEventContext(string customEventName)
	{
		if (!string.IsNullOrEmpty(customEventName))
		{
			TrimExpiredEvents();
			_recentEvents.Add((customEventName, Time.time));
		}
	}

	private static void AfterAddPrefab(GameObject __result)
	{
		if ((Object)(object)__result == (Object)null || !CoopRuntime.SessionActive || (Object)(object)__result.GetComponent<Character>() != (Object)null)
		{
			return;
		}
		Infection component = __result.GetComponent<Infection>();
		if ((Object)(object)component != (Object)null)
		{
			Vector3 infectionOrigin = component.infectionOrigin;
			SyncProp("Infection|" + ((object)infectionOrigin).ToString(), __result);
			return;
		}
		TrimExpiredEvents();
		if (_recentEvents.Count != 0)
		{
			SyncProp(_recentEvents[_recentEvents.Count - 1].name + "|" + ((Object)__result).name, __result);
		}
	}

	private static void SyncProp(string key, GameObject go)
	{
		string name = ((Object)go).name;
		if (CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
		{
			Plugin.Log.LogInfo("[PropSpawnSync] '" + name + "' (chave='" + key + "') nasceu aqui em " + $"{go.transform.position:F0} - mandando posição real pro peer.");
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.PropSpawnPosition).Str(key).Str(name)
				.Vec3(go.transform.position)
				.Finish());
			return;
		}
		TrimExpiredSpawns();
		if (_pendingHostPositions.TryGetValue(key, out Queue<(Vector3, float)> value) && value.Count > 0)
		{
			Vector3 item = value.Dequeue().Item1;
			go.transform.position = item;
			Plugin.Log.LogInfo("[PropSpawnSync] '" + name + "' (chave='" + key + "') reposicionado pra " + $"{item:F0} (posição do host já tinha chegado).");
		}
		else
		{
			if (!_pendingLocalSpawns.TryGetValue(key, out Queue<(GameObject, float)> value2))
			{
				value2 = new Queue<(GameObject, float)>();
				_pendingLocalSpawns[key] = value2;
			}
			value2.Enqueue((go, Time.time));
		}
	}

	public static void ApplyRemote(PacketReader r)
	{
		string text = r.Str();
		string text2 = r.Str();
		Vector3 val = r.Vec3();
		TrimExpiredSpawns();
		if (_pendingLocalSpawns.TryGetValue(text, out Queue<(GameObject, float)> value))
		{
			while (value.Count > 0)
			{
				GameObject item = value.Dequeue().Item1;
				if (!((Object)(object)item == (Object)null))
				{
					item.transform.position = val;
					Plugin.Log.LogInfo("[PropSpawnSync] '" + text2 + "' (chave='" + text + "') que eu já tinha " + $"criado local - reposicionado pra {val:F0} (posição real do host).");
					return;
				}
			}
		}
		if (!_pendingHostPositions.TryGetValue(text, out Queue<(Vector3, float)> value2))
		{
			value2 = new Queue<(Vector3, float)>();
			_pendingHostPositions[text] = value2;
		}
		value2.Enqueue((val, Time.time));
	}

	private static void TrimExpiredEvents()
	{
		float time = Time.time;
		for (int num = _recentEvents.Count - 1; num >= 0; num--)
		{
			if (time - _recentEvents[num].time > 12f)
			{
				_recentEvents.RemoveAt(num);
			}
		}
	}

	private static void TrimExpiredSpawns()
	{
		float time = Time.time;
		foreach (Queue<(GameObject, float)> value in _pendingLocalSpawns.Values)
		{
			while (value.Count > 0 && time - value.Peek().Item2 > 20f)
			{
				value.Dequeue();
			}
		}
		foreach (Queue<(Vector3, float)> value2 in _pendingHostPositions.Values)
		{
			while (value2.Count > 0 && time - value2.Peek().Item2 > 20f)
			{
				value2.Dequeue();
			}
		}
	}

	public static void Reset()
	{
		_recentEvents.Clear();
		_pendingLocalSpawns.Clear();
		_pendingHostPositions.Clear();
	}
}
