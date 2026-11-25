using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class LampDiagnostics
{
	public static void DumpAllItemTypes()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogInfo("[ItemTypeDump] Player null");
			return;
		}
		Plugin.Log.LogInfo("========================= [ItemTypeDump] =========================");
		DumpInventory("HOTBAR", instance.Hotbar);
		DumpInventory("INVENTORY", instance.Inventory);
		Plugin.Log.LogInfo("====================================================================");
	}

	private static void DumpInventory(string label, Inventory inv)
	{
		if ((Object)(object)inv == (Object)null)
		{
			Plugin.Log.LogInfo("[ItemTypeDump] " + label + ": inventory null");
			return;
		}
		if (inv.slots == null)
		{
			Plugin.Log.LogInfo("[ItemTypeDump] " + label + ": slots null");
			return;
		}
		int num = 0;
		for (int i = 0; i < inv.slots.Count; i++)
		{
			InvItemClass invItem = inv.slots[i].invItem;
			if (!InvItemClass.isNull(invItem))
			{
				num++;
				Plugin.Log.LogInfo($"[ItemTypeDump] {label}[slot {i}]: type='{invItem.type}' activated={invItem.activated} " + string.Format("amount={0} baseClass.name='{1}'", invItem.amount, ((Object)(object)invItem.baseClass != (Object)null) ? ((Object)invItem.baseClass).name : "NULL"));
			}
		}
		if (num == 0)
		{
			Plugin.Log.LogInfo($"[ItemTypeDump] {label}: (vazio - {inv.slots.Count} slot(s), nenhum item)");
		}
	}
}
