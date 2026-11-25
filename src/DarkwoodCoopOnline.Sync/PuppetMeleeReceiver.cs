using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public sealed class PuppetMeleeReceiver : MonoBehaviour
{
	private void OnTriggerEnter(Collider other)
	{
		//IL_0012: Unknown result type (might be due to invalid IL or missing references)
		//IL_0018: Invalid comparison between Unknown and I4
		MeleeSensor component = ((Component)other).GetComponent<MeleeSensor>();
		if ((Object)(object)component == (Object)null || (int)component.type != 1)
		{
			return;
		}
		float num = component.damage;
		if (!(num <= 0f))
		{
			Plugin.Log.LogInfo($"[PuppetMeleeReceiver] MeleeSensor acertou o puppet de verdade (damage={num}) — repassando pro peer.");
			PlayerDamageSync.RelayHit(num, normalHit: true);
			meleeSensorDelegate onHit = component.onHit;
			if (onHit != null)
			{
				onHit.Invoke();
			}
		}
	}
}
