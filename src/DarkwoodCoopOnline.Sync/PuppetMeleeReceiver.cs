using DarkwoodCoopOnline;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public sealed class PuppetMeleeReceiver : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		MeleeSensor component = ((Component)other).GetComponent<MeleeSensor>();
		if ((Object)(object)component == (Object)null || (int)component.type != 1)
		{
			return;
		}
		float num = component.damage;
		if (!(num <= 0f))
		{
			Plugin.Log.LogInfo($"[PuppetMeleeReceiver] MeleeSensor acertou o puppet de verdade (damage={num}) — repassando pro peer.");
			RemotePlayerPuppet componentInParent = ((Component)this).GetComponentInParent<RemotePlayerPuppet>();
			if ((Object)(object)componentInParent != (Object)null)
			{
				PlayerDamageSync.RelayHit(num, normalHit: true, componentInParent.Slot);
			}
			meleeSensorDelegate onHit = component.onHit;
			if (onHit != null)
			{
				onHit.Invoke();
			}
		}
	}
}
