using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class SharedVisionSync
{
	private static bool _enabled;

	private static Light2D _clonedCone;

	private static RemotePlayerPuppet _attachedTo;

	public static bool Enabled
	{
		get
		{
			return _enabled;
		}
		set
		{
			if (_enabled != value)
			{
				_enabled = value;
				Plugin.Log.LogInfo("[SharedVisionSync] visão compartilhada " + (_enabled ? "LIGADA" : "desligada") + ".");
				if (!_enabled)
				{
					Cleanup();
				}
			}
		}
	}

	public static void Tick()
	{
		if (!_enabled)
		{
			return;
		}
		RemotePlayerPuppet remotePlayerPuppet = (CoopRuntime.SessionActive ? CoopRuntime.Session.Puppet : null);
		if ((Object)(object)remotePlayerPuppet == (Object)null || !remotePlayerPuppet.HasState)
		{
			Cleanup();
			return;
		}
		EnsureCone(remotePlayerPuppet);
		if ((Object)(object)_clonedCone != (Object)null)
		{
			((Component)_clonedCone).transform.rotation = Quaternion.Euler(90f, remotePlayerPuppet.TargetAim, 0f);
		}
	}

	private static void EnsureCone(RemotePlayerPuppet puppet)
	{
		if ((Object)(object)_clonedCone != (Object)null && (Object)(object)_attachedTo == (Object)(object)puppet)
		{
			return;
		}
		Cleanup();
		Light2D val = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.FOVDot : null);
		if (!((Object)(object)val == (Object)null))
		{
			GameObject obj = Object.Instantiate<GameObject>(((Component)val).gameObject);
			obj.SetActive(false);
			obj.transform.SetParent(((Component)puppet).transform, false);
			obj.transform.localPosition = Vector3.zero;
			Light2D component = obj.GetComponent<Light2D>();
			if ((Object)(object)component != (Object)null)
			{
				component.lightsPlayer = false;
			}
			obj.SetActive(true);
			_clonedCone = component;
			_attachedTo = puppet;
		}
	}

	private static void Cleanup()
	{
		if ((Object)(object)_clonedCone != (Object)null)
		{
			Object.Destroy((Object)(object)((Component)_clonedCone).gameObject);
		}
		_clonedCone = null;
		_attachedTo = null;
	}

	public static void Reset()
	{
		Cleanup();
	}
}
