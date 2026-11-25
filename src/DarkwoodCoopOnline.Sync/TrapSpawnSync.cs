using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class TrapSpawnSync
{
	private static bool _patched;

	private static readonly HashSet<string> _handledLocal = new HashSet<string>();

	private static readonly HashSet<string> _handledRemote = new HashSet<string>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Trigger), "OnAfterTrigger", new Type[2]
			{
				typeof(Collider),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(TrapSpawnSync), "AfterOnAfterTrigger", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterOnAfterTrigger(Trigger __instance, Collider other, bool doConnectChain)
	{
		if (!CoopRuntime.SessionActive || (Object)(object)__instance == (Object)null || __instance.prefabToSpawn == (Object)null || !doConnectChain)
		{
			return;
		}
		string text = KeyOf(__instance);
		if (_handledLocal.Contains(text))
		{
			return;
		}
		byte b = 0;
		string v = "";
		if ((Object)(object)other != (Object)null)
		{
			if ((Object)(object)((Component)other).GetComponentInParent<RemotePlayerPuppet>() != (Object)null)
			{
				return;
			}
			if ((Object)(object)Player.Instance != (Object)null && (Object)(object)((Component)other).gameObject == (Object)(object)((Component)Player.Instance).gameObject)
			{
				b = 1;
			}
			else
			{
				Character componentInParent = ((Component)other).GetComponentInParent<Character>();
				string text2 = (((Object)(object)componentInParent != (Object)null) ? EnemySync.ResolveOwnedId(componentInParent) : null);
				if (text2 != null)
				{
					b = 2;
					v = text2;
				}
			}
		}
		_handledLocal.Add(text);
		Plugin.Log.LogInfo($"[TrapSpawnSync] '{text}' disparou com prefab pra spawnar (isChainTrap={__instance.isChainTrap}) - anunciando (targetKind={b}).");
		CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.TrapSpawnFired).Str(text).Byte(b)
			.Str(v)
			.Finish());
	}

	public static void ApplyFired(PacketReader r)
	{
		string text = r.Str();
		byte b = r.Byte();
		string text2 = r.Str();
		if (_handledRemote.Contains(text))
		{
			return;
		}
		_handledRemote.Add(text);
		Trigger val = FindByKey(text);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogWarning("[TrapSpawnSync] peer anunciou disparo de '" + text + "' mas não achei o Trigger correspondente aqui.");
		}
		else
		{
			if (val.prefabToSpawn == (Object)null)
			{
				return;
			}
			GameObject val2 = Core.AddPrefab(val.prefabToSpawn, ((Component)val).transform.position + new Vector3(0f, 1f, 0f), Quaternion.Euler(90f, 0f, 0f), (GameObject)null, false);
			if ((Object)(object)val2 == (Object)null)
			{
				Plugin.Log.LogWarning("[TrapSpawnSync] falha ao criar o prefab de '" + text + "'.");
				return;
			}
			if (val.isChainTrap)
			{
				ChainParent component = val2.GetComponent<ChainParent>();
				if ((Object)(object)component != (Object)null)
				{
					GameObject val3 = null;
					switch (b)
					{
					case 1:
						val3 = (((Object)(object)CoopPeers.SenderPuppet != (Object)null) ? ((Component)CoopPeers.SenderPuppet).gameObject : null);
						break;
					case 2:
					{
						Character val4 = EnemySync.ResolvePuppetById(text2);
						val3 = (((Object)(object)val4 != (Object)null) ? ((Component)val4).gameObject : null);
						break;
					}
					}
					if ((Object)(object)val3 != (Object)null)
					{
						component.target = val3;
					}
					else
					{
						Plugin.Log.LogWarning($"[TrapSpawnSync] não consegui resolver o alvo (kind={b}, id='{text2}') pra acorrentar em '{text}' - corrente vai ficar sem alvo.");
					}
				}
			}
			Plugin.Log.LogInfo($"[TrapSpawnSync] replicado disparo de '{text}' (prefab criado, isChainTrap={val.isChainTrap}).");
		}
	}

	private static Trigger FindByKey(string key)
	{
		Trigger[] array = SceneCache.Triggers();
		foreach (Trigger val in array)
		{
			if (!((Object)(object)val == (Object)null) && KeyOf(val) == key)
			{
				return val;
			}
		}
		return null;
	}

	private static string KeyOf(Trigger t)
	{
		return ((Object)t).name + "@" + Mathf.RoundToInt(((Component)t).transform.position.x) + "_" + Mathf.RoundToInt(((Component)t).transform.position.z);
	}

	public static void Reset()
	{
		_handledLocal.Clear();
		_handledRemote.Clear();
	}
}
