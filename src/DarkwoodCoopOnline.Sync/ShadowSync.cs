using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ShadowSync
{
	private struct LocalShadow
	{
		public ShadowCreature sc;

		public string id;
	}

	private static readonly List<LocalShadow> _localCache = new List<LocalShadow>();

	private static readonly HashSet<string> _announced = new HashSet<string>();

	private static readonly HashSet<string> _aliveSent = new HashSet<string>();

	private static List<string> _pendingDeaths;

	private static List<KeyValuePair<string, Vector3>> _pendingSpawns;

	private static readonly Dictionary<string, ShadowPuppet> _puppets = new Dictionary<string, ShadowPuppet>();

	public static byte[] BuildDelta()
	{
		HashSet<ShadowCreature> hashSet = new HashSet<ShadowCreature>();
		foreach (LocalShadow item2 in _localCache)
		{
			hashSet.Add(item2.sc);
		}
		ShadowCreature[] array = SceneCache.Shadows();
		foreach (ShadowCreature val in array)
		{
			if (!((Object)(object)val == (Object)null) && !hashSet.Contains(val) && !((Object)(object)((Component)val).GetComponent<ShadowPuppet>() != (Object)null))
			{
				_localCache.Add(new LocalShadow
				{
					sc = val,
					id = ((Object)val).GetInstanceID().ToString()
				});
			}
		}
		List<LocalShadow> list = null;
		for (int num = _localCache.Count - 1; num >= 0; num--)
		{
			LocalShadow item = _localCache[num];
			if ((Object)(object)item.sc == (Object)null)
			{
				if (_aliveSent.Contains(item.id))
				{
					if (_pendingDeaths == null)
					{
						_pendingDeaths = new List<string>();
					}
					_pendingDeaths.Add(item.id);
					_aliveSent.Remove(item.id);
				}
				_announced.Remove(item.id);
				_localCache.RemoveAt(num);
			}
			else
			{
				_aliveSent.Add(item.id);
				if (!_announced.Contains(item.id))
				{
					_announced.Add(item.id);
					if (_pendingSpawns == null)
					{
						_pendingSpawns = new List<KeyValuePair<string, Vector3>>();
					}
					_pendingSpawns.Add(new KeyValuePair<string, Vector3>(item.id, ((Component)item.sc).transform.position));
				}
				if (list == null)
				{
					list = new List<LocalShadow>();
				}
				list.Add(item);
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.ShadowDelta).UShort((ushort)list.Count);
		foreach (LocalShadow item3 in list)
		{
			tk2dSpriteAnimator component = ((Component)item3.sc).GetComponent<tk2dSpriteAnimator>();
			string v = (((Object)(object)component != (Object)null && component.CurrentClip != null) ? component.CurrentClip.name : "Float");
			packetWriter.Str(item3.id).Vec3(((Component)item3.sc).transform.position).Str(v);
		}
		return packetWriter.Finish();
	}

	public static byte[] BuildSpawns()
	{
		if (_pendingSpawns == null || _pendingSpawns.Count == 0)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.ShadowSpawn).UShort((ushort)_pendingSpawns.Count);
		foreach (KeyValuePair<string, Vector3> pendingSpawn in _pendingSpawns)
		{
			packetWriter.Str(pendingSpawn.Key).Vec3(pendingSpawn.Value);
		}
		_pendingSpawns = null;
		return packetWriter.Finish();
	}

	public static byte[] BuildDeaths()
	{
		if (_pendingDeaths == null || _pendingDeaths.Count == 0)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.ShadowDeath).UShort((ushort)_pendingDeaths.Count);
		foreach (string pendingDeath in _pendingDeaths)
		{
			packetWriter.Str(pendingDeath);
		}
		_pendingDeaths = null;
		return packetWriter.Finish();
	}

	public static void ApplyDelta(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			Vector3 pos = r.Vec3();
			string clip = r.Str();
			if (_puppets.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
			{
				value.SetTarget(pos, clip);
			}
		}
	}

	public static void ApplySpawns(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			Vector3 val = r.Vec3();
			if (_puppets.ContainsKey(text))
			{
				continue;
			}
			GameObject val2 = Core.AddPrefab("Characters/Shadow", val, Quaternion.Euler(90f, 0f, 0f), (GameObject)null, true);
			if ((Object)(object)val2 == (Object)null)
			{
				Plugin.Log.LogWarning("[ShadowSync] falhou ao criar puppet de sombra — prefab 'Characters/Shadow' não encontrado?");
				continue;
			}
			ShadowCreature component = val2.GetComponent<ShadowCreature>();
			if ((Object)(object)component != (Object)null)
			{
				Object.Destroy((Object)(object)component);
			}
			tk2dSpriteAnimator component2 = val2.GetComponent<tk2dSpriteAnimator>();
			ShadowPuppet shadowPuppet = val2.AddComponent<ShadowPuppet>();
			shadowPuppet.Bind(component2);
			_puppets[text] = shadowPuppet;
			Plugin.Log.LogInfo("[ShadowSync] puppet de sombra criado (id=" + text + ").");
		}
	}

	public static void ApplyDeaths(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string key = r.Str();
			if (_puppets.TryGetValue(key, out var value) && (Object)(object)value != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)value).gameObject);
			}
			_puppets.Remove(key);
		}
	}

	public static void Reset()
	{
		_localCache.Clear();
		_announced.Clear();
		_aliveSent.Clear();
		_pendingDeaths = null;
		_pendingSpawns = null;
		foreach (KeyValuePair<string, ShadowPuppet> puppet in _puppets)
		{
			if ((Object)(object)puppet.Value != (Object)null)
			{
				Object.Destroy((Object)(object)((Component)puppet.Value).gameObject);
			}
		}
		_puppets.Clear();
	}
}
