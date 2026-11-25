using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DropDiagnostics
{
	private const float Radius = 600f;

	private static bool _watching;

	private static readonly Dictionary<Item, bool> _known = new Dictionary<Item, bool>();

	public static void Capture()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogInfo("[DropDiag] Player null");
			return;
		}
		Plugin.Log.LogInfo("========================= [DropDiag] CAPTURA =========================");
		int num = 0;
		Item[] array = SceneCache.Items();
		foreach (Item val in array)
		{
			if (!((Object)(object)val == (Object)null) && val.isDroppedItem)
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, ((Component)instance).transform.position);
				if (!(num2 > 600f))
				{
					DumpItem(val, num2);
					num++;
				}
			}
		}
		Plugin.Log.LogInfo($"[DropDiag] {num} item(ns) largado(s) na área.");
		Plugin.Log.LogInfo("========================================================================");
	}

	private static void DumpItem(Item it, float dist)
	{
		GameObject gameObject = ((Component)it).gameObject;
		Inventory component = ((Component)it).GetComponent<Inventory>();
		string arg = "?";
		int num = 0;
		float num2 = 0f;
		if ((Object)(object)component != (Object)null && component.slots != null && component.slots.Count > 0 && !InvItemClass.isNull(component.slots[0].invItem))
		{
			InvItemClass invItem = component.slots[0].invItem;
			arg = invItem.type;
			num = invItem.amount;
			num2 = invItem.durability;
		}
		Plugin.Log.LogInfo($"[DropDiag] go='{((Object)gameObject).name}'(id={((Object)gameObject).GetInstanceID()}) dist={dist:F0} " + $"isDroppedItem={it.isDroppedItem} disabled={it.disabled} destroyed={it.destroyed} " + $"health={it.health} draggable={it.draggable}");
		Plugin.Log.LogInfo($"[DropDiag]   invSlots={(((Object)(object)component != (Object)null && component.slots != null) ? component.slots.Count : (-1))} " + $"type='{arg}' amount={num} durability={num2:F2}");
		TimestampedLogSource log = Plugin.Log;
		Vector3 val = gameObject.transform.position;
		string text = val.ToString("F1");
		val = gameObject.transform.eulerAngles;
		log.LogInfo("[DropDiag]   wpos=" + text + " wrot=" + val.ToString("F1"));
		List<string> list = new List<string>();
		Component[] components = gameObject.GetComponents<Component>();
		foreach (Component val2 in components)
		{
			if ((Object)(object)val2 != (Object)null && !(val2 is Transform))
			{
				list.Add(((object)val2).GetType().Name);
			}
		}
		Plugin.Log.LogInfo("[DropDiag]   componentes=[" + string.Join(",", list.ToArray()) + "]");
	}

	public static void ToggleWatch()
	{
		_watching = !_watching;
		Plugin.Log.LogInfo($"[DropDiag] watch = {_watching}");
		if (_watching)
		{
			_known.Clear();
		}
	}

	public static void Tick()
	{
		if (!_watching)
		{
			return;
		}
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		HashSet<Item> hashSet = new HashSet<Item>();
		Item[] array = SceneCache.Items();
		foreach (Item val in array)
		{
			if ((Object)(object)val == (Object)null || !val.isDroppedItem || Vector3.Distance(((Component)val).transform.position, ((Component)instance).transform.position) > 600f)
			{
				continue;
			}
			hashSet.Add(val);
			if (!_known.ContainsKey(val))
			{
				_known[val] = true;
				string text = "?";
				int num = 0;
				Inventory component = ((Component)val).GetComponent<Inventory>();
				if ((Object)(object)component != (Object)null && component.slots != null && component.slots.Count > 0 && !InvItemClass.isNull(component.slots[0].invItem))
				{
					text = component.slots[0].invItem.type;
					num = component.slots[0].invItem.amount;
				}
				TimestampedLogSource log = Plugin.Log;
				string text2 = $"[DropWatch] t={Time.time:F2} NOVO '{val.name}' type='{text}' amount={num} ";
				Vector3 position = ((Component)val).transform.position;
				log.LogInfo(text2 + "pos=" + position.ToString("F1"));
			}
		}
		List<Item> list = null;
		foreach (KeyValuePair<Item, bool> item in _known)
		{
			if ((Object)(object)item.Key == (Object)null || !hashSet.Contains(item.Key))
			{
				if (list == null)
				{
					list = new List<Item>();
				}
				list.Add(item.Key);
			}
		}
		if (list == null)
		{
			return;
		}
		foreach (Item item2 in list)
		{
			Plugin.Log.LogInfo(string.Format("[DropWatch] t={0:F2} SUMIU '{1}'", Time.time, ((Object)(object)item2 != (Object)null) ? item2.name : "(destruído)"));
			_known.Remove(item2);
		}
	}

	public static void Reset()
	{
		_known.Clear();
		_watching = false;
	}
}
