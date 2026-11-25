using System.Text;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ChestDiagnostics
{
	private const float SearchRadius = 100f;

	private static Item _item;

	private static Inventory _inventory;

	private static bool _streaming;

	private static float _lastLog = -999f;

	private const float LogInterval = 0.25f;

	private static string _lastSnapshot = "";

	public static void Capture()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogInfo("[ChestDiag] Player null");
			return;
		}
		Item val = null;
		Inventory val2 = null;
		float num = -1f;
		bool flag = false;
		if ((Object)(object)instance.selectedObject != (Object)null)
		{
			Item component = ((Component)instance.selectedObject).GetComponent<Item>();
			if ((Object)(object)component != (Object)null)
			{
				Inventory component2 = ((Component)component).GetComponent<Inventory>();
				if ((Object)(object)component2 != (Object)null)
				{
					val = component;
					val2 = component2;
					num = Vector3.Distance(((Component)component).transform.position, ((Component)instance).transform.position);
					flag = true;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			float num2 = 100f;
			Item[] array = Object.FindObjectsOfType<Item>();
			foreach (Item val3 in array)
			{
				if ((Object)(object)val3 == (Object)null)
				{
					continue;
				}
				Inventory component3 = ((Component)val3).GetComponent<Inventory>();
				if (!((Object)(object)component3 == (Object)null))
				{
					float num3 = Vector3.Distance(((Component)val3).transform.position, ((Component)instance).transform.position);
					if (num3 < num2)
					{
						num2 = num3;
						val = val3;
						val2 = component3;
					}
				}
			}
			num = num2;
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[ChestDiag] nenhum Item com Inventory na cena por perto");
			return;
		}
		_item = val;
		_inventory = val2;
		Plugin.Log.LogInfo("========================= [ChestDiag] CAPTURA =========================");
		TimestampedLogSource log = Plugin.Log;
		string text = string.Format("[ChestDiag] alvo ({0}) a {1:F0} do Player: ", flag ? "selectedObject" : "raio", num);
		string text2 = $"name='{val.name}' invType={val2.invType} searched={val.searched} ";
		object arg = val.destroyToAccessInventory;
		Vector3 position = ((Component)val).transform.position;
		log.LogInfo(text + text2 + string.Format("destroyToAccessInventory={0} wpos={1}", arg, position.ToString("F1")));
		Plugin.Log.LogInfo(DumpSlots(val2));
		Plugin.Log.LogInfo("[ChestDiag] alvo travado. Keypad2 = começar/parar streaming por frame.");
		Plugin.Log.LogInfo("========================================================================");
	}

	private static string DumpSlots(Inventory inv)
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append($"[ChestDiag] slots={inv.slots.Count}: ");
		for (int i = 0; i < inv.slots.Count; i++)
		{
			InvItemClass invItem = inv.slots[i].invItem;
			if (InvItemClass.isNull(invItem))
			{
				stringBuilder.Append($"[{i}:vazio] ");
				continue;
			}
			stringBuilder.Append($"[{i}:{invItem.type} x{invItem.amount} dur={invItem.durability:F1}] ");
		}
		return stringBuilder.ToString();
	}

	public static void ToggleStream()
	{
		if ((Object)(object)_item == (Object)null || (Object)(object)_inventory == (Object)null)
		{
			Plugin.Log.LogInfo("[ChestDiag] sem alvo — Keypad1 perto do container primeiro.");
			return;
		}
		_streaming = !_streaming;
		_lastSnapshot = "";
		Plugin.Log.LogInfo($"[ChestDiag] streaming = {_streaming}");
	}

	public static void Tick()
	{
		if (_streaming && !((Object)(object)_item == (Object)null) && !((Object)(object)_inventory == (Object)null) && !(Time.time - _lastLog < 0.25f))
		{
			_lastLog = Time.time;
			string text = DumpSlots(_inventory);
			if (text != _lastSnapshot)
			{
				Plugin.Log.LogInfo($"[ChestWatch] t={Time.time:F2} {text}");
				_lastSnapshot = text;
			}
		}
	}

	public static void Reset()
	{
		_item = null;
		_inventory = null;
		_streaming = false;
		_lastSnapshot = "";
	}
}
