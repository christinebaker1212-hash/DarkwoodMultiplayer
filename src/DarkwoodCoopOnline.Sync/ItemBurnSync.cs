using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class ItemBurnSync
{
	private const float MatchRadius = 250f;

	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Expected O, but got Unknown
		//IL_0069: Unknown result type (might be due to invalid IL or missing references)
		//IL_0076: Expected O, but got Unknown
		//IL_00cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d8: Expected O, but got Unknown
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Flame), "onCollideWith", new Type[3]
			{
				typeof(Transform),
				typeof(Collider),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(ItemBurnSync), "IgnitePostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Item), "getHit", new Type[3]
			{
				typeof(int),
				typeof(Transform),
				typeof(bool)
			}, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(ItemBurnSync), "DestroyPostfix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void IgnitePostfix(Collider _collider)
	{
		//IL_005b: Unknown result type (might be due to invalid IL or missing references)
		if (CoopRuntime.SessionActive && !((Object)(object)_collider == (Object)null))
		{
			Item component = ((Component)_collider).GetComponent<Item>();
			if (!((Object)(object)component == (Object)null) && component.destructible && !((Object)(object)((Component)component).GetComponent<Burn>() == (Object)null))
			{
				CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ItemIgnited).Str(((Object)((Component)component).gameObject).name).Vec3(((Component)component).transform.position)
					.Finish());
			}
		}
	}

	public static void ApplyIgnited(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 pos = r.Vec3();
		Item val = FindNearest(text, pos);
		if (!((Object)(object)val == (Object)null) && !((Object)(object)((Component)val).GetComponent<Burn>() != (Object)null))
		{
			((Component)val).gameObject.AddComponent<Burn>().burnTime = 20f;
			Plugin.Log.LogInfo("[ItemBurnSync] '" + text + "' pegando fogo pelo peer - repliquei a chama aqui.");
		}
	}

	private static void DestroyPostfix(Item __instance)
	{
		//IL_003b: Unknown result type (might be due to invalid IL or missing references)
		if (CoopRuntime.SessionActive && !((Object)(object)__instance == (Object)null) && __instance.destroyed)
		{
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ItemDestroyed).Str(((Object)((Component)__instance).gameObject).name).Vec3(((Component)__instance).transform.position)
				.Finish());
		}
	}

	public static void ApplyDestroyed(PacketReader r)
	{
		//IL_0008: Unknown result type (might be due to invalid IL or missing references)
		//IL_000d: Unknown result type (might be due to invalid IL or missing references)
		//IL_000f: Unknown result type (might be due to invalid IL or missing references)
		string text = r.Str();
		Vector3 pos = r.Vec3();
		Item val = FindNearest(text, pos);
		if (!((Object)(object)val == (Object)null) && !val.destroyed)
		{
			val.die();
			Plugin.Log.LogInfo("[ItemBurnSync] '" + text + "' destruído pelo peer - repliquei aqui.");
		}
	}

	private static Item FindNearest(string name, Vector3 pos)
	{
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		Item result = null;
		float num = 250f;
		Item[] array = SceneCache.Items();
		foreach (Item val in array)
		{
			if (!((Object)(object)val == (Object)null) && !(((Object)((Component)val).gameObject).name != name))
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, pos);
				if (num2 < num)
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}
}
