using System;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class FirearmFxSync
{
	private const float BulletSpeed = 700f;

	private const int HitLayerMask = 18909185;

	private static bool _patched;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			Harmony val = new Harmony(modId);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "fireWeapon", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(FirearmFxSync), "After", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
			val.Patch((MethodBase)AccessTools.Method(typeof(Player), "spawnBullet", new Type[1] { typeof(float) }, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(FirearmFxSync), "AfterSpawnBullet", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void After(Player __instance)
	{
		if (CoopRuntime.SessionActive && !InvItemClass.isNull(__instance.currentItem))
		{
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.FirearmFired).Str(__instance.currentItem.type).Vec3(((Component)__instance).transform.position)
				.Float(((Component)__instance).transform.eulerAngles.y)
				.Finish());
		}
	}

	public static void ApplyFired(PacketReader r)
	{
		string text = r.Str();
		Vector3 val = r.Vec3();
		float num = r.Float();
		InvItem item = Singleton<ItemsDatabase>.Instance.getItem(text, false);
		if ((Object)(object)item == (Object)null)
		{
			return;
		}
		Quaternion val2 = Quaternion.Euler(90f, num, 0f);
		if (!item.noMuzzleFlash)
		{
			Core.AddPrefab("FX/Muzzle/PistolFlash", val, val2, (GameObject)null, true);
		}
		if (item.muzzlePrefab != (Object)null)
		{
			Core.AddPooledPrefab("FX", item.muzzlePrefab.name, val, val2);
		}
		if (item.muzzleParticles != (Object)null)
		{
			Core.AddPooledPrefab("FX", item.muzzleParticles.name, val, val2);
		}
		if (item.item != (Object)null)
		{
			GameObject val3 = Core.AddPrefab(item.item, val, val2, (GameObject)null, false);
			Rigidbody val4 = (((Object)(object)val3 != (Object)null) ? val3.GetComponent<Rigidbody>() : null);
			if ((Object)(object)val4 != (Object)null)
			{
				val4.velocity = val2 * Vector3.up * 700f;
			}
		}
	}

	private static void AfterSpawnBullet(Player __instance)
	{
		if (!CoopRuntime.SessionActive || InvItemClass.isNull(__instance.currentItem) || __instance.currentItem.baseClass.item != (Object)null)
		{
			return;
		}
		Transform transform = ((Component)__instance).transform;
		RaycastHit val = default(RaycastHit);
		if (Physics.Raycast(transform.position + transform.up, transform.up, out val, 1000f, 18909185) && !((Object)(object)val.collider == (Object)null))
		{
			Character component = ((Component)((Component)val.collider).transform).GetComponent<Character>();
			if ((Object)(object)component == (Object)null && (Object)(object)((Component)val.collider).transform.parent != (Object)null)
			{
				component = ((Component)((Component)val.collider).transform.parent).GetComponent<Character>();
			}
			bool flag = (Object)(object)component != (Object)null && !((CharBase)component).isUnderwater && !component.ethereal;
			bool v = flag && ((CharBase)component).inWater;
			CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.BulletImpact).Vec3(val.point).Float(transform.eulerAngles.y)
				.Bool(flag)
				.Bool(v)
				.Finish());
		}
	}

	public static void ApplyImpact(PacketReader r)
	{
		Vector3 val = r.Vec3();
		float num = r.Float();
		bool num2 = r.Bool();
		bool flag = r.Bool();
		if (num2)
		{
			Core.AddPrefab(flag ? "FX/Bloodsplats/Shotsplat" : "FX/Bloodsplats/Shotsplat_stay", val, Quaternion.Euler(90f, num + UnityEngine.Random.Range(-20f, 20f), 0f), (GameObject)null, false);
		}
		else
		{
			Core.AddPooledPrefab("FX", "bullet_hit_1", val, Quaternion.Euler(90f, num, 0f));
		}
	}
}
