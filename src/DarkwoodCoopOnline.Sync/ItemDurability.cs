using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class ItemDurability
{
	public static float ToFraction(string type, float absoluteDurability)
	{
		if (float.IsInfinity(absoluteDurability) || float.IsNaN(absoluteDurability))
		{
			Plugin.Log.LogWarning($"[ItemDurability] valor inválido recebido pra '{type}' (durability={absoluteDurability}) - " + "corrigindo pra fração cheia (1.0) em vez de propagar o valor quebrado.");
			return 1f;
		}
		InvItem val = (((Object)(object)Singleton<ItemsDatabase>.Instance != (Object)null) ? Singleton<ItemsDatabase>.Instance.getItem(type, false) : null);
		float num = (((Object)(object)val != (Object)null) ? val.maxDurability : 0f);
		if (!(num > 0f))
		{
			return 1f;
		}
		return absoluteDurability / num;
	}
}
