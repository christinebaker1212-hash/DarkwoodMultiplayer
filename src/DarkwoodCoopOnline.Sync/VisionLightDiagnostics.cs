using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class VisionLightDiagnostics
{
	private static bool _streaming;

	private static float _lastLog = -999f;

	private const float LogInterval = 0.125f;

	internal const int RoofCheckMask = 16809985;

	private static bool _lastConeEnabled;

	private static bool _lastDotEnabled;

	private static bool _everLogged;

	public static bool Streaming => _streaming;

	public static void ToggleStream()
	{
		_streaming = !_streaming;
		_everLogged = false;
		Plugin.Log.LogInfo($"[VisionLightWatch] streaming = {_streaming}");
		if (_streaming)
		{
			Plugin.Log.LogInfo("[VisionLightWatch] t | cone(angle/intensity/enabled) | dot(radius/intensity/enabled) | distPeer | sharedVisionOn | aiming | moving  -- só loga a cada MUDANÇA de enabled (raro) + o teste de 'teto' toda vez que ele acerta algo (ainda mais raro).");
		}
	}

	public static void Tick()
	{
		if (!_streaming)
		{
			return;
		}
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Transform val = ((Component)instance).transform.Find("PlayerFOVLight");
		Light2D val2 = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<Light2D>() : null);
		Light2D fOVDot = instance.FOVDot;
		RaycastHit val3 = default(RaycastHit);
		if ((Object)(object)fOVDot != (Object)null && Physics.Raycast(((Component)fOVDot).transform.position + Vector3.up * 100f, Vector3.down, out val3, 200f, 16809985))
		{
			RemotePlayerPuppet remotePlayerPuppet = (CoopRuntime.SessionActive ? CoopRuntime.Session.Puppet : null);
			bool flag = (Object)(object)remotePlayerPuppet != (Object)null && (Object)(object)((Component)val3.collider).transform.root == (Object)(object)((Component)remotePlayerPuppet).transform;
			Plugin.Log.LogInfo("[VisionLightWatch][ROOF-CHECK] ACERTOU '" + ((Object)((Component)val3.collider).gameObject).name + "' (layer=" + LayerMask.LayerToName(((Component)val3.collider).gameObject.layer) + ", raiz='" + ((Object)((Component)val3.collider).transform.root).name + "') " + $"dist={val3.distance:F1} - isto força o renderer da luz a DESLIGAR agora (Light2D nativo)." + (flag ? " *** É O PUPPET DO PEER - confirma a hipótese! ***" : ""));
		}
		if (!(Time.time - _lastLog < 0.125f))
		{
			_lastLog = Time.time;
			bool flag2 = (Object)(object)val2 != (Object)null && val2.LightEnabled;
			bool flag3 = (Object)(object)fOVDot != (Object)null && fOVDot.LightEnabled;
			if (!_everLogged || flag2 != _lastConeEnabled || flag3 != _lastDotEnabled)
			{
				_everLogged = true;
				_lastConeEnabled = flag2;
				_lastDotEnabled = flag3;
				string text = (((Object)(object)val2 != (Object)null) ? $"angle={val2.LightConeAngle:F0} intensity={val2.LightIntensity:F2} enabled={val2.LightEnabled} visible={val2.IsVisible}" : "(PlayerFOVLight não encontrado)");
				string text2 = (((Object)(object)fOVDot != (Object)null) ? $"radius={fOVDot.LightRadius:F1} intensity={fOVDot.LightIntensity:F2} enabled={fOVDot.LightEnabled} visible={fOVDot.IsVisible}" : "(FOVDot null)");
				RemotePlayerPuppet remotePlayerPuppet2 = (CoopRuntime.SessionActive ? CoopRuntime.Session.Puppet : null);
				string text3 = (((Object)(object)remotePlayerPuppet2 != (Object)null) ? Vector3.Distance(((Component)instance).transform.position, ((Component)remotePlayerPuppet2).transform.position).ToString("F0") : "(sem peer)");
				Plugin.Log.LogInfo($"[VisionLightWatch] t={Time.time:F2} cone[{text}] dot[{text2}] distPeer={text3} " + $"sharedVisionOn={SharedVisionSync.Enabled} aiming={instance.aiming} walking={instance.walking} running={instance.running}");
			}
		}
	}

	public static void Reset()
	{
		_streaming = false;
		_everLogged = false;
	}
}
