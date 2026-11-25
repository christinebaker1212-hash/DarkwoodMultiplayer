using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DurabilityDiagnostics
{
	private struct FuelState
	{
		public float generatorFuelBefore;

		public string itemType;

		public int itemAmount;

		public float itemDurability;

		public bool hadItem;
	}

	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Expected O, but got Unknown
		//IL_0070: Expected O, but got Unknown
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ec: Expected O, but got Unknown
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_013b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0148: Expected O, but got Unknown
		//IL_0148: Expected O, but got Unknown
		//IL_0190: Unknown result type (might be due to invalid IL or missing references)
		//IL_019d: Expected O, but got Unknown
		//IL_01f2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ff: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(InvItemClass), "drainDurability", new Type[1] { typeof(float) }, (Type[])null), new HarmonyMethod(typeof(DurabilityDiagnostics), "DrainPrefix", (Type[])null), new HarmonyMethod(typeof(DurabilityDiagnostics), "DrainPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(InvSlot), "createItem", new Type[5]
			{
				typeof(string),
				typeof(int),
				typeof(float),
				typeof(InvItem.ModifierQuality),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(DurabilityDiagnostics), "CreatePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Generator), "addFuel", new Type[1] { typeof(float) }, (Type[])null), new HarmonyMethod(typeof(DurabilityDiagnostics), "FuelPrefix", (Type[])null), new HarmonyMethod(typeof(DurabilityDiagnostics), "FuelPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(InvItemClass), "getStackedDurability", new Type[2]
			{
				typeof(InvItemClass),
				typeof(int)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(DurabilityDiagnostics), "StackedDurability2Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(InvItemClass), "getStackedDurability", new Type[3]
			{
				typeof(InvItemClass),
				typeof(InvItemClass),
				typeof(int)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(DurabilityDiagnostics), "StackedDurability3Postfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void StackedDurability2Postfix(InvItemClass item, int amountToTransfer, float __result)
	{
		Plugin.Log.LogInfo("[DurabilityDiag] getStackedDurability(item,int) tipo='" + item?.type + "' " + $"amountAtual={item?.amount} durabilityAtual={item?.durability:F2} amountSomado={amountToTransfer} " + $"maxDurability_base={(((Object)(object)item?.baseClass != (Object)null) ? item.baseClass.maxDurability : (-1f)):F2} -> resultado={__result:F2}");
	}

	private static void StackedDurability3Postfix(InvItemClass item1, InvItemClass item2, int amountToTransfer, float __result)
	{
		Plugin.Log.LogInfo("[DurabilityDiag] getStackedDurability(item,item,int) tipo='" + item1?.type + "' " + $"amountAtual={item1?.amount} durabilityAtual={item1?.durability:F2} outroAmount={item2?.amount} " + $"outraDurability={item2?.durability:F2} amountTransferPedido={amountToTransfer} -> resultado={__result:F2}");
	}

	private static void DrainPrefix(InvItemClass __instance, float amountToDrain, out float __state)
	{
		__state = __instance.durability;
	}

	private static void DrainPostfix(InvItemClass __instance, float amountToDrain, float __state)
	{
		bool flag = !InvItemClass.isNull(__instance);
		float num = (flag ? __instance.durability : (-999f));
		float num2 = ((__instance != null && (Object)(object)__instance.baseClass != (Object)null) ? __instance.baseClass.maxDurability : (-1f));
		Plugin.Log.LogInfo($"[DurabilityDiag] drainDurability(pedido={amountToDrain:F2}) tipo='{__instance?.type}' " + $"durability {__state:F2}->{num:F2} (maxDurability_base={num2:F2}) aindaExiste={flag}");
	}

	private static void CreatePostfix(InvSlot __instance, string type, int amount, float durability, InvItem.ModifierQuality modifierQuality, bool isRecipe, InvItemClass __result)
	{
		float num = __result?.durability ?? (-999f);
		float num2 = ((__result != null && (Object)(object)__result.baseClass != (Object)null) ? __result.baseClass.maxDurability : (-1f));
		Plugin.Log.LogInfo($"[DurabilityDiag] InvSlot.createItem tipo='{type}' fraçãoPedida={durability:F3} " + $"amount={amount} -> durability_resultante={num:F2} (maxDurability_base={num2:F2})");
	}

	private static void FuelPrefix(Generator __instance, float addFuelAmount, out FuelState __state)
	{
		__state = default(FuelState);
		__state.generatorFuelBefore = __instance.fuel;
		InvItemClass val = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.currentItem : null);
		__state.hadItem = !InvItemClass.isNull(val);
		if (__state.hadItem)
		{
			__state.itemType = val.type;
			__state.itemAmount = val.amount;
			__state.itemDurability = val.durability;
		}
	}

	private static void FuelPostfix(Generator __instance, float addFuelAmount, FuelState __state)
	{
		InvItemClass val = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.currentItem : null);
		string text = ((!InvItemClass.isNull(val)) ? $"tipo='{val.type}' amount={val.amount} durability={val.durability:F2}" : "(sem item selecionado)");
		string text2 = (__state.hadItem ? $"tipo='{__state.itemType}' amount={__state.itemAmount} durability={__state.itemDurability:F2}" : "(sem item selecionado)");
		Plugin.Log.LogInfo($"[DurabilityDiag] Generator.addFuel(pedido={addFuelAmount:F2}) fuel {__state.generatorFuelBefore:F2}->{__instance.fuel:F2} | " + "item ANTES: " + text2 + " | item DEPOIS: " + text);
	}
}
