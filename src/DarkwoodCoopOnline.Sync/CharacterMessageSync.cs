using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class CharacterMessageSync
{
	private class Playback
	{
		public CharacterMessage msg;

		public List<string> lines;

		public int index;

		public float nextAdvanceAt;

		public bool looping;
	}

	private static bool _patched;

	private static readonly List<(CharacterMessage msg, Transform target, int queuedFrame)> _pending = new List<(CharacterMessage, Transform, int)>();

	private const float RepeatCooldown = 1.5f;

	private static readonly Dictionary<string, (string text, float at)> _lastAnnounced = new Dictionary<string, (string, float)>();

	private static readonly List<Playback> _playing = new List<Playback>();

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Core), "displayMessage", new Type[4]
			{
				typeof(string),
				typeof(Transform),
				typeof(float),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(CharacterMessageSync), "Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void Postfix(Transform destTransform, CharacterMessage __result)
	{
		if (CoopRuntime.SessionActive && !((Object)(object)__result == (Object)null) && !((Object)(object)destTransform == (Object)null))
		{
			_pending.Add((__result, destTransform, Time.frameCount));
		}
	}

	public static byte[] PollNew()
	{
		if (_pending.Count == 0)
		{
			return null;
		}
		List<(byte, string, List<string>, bool, float)> list = null;
		for (int num = _pending.Count - 1; num >= 0; num--)
		{
			var (val, val2, num2) = _pending[num];
			if ((Object)(object)val == (Object)null || (Object)(object)val2 == (Object)null)
			{
				_pending.RemoveAt(num);
			}
			else if (Time.frameCount > num2)
			{
				_pending.RemoveAt(num);
				List<string> list2 = new List<string> { ((Object)(object)val.textMesh != (Object)null) ? val.textMesh.text : "" };
				if (val.texts != null)
				{
					for (int i = 1; i < val.texts.Count; i++)
					{
						list2.Add(Language.Get(val.texts[i], "CharacterDialogue.Dialogue"));
					}
				}
				byte b = 0;
				string text = "";
				if ((Object)(object)Player.Instance != (Object)null && (Object)(object)val2 == (Object)(object)((Component)Player.Instance).transform)
				{
					b = 1;
				}
				else
				{
					Character component = ((Component)val2).GetComponent<Character>();
					string text2 = (((Object)(object)component != (Object)null) ? EnemySync.ResolveOwnedId(component) : null);
					if (text2 != null)
					{
						b = 2;
						text = text2;
					}
					else
					{
						NPC component2 = ((Component)val2).GetComponent<NPC>();
						if ((Object)(object)component2 != (Object)null)
						{
							b = 3;
							text = NpcKeyOf(component2);
						}
					}
				}
				if (b == 0)
				{
					b = 4;
					text = TransformKeyOf(val2);
				}
				string key = $"{b}:{text}";
				string text3 = ((list2.Count > 0) ? list2[0] : "");
				if (!_lastAnnounced.TryGetValue(key, out (string, float) value) || !(value.Item1 == text3) || !(Time.unscaledTime - value.Item2 < 1.5f))
				{
					_lastAnnounced[key] = (text3, Time.unscaledTime);
					if (list == null)
					{
						list = new List<(byte, string, List<string>, bool, float)>();
					}
					list.Add((b, text, list2, val.looping, val.longevity));
				}
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.CharacterMessageShown).UShort((ushort)list.Count);
		foreach (var (b2, v, list3, v2, v3) in list)
		{
			Plugin.Log.LogInfo($"[CharacterMessageSync] fala nova (targetKind={b2}, {list3.Count} linha(s)) - anunciando.");
			packetWriter.Byte(b2).Str(v).Bool(v2)
				.Float(v3)
				.UShort((ushort)list3.Count);
			foreach (string item in list3)
			{
				packetWriter.Str(item);
			}
		}
		return packetWriter.Finish();
	}

	public static void ApplyShown(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			byte b = r.Byte();
			string text = r.Str();
			bool looping = r.Bool();
			float num2 = r.Float();
			int num3 = r.UShort();
			List<string> list = new List<string>(num3);
			for (int j = 0; j < num3; j++)
			{
				list.Add(r.Str());
			}
			if (list.Count == 0)
			{
				continue;
			}
			Transform val = ResolveTarget(b, text);
			if ((Object)(object)val == (Object)null)
			{
				Plugin.Log.LogWarning($"[CharacterMessageSync] não consegui resolver o alvo (kind={b}, id='{text}') - fala do peer ignorada.");
				continue;
			}
			CharacterMessage val2 = Core.displayMessage(list[0], val, (num2 <= 0f) ? 1f : num2, false);
			if (!((Object)(object)val2 == (Object)null) && list.Count > 1)
			{
				if ((Object)(object)val2.waitAndDie != (Object)null)
				{
					val2.waitAndDie.permanent = true;
				}
				_playing.Add(new Playback
				{
					msg = val2,
					lines = list,
					index = 0,
					nextAdvanceAt = Time.time + val2.getLongevity(),
					looping = looping
				});
			}
		}
	}

	public static void Tick()
	{
		for (int num = _playing.Count - 1; num >= 0; num--)
		{
			Playback playback = _playing[num];
			if ((Object)(object)playback.msg == (Object)null)
			{
				_playing.RemoveAt(num);
			}
			else
			{
				if (Time.time < playback.nextAdvanceAt)
				{
					continue;
				}
				playback.index++;
				if (playback.index >= playback.lines.Count)
				{
					if (!playback.looping)
					{
						_playing.RemoveAt(num);
						WaitAndDie waitAndDie = playback.msg.waitAndDie;
						if (waitAndDie != null)
						{
							waitAndDie.onDeath();
						}
						continue;
					}
					playback.index = 0;
				}
				playback.msg.textMesh.text = playback.lines[playback.index];
				playback.nextAdvanceAt = Time.time + playback.msg.getLongevity();
			}
		}
	}

	private static Transform ResolveTarget(byte targetKind, string targetId)
	{
		switch (targetKind)
		{
		case 1:
			// the player who sent the message (the nearest one if it's resolved later)
			RemotePlayerPuppet remotePlayerPuppet = CoopPeers.SenderPuppet ?? CoopRuntime.Session?.Puppet;
			if (!((Object)(object)remotePlayerPuppet != (Object)null))
			{
				return null;
			}
			return ((Component)remotePlayerPuppet).transform;
		case 2:
		{
			Character val2 = EnemySync.ResolvePuppetById(targetId);
			if (!((Object)(object)val2 != (Object)null))
			{
				return null;
			}
			return ((Component)val2).transform;
		}
		case 3:
		{
			NPC[] array2 = SceneCache.Npcs();
			foreach (NPC val3 in array2)
			{
				if ((Object)(object)val3 != (Object)null && NpcKeyOf(val3) == targetId)
				{
					return ((Component)val3).transform;
				}
			}
			return null;
		}
		case 4:
		{
			Transform result = null;
			float num = 400f;
			Transform[] array = SceneCache.AllTransforms();
			for (int pass = 0; pass < 2 && (Object)(object)result == (Object)null; pass++)
			{
				foreach (Transform val in array)
				{
					if (!((Object)(object)val == (Object)null) && TransformKeyMatches(val, targetId, out var dist) && dist < num)
					{
						num = dist;
						result = val;
					}
				}
				if ((Object)(object)result == (Object)null)
				{
					array = SceneCache.AllTransforms(fresh: true);
				}
			}
			return result;
		}
		default:
			return null;
		}
	}

	private static string NpcKeyOf(NPC npc)
	{
		return $"{((Object)((Component)npc).gameObject).name}@{Mathf.RoundToInt(((Component)npc).transform.position.x)}_{Mathf.RoundToInt(((Component)npc).transform.position.z)}";
	}

	private static string TransformKeyOf(Transform t)
	{
		return $"{((Object)((Component)t).gameObject).name}@{Mathf.RoundToInt(t.position.x)}_{Mathf.RoundToInt(t.position.z)}";
	}

	private static bool TransformKeyMatches(Transform t, string key, out float dist)
	{
		dist = float.MaxValue;
		int num = key.LastIndexOf('@');
		if (num < 0)
		{
			return false;
		}
		string text = key.Substring(0, num);
		if (((Object)((Component)t).gameObject).name != text)
		{
			return false;
		}
		string[] array = key.Substring(num + 1).Split(new char[1] { '_' });
		if (array.Length != 2 || !int.TryParse(array[0], out var result) || !int.TryParse(array[1], out var result2))
		{
			return false;
		}
		dist = Vector2.Distance(new Vector2(t.position.x, t.position.z), new Vector2((float)result, (float)result2));
		return true;
	}

	public static void Reset()
	{
		_pending.Clear();
		_playing.Clear();
		_lastAnnounced.Clear();
	}
}
