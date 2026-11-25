using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DialogueStateSync
{
	private const float Range = 4000f;

	private static readonly Dictionary<string, bool> _knownDisabled = new Dictionary<string, bool>();

	private static readonly Dictionary<string, bool> _knownAlreadyShown = new Dictionary<string, bool>();

	private static readonly Dictionary<string, int> _knownOptionCount = new Dictionary<string, int>();

	private static readonly Dictionary<string, int> _knownPortrait = new Dictionary<string, int>();

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_021b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0222: Expected I4, but got Unknown
		List<(string, string, bool, bool)> list = null;
		List<(string, string, string)> list2 = null;
		List<(string, int)> list3 = null;
		NPC[] array = SceneCache.Npcs();
		foreach (NPC val in array)
		{
			if ((Object)(object)val == (Object)null || (Object)(object)val.characterDialogue == (Object)null || Mathf.Min(Vector3.Distance(((Component)val).transform.position, p1), Vector3.Distance(((Component)val).transform.position, p2)) > 4000f)
			{
				continue;
			}
			string text = KeyOf(((Object)((Component)val).gameObject).name, ((Component)val).transform.position);
			CharacterDialogue characterDialogue = val.characterDialogue;
			foreach (CharacterDialogue.Dialogue dialogue in characterDialogue.dialogues)
			{
				if (dialogue == null || string.IsNullOrEmpty(dialogue.name))
				{
					continue;
				}
				string key = text + "#" + dialogue.name;
				bool value;
				bool num = _knownDisabled.TryGetValue(key, out value);
				bool value2;
				bool flag = _knownAlreadyShown.TryGetValue(key, out value2);
				if (!(num && value == dialogue.disabled && flag) || value2 != dialogue.alreadyShown)
				{
					_knownDisabled[key] = dialogue.disabled;
					_knownAlreadyShown[key] = dialogue.alreadyShown;
					if (list == null)
					{
						list = new List<(string, string, bool, bool)>();
					}
					list.Add((text, dialogue.name, dialogue.disabled, dialogue.alreadyShown));
				}
			}
			int value3;
			int num2 = (_knownOptionCount.TryGetValue(text, out value3) ? value3 : 0);
			if (characterDialogue.specialOptions.Count > num2)
			{
				for (int j = num2; j < characterDialogue.specialOptions.Count; j++)
				{
					CharacterDialogue.SpecialOption val2 = characterDialogue.specialOptions[j];
					if (list2 == null)
					{
						list2 = new List<(string, string, string)>();
					}
					list2.Add((text, val2.name, val2.type));
				}
				_knownOptionCount[text] = characterDialogue.specialOptions.Count;
			}
			int num3 = (int)characterDialogue.portraitType;
			if (!_knownPortrait.TryGetValue(text, out var value4))
			{
				_knownPortrait[text] = num3;
			}
			else if (value4 != num3)
			{
				_knownPortrait[text] = num3;
				if (list3 == null)
				{
					list3 = new List<(string, int)>();
				}
				list3.Add((text, num3));
			}
		}
		if (list == null && list2 == null && list3 == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.DialogueStateChanged);
		packetWriter.UShort((ushort)(list?.Count ?? 0));
		if (list != null)
		{
			foreach (var item in list)
			{
				packetWriter.Str(item.Item1).Str(item.Item2).Bool(item.Item3)
					.Bool(item.Item4);
			}
		}
		packetWriter.UShort((ushort)(list2?.Count ?? 0));
		if (list2 != null)
		{
			foreach (var item2 in list2)
			{
				packetWriter.Str(item2.Item1).Str(item2.Item2).Str(item2.Item3);
			}
		}
		packetWriter.UShort((ushort)(list3?.Count ?? 0));
		if (list3 != null)
		{
			foreach (var item3 in list3)
			{
				packetWriter.Str(item3.Item1).Int(item3.Item2);
			}
		}
		return packetWriter.Finish();
	}

	public static void ApplyChanges(PacketReader r)
	{
		//IL_0182: Unknown result type (might be due to invalid IL or missing references)
		//IL_018c: Expected O, but got Unknown
		//IL_0241: Unknown result type (might be due to invalid IL or missing references)
		//IL_0245: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Unknown result type (might be due to invalid IL or missing references)
		//IL_0253: Unknown result type (might be due to invalid IL or missing references)
		//IL_0255: Unknown result type (might be due to invalid IL or missing references)
		//IL_0266: Unknown result type (might be due to invalid IL or missing references)
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			string text2 = r.Str();
			bool flag = r.Bool();
			bool flag2 = r.Bool();
			string key = text + "#" + text2;
			_knownDisabled[key] = flag;
			_knownAlreadyShown[key] = flag2;
			NPC val = FindByKey(text);
			CharacterDialogue.Dialogue val2 = (((Object)(object)val != (Object)null && (Object)(object)val.characterDialogue != (Object)null) ? val.characterDialogue.getDialogue(text2) : null);
			if (val2 != null)
			{
				val2.disabled = flag;
				val2.alreadyShown = flag2;
				Plugin.Log.LogInfo($"[DialogueStateSync] '{text}': fala '{text2}' disabled={flag} alreadyShown={flag2} (peer).");
			}
		}
		int num2 = r.UShort();
		for (int j = 0; j < num2; j++)
		{
			string text3 = r.Str();
			string optName = r.Str();
			string optType = r.Str();
			NPC val3 = FindByKey(text3);
			if ((Object)(object)val3 != (Object)null && (Object)(object)val3.characterDialogue != (Object)null)
			{
				if (!val3.characterDialogue.specialOptions.Exists((CharacterDialogue.SpecialOption o) => o.name == optName && o.type == optType))
				{
					val3.characterDialogue.specialOptions.Add(new CharacterDialogue.SpecialOption(optName, optType));
					Plugin.Log.LogInfo("[DialogueStateSync] '" + text3 + "': nova opção especial '" + optName + "' (peer).");
				}
				_knownOptionCount[text3] = val3.characterDialogue.specialOptions.Count;
			}
		}
		int num3 = r.UShort();
		for (int k = 0; k < num3; k++)
		{
			string text4 = r.Str();
			int num4 = r.Int();
			_knownPortrait[text4] = num4;
			NPC val4 = FindByKey(text4);
			if ((Object)(object)val4 != (Object)null && (Object)(object)val4.characterDialogue != (Object)null)
			{
				CharacterDialogue.PortraitType val5 = (val4.portraitType = (CharacterDialogue.PortraitType)num4);
				val4.characterDialogue.portraitType = val5;
				Plugin.Log.LogInfo($"[DialogueStateSync] '{text4}': portraitType={val5} (peer).");
			}
		}
	}

	private static NPC FindByKey(string key)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		NPC[] array = SceneCache.Npcs();
		foreach (NPC val in array)
		{
			if (!((Object)(object)val == (Object)null) && KeyOf(((Object)((Component)val).gameObject).name, ((Component)val).transform.position) == key)
			{
				return val;
			}
		}
		return null;
	}

	private static string KeyOf(string name, Vector3 p)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		return $"{name}@{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void Reset()
	{
		_knownDisabled.Clear();
		_knownAlreadyShown.Clear();
		_knownOptionCount.Clear();
		_knownPortrait.Clear();
	}
}
