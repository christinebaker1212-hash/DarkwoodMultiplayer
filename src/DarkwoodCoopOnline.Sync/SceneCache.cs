using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class SceneCache
{
	private class Cache<T>
	{
		public T[] items;

		public float nextScanAt = float.MinValue;
	}

	private const float Interval = 5f;

	private const float DoorPhase = 0f;

	private const float ItemPhase = 1.25f;

	private const float TriggerPhase = 2.5f;

	private const float CharacterPhase = 3.75f;

	private const float WindowPhase = 1f;

	private const float GeneratorPhase = 4.5f;

	private const float SawPhase = 0.6f;

	private const float LockedPhase = 2f;

	private const float PadlockPhase = 3.25f;

	private const float ShadowPhase = 4.75f;

	private const float ConstructiblePhase = 0.3f;

	private const float NpcPhase = 4.25f;

	private const float WorkbenchPhase = 3f;

	private const float InSightPhase = 1.75f;

	private static readonly Cache<Door> _doors = new Cache<Door>();

	private static readonly Cache<Item> _items = new Cache<Item>();

	private static readonly Cache<Trigger> _triggers = new Cache<Trigger>();

	private static readonly Cache<Character> _characters = new Cache<Character>();

	private static readonly Cache<Window> _windows = new Cache<Window>();

	private static readonly Cache<Generator> _generators = new Cache<Generator>();

	private static readonly Cache<Saw> _saws = new Cache<Saw>();

	private static readonly Cache<Locked> _locks = new Cache<Locked>();

	private static readonly Cache<Padlock> _padlocks = new Cache<Padlock>();

	private static readonly Cache<ShadowCreature> _shadows = new Cache<ShadowCreature>();

	private static readonly Cache<Constructible> _constructibles = new Cache<Constructible>();

	private static readonly Cache<NPC> _npcs = new Cache<NPC>();

	private static readonly Cache<Workbench> _workbenches = new Cache<Workbench>();

	private static readonly Cache<InSightOfPlayer> _inSights = new Cache<InSightOfPlayer>();

	public static Door[] Doors()
	{
		return Get<Door>(_doors, 0f);
	}

	public static Item[] Items()
	{
		return Get<Item>(_items, 1.25f);
	}

	public static Trigger[] Triggers()
	{
		return Get<Trigger>(_triggers, 2.5f);
	}

	public static Character[] Characters()
	{
		return Get<Character>(_characters, 3.75f);
	}

	public static Window[] Windows()
	{
		return Get<Window>(_windows, 1f);
	}

	public static Generator[] Generators()
	{
		return Get<Generator>(_generators, 4.5f);
	}

	public static Saw[] Saws()
	{
		return Get<Saw>(_saws, 0.6f);
	}

	public static Locked[] Locks()
	{
		return Get<Locked>(_locks, 2f);
	}

	public static Padlock[] Padlocks()
	{
		return Get<Padlock>(_padlocks, 3.25f);
	}

	public static ShadowCreature[] Shadows()
	{
		return Get<ShadowCreature>(_shadows, 4.75f);
	}

	public static Constructible[] Constructibles()
	{
		return Get<Constructible>(_constructibles, 0.3f);
	}

	public static NPC[] Npcs()
	{
		return Get<NPC>(_npcs, 4.25f);
	}

	public static Workbench[] Workbenches()
	{
		return Get<Workbench>(_workbenches, 3f);
	}

	public static InSightOfPlayer[] InSightObjects()
	{
		return Get<InSightOfPlayer>(_inSights, 1.75f);
	}

	private static T[] Get<T>(Cache<T> c, float phase) where T : Object
	{
		if (c.items == null)
		{
			c.items = Object.FindObjectsOfType<T>();
			c.nextScanAt = Time.time + 5f + phase;
			return c.items;
		}
		if (Time.time >= c.nextScanAt)
		{
			c.items = Object.FindObjectsOfType<T>();
			c.nextScanAt = Time.time + 5f;
		}
		return c.items;
	}

	// Every Transform in the scene, active or not. Scanning them all is slow on Darkwood's maps, so the
	// result is reused for up to 10 seconds; FindTransform rescans once if the cached list misses.
	private static Transform[] _allTransforms;

	private static float _allTransformsAt = float.MinValue;

	public static Transform[] AllTransforms(bool fresh = false)
	{
		if (fresh || _allTransforms == null || Time.unscaledTime - _allTransformsAt > 10f)
		{
			_allTransforms = Object.FindObjectsOfType<Transform>(true);
			_allTransformsAt = Time.unscaledTime;
		}
		return _allTransforms;
	}

	// The transform named `name` nearest to pos (within maxDist), or null
	public static Transform FindTransform(System.Func<Transform, bool> match, Vector3 pos, float maxDist)
	{
		Transform transform = Nearest(AllTransforms(), match, pos, maxDist);
		if ((Object)(object)transform == (Object)null && Time.unscaledTime - _allTransformsAt > 0.5f)
		{
			transform = Nearest(AllTransforms(fresh: true), match, pos, maxDist);
		}
		return transform;
	}

	private static Transform Nearest(Transform[] all, System.Func<Transform, bool> match, Vector3 pos, float maxDist)
	{
		Transform result = null;
		float num = maxDist;
		foreach (Transform transform in all)
		{
			if (!((Object)(object)transform == (Object)null) && match(transform))
			{
				float num2 = Vector3.Distance(transform.position, pos);
				if (num2 < num)
				{
					num = num2;
					result = transform;
				}
			}
		}
		return result;
	}

	public static void ForceRescanCharacters()
	{
		_characters.nextScanAt = float.MinValue;
	}

	public static void Reset()
	{
		_doors.items = null;
		_doors.nextScanAt = float.MinValue;
		_items.items = null;
		_items.nextScanAt = float.MinValue;
		_triggers.items = null;
		_triggers.nextScanAt = float.MinValue;
		_characters.items = null;
		_characters.nextScanAt = float.MinValue;
		_windows.items = null;
		_windows.nextScanAt = float.MinValue;
		_generators.items = null;
		_generators.nextScanAt = float.MinValue;
		_saws.items = null;
		_saws.nextScanAt = float.MinValue;
		_locks.items = null;
		_locks.nextScanAt = float.MinValue;
		_padlocks.items = null;
		_padlocks.nextScanAt = float.MinValue;
		_shadows.items = null;
		_shadows.nextScanAt = float.MinValue;
		_constructibles.items = null;
		_constructibles.nextScanAt = float.MinValue;
		_npcs.items = null;
		_npcs.nextScanAt = float.MinValue;
		_inSights.items = null;
		_inSights.nextScanAt = float.MinValue;
		_allTransforms = null;
		_allTransformsAt = float.MinValue;
	}
}
