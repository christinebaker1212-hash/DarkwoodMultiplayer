using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class KeyItemSync
{
	private enum Kind : byte
	{
		Note,
		Key,
		StoryItem,
		PhysicalItem
	}

	private static readonly HashSet<string> _known = new HashSet<string>();

	public static byte[] PollLocalPickups()
	{
		UI instance = Singleton<UI>.Instance;
		if ((Object)(object)instance?.journal != (Object)null)
		{
			foreach (string key in instance.journal.notesDict.Keys)
			{
				if (_known.Add(key))
				{
					return Build(key, Kind.Note);
				}
			}
			foreach (string key2 in instance.journal.keysDict.Keys)
			{
				if (_known.Add(key2))
				{
					return Build(key2, Kind.Key);
				}
			}
			foreach (string key3 in instance.journal.itemsDict.Keys)
			{
				if (_known.Add(key3))
				{
					return Build(key3, Kind.StoryItem);
				}
			}
		}
		Player instance2 = Player.Instance;
		if ((Object)(object)instance2?.Inventory != (Object)null)
		{
			foreach (InvItemClass item in instance2.Inventory.getAllItemsInPlayer())
			{
				if (!((Object)(object)item?.baseClass == (Object)null) && item.baseClass.isImportantItem && _known.Add(item.type))
				{
					return Build(item.type, Kind.PhysicalItem);
				}
			}
		}
		return null;
	}

	private static byte[] Build(string type, Kind kind)
	{
		Plugin.Log.LogInfo($"[KeyItemSync] item-chave novo aqui: '{type}' ({kind}) — mandando pro peer.");
		return PacketWriter.Begin(PacketType.KeyItemPickup).Str(type).Byte((byte)kind)
			.Finish();
	}

	public static void ApplyPickup(PacketReader r)
	{
		//IL_006b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Expected O, but got Unknown
		//IL_00b1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Expected O, but got Unknown
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0105: Expected O, but got Unknown
		string text = r.Str();
		Kind kind = (Kind)r.Byte();
		_known.Add(text);
		Journal val = Singleton<UI>.Instance?.journal;
		switch (kind)
		{
		case Kind.Note:
			if ((Object)(object)val != (Object)null && !val.notesDict.ContainsKey(text))
			{
				val.notesDict.Add(text, new Journal.Note
				{
					type = text
				});
				val.showJournalInfoPopup("Note", text);
			}
			break;
		case Kind.Key:
			if ((Object)(object)val != (Object)null && !val.keysDict.ContainsKey(text))
			{
				val.keysDict.Add(text, new Journal.Key
				{
					type = text
				});
				val.showJournalInfoPopup("Key", text);
			}
			break;
		case Kind.StoryItem:
			if ((Object)(object)val != (Object)null && !val.itemsDict.ContainsKey(text))
			{
				val.itemsDict.Add(text, new Journal.Item
				{
					type = text
				});
				val.showJournalInfoPopup("QuestItem", text);
			}
			break;
		case Kind.PhysicalItem:
		{
			Player instance = Player.Instance;
			if ((Object)(object)instance?.Inventory != (Object)null && instance.Inventory.getItemInPlayer(text, 0f, false) == null)
			{
				instance.Inventory.addItemTypeToPlayer(text, 1, true);
				if (val != null)
				{
					val.showJournalInfoPopup("InvItem", text);
				}
			}
			break;
		}
		}
		Plugin.Log.LogInfo($"[KeyItemSync] item-chave '{text}' ({kind}) recebido do peer — aplicado.");
	}

	public static void Reset()
	{
		_known.Clear();
	}
}
