using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class CorpseSync
{
	private const float MatchRadius = 800f;

	private const float ScanInterval = 4f;

	private static float _nextScanAt = float.MinValue;

	private static readonly HashSet<Character> _announced = new HashSet<Character>();

	private static readonly HashSet<Character> _known = new HashSet<Character>();

	private static string CleanName(string name)
	{
		if (name == null || !name.EndsWith("(Clone)"))
		{
			return name;
		}
		return name.Substring(0, name.Length - 7);
	}

	public static byte[] PollFoundCorpses()
	{
		//IL_0106: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0127: Expected I4, but got Unknown
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		if (Time.time < _nextScanAt)
		{
			return null;
		}
		_nextScanAt = Time.time + 4f;
		List<(Character, string)> list = null;
		Character[] array = SceneCache.Characters();
		foreach (Character val in array)
		{
			if ((Object)(object)val == (Object)null || ((CharBase)val).alive || _announced.Contains(val) || _known.Contains(val))
			{
				continue;
			}
			if (EnemySync.IsClaimed(val))
			{
				_known.Add(val);
				continue;
			}
			if (list == null)
			{
				list = new List<(Character, string)>();
			}
			list.Add((val, CleanName(((Object)((Component)val).gameObject).name)));
			_announced.Add(val);
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.CorpseFound).UShort((ushort)list.Count);
		foreach (var (val2, text) in list)
		{
			Plugin.Log.LogInfo($"[CorpseSync] corpo não rastreado achado - anunciando: '{text}' pos={((Component)val2).transform.position:F0}");
			packetWriter.Int((int)((CharBase)val2).type).Vec2XZ(((Component)val2).transform.position).Str(text);
		}
		return packetWriter.Finish();
	}

	public static void ApplyFoundCorpses(PacketReader r)
	{
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0061: Invalid comparison between Unknown and I4
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d7: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Unknown result type (might be due to invalid IL or missing references)
		//IL_006f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0173: Unknown result type (might be due to invalid IL or missing references)
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			int num2 = r.Int();
			Vector3 val = r.Vec2XZ(0f);
			string text = r.Str();
			Character val2 = null;
			float num3 = 800f;
			Character[] array = SceneCache.Characters();
			foreach (Character val3 in array)
			{
				if (!((Object)(object)val3 == (Object)null) && !((CharBase)val3).alive && (int)((CharBase)val3).type == num2)
				{
					float num4 = Vector3.Distance(((Component)val3).transform.position, val);
					if (num4 < num3)
					{
						num3 = num4;
						val2 = val3;
					}
				}
			}
			if ((Object)(object)val2 != (Object)null)
			{
				_known.Add(val2);
			}
			else if (!string.IsNullOrEmpty(text))
			{
				GameObject val4 = Core.AddPrefab("Characters/" + text, val, Quaternion.Euler(90f, 0f, 0f), (GameObject)null, true);
				Character val5 = (((Object)(object)val4 != (Object)null) ? val4.GetComponent<Character>() : null);
				if ((Object)(object)val5 == (Object)null)
				{
					Plugin.Log.LogWarning("[CorpseSync] falhou ao criar corpo '" + text + "' (Characters/" + text + " não encontrado?)");
				}
				else
				{
					((Behaviour)val5).enabled = true;
					((CharBase)val5).getHit(99999f, (Transform)null, false, false, false, true, false, true, true);
					_known.Add(val5);
					Plugin.Log.LogInfo($"[CorpseSync] corpo criado a partir do anúncio do peer: '{text}' pos={val:F0}");
				}
			}
		}
	}

	public static void Reset()
	{
		_nextScanAt = float.MinValue;
		_announced.Clear();
		_known.Clear();
	}
}
