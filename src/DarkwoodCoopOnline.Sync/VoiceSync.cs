using System;
using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class VoiceSync
{
	private const int SampleRate = 16000;

	private const float SendInterval = 0.05f;

	private const int MicBufferSeconds = 1;

	private const float StartRetryInterval = 2f;

	private const float VadThreshold = 0.004f;

	private const float VadHangoverSeconds = 0.3f;

	private const float DiagLogInterval = 2f;

	private const float FullVolumeDistance = 300f;

	private const float SilentDistance = 1500f;

	private const float NativeRolloffNeutralDistance = 100000f;

	private const int OcclusionLayerMask = 32769;

	private const float OcclusionCutoffFrequency = 1500f;

	private const float OcclusionVolumeMultiplier = 0.75f;

	private const float LoudVoiceRmsDefault = 0.15f;

	private const float MinVoiceAlertRadius = 0f;

	private const float MaxVoiceAlertRadius = 350f;

	private static bool _recording;

	private static float _nextStartAttempt = float.MinValue;

	private static AudioClip _micClip;

	private static int _micReadPos;

	private static float _sendTimer;

	private static float _hangoverTimer;

	private static readonly List<short> _pendingSend = new List<short>();

	private static float _diagNextLogAt = float.MinValue;

	private static float _diagPeakRms;

	private static int _diagPacketsSent;

	private static long _diagBytesSent;

	private static float _recvDiagNextLogAt = float.MinValue;

	private static int _recvDiagPacketsThisWindow;

	private static long _recvDiagBytesThisWindow;

	private static float _alertDiagNextLogAt = float.MinValue;











	public static bool AlertEnabled { get; set; }

	public static float LoudVoiceRms { get; set; } = 0.15f;


	public static float LastAlertRms { get; private set; }

	public static bool Enabled { get; private set; }

	public static string Device { get; private set; } = "";


	public static bool IsSpeaking { get; private set; }

	public static void Configure(bool enabled, string device)
	{
		Device = device ?? "";
		SetEnabled(enabled);
	}

	public static void SetEnabled(bool enabled)
	{
		if (Enabled != enabled)
		{
			Enabled = enabled;
			Plugin.Log.LogInfo("[VoiceSync] voice chat " + (enabled ? "ligado" : "desligado") + ".");
			if (!Enabled)
			{
				StopRecording();
			}
		}
	}

	public static void SetDevice(string device)
	{
		device = device ?? "";
		if (!(Device == device))
		{
			if (_recording)
			{
				StopRecording();
			}
			Device = device;
			Plugin.Log.LogInfo("[VoiceSync] microfone trocado pra '" + Device + "'.");
		}
	}

	public static byte[] PollOutgoing()
	{
		if (!Enabled)
		{
			return null;
		}
		if (!_recording)
		{
			if (Time.time < _nextStartAttempt)
			{
				return null;
			}
			StartRecording();
			if (!_recording)
			{
				_nextStartAttempt = Time.time + 2f;
				return null;
			}
		}
		ReadMicSamples();
		_sendTimer += Time.deltaTime;
		if (_sendTimer < 0.05f)
		{
			return null;
		}
		_sendTimer = 0f;
		if (_pendingSend.Count == 0)
		{
			IsSpeaking = false;
			return null;
		}
		byte[] array = new byte[_pendingSend.Count * 2];
		Buffer.BlockCopy(_pendingSend.ToArray(), 0, array, 0, array.Length);
		_pendingSend.Clear();
		_diagPacketsSent++;
		_diagBytesSent += array.Length;
		return PacketWriter.Begin(PacketType.VoiceData).Bytes(array).Finish();
	}

	private static void ReadMicSamples()
	{
		int position = Microphone.GetPosition(Device);
		bool flag = position != _micReadPos;
		if (flag)
		{
			int samples = _micClip.samples;
			int num = position - _micReadPos;
			if (num < 0)
			{
				num += samples;
			}
			if (num > 0)
			{
				float[] array = new float[num];
				if (_micReadPos + num <= samples)
				{
					_micClip.GetData(array, _micReadPos);
				}
				else
				{
					float[] array2 = new float[samples];
					_micClip.GetData(array2, 0);
					int num2 = samples - _micReadPos;
					Array.Copy(array2, _micReadPos, array, 0, num2);
					Array.Copy(array2, 0, array, num2, num - num2);
				}
				_micReadPos = position;
				float num3 = 0f;
				for (int i = 0; i < array.Length; i++)
				{
					num3 += array[i] * array[i];
				}
				float num4 = Mathf.Sqrt(num3 / (float)array.Length);
				if (num4 > _diagPeakRms)
				{
					_diagPeakRms = num4;
				}
				if (num4 >= 0.004f)
				{
					_hangoverTimer = 0.3f;
				}
				else
				{
					_hangoverTimer -= Time.deltaTime;
				}
				if (_hangoverTimer > 0f)
				{
					IsSpeaking = true;
					for (int j = 0; j < array.Length; j++)
					{
						_pendingSend.Add((short)(Mathf.Clamp(array[j], -1f, 1f) * 32767f));
					}
				}
			}
		}
		else
		{
			_hangoverTimer -= Time.deltaTime;
		}
		if (Time.time >= _diagNextLogAt)
		{
			_diagNextLogAt = Time.time + 2f;
			Plugin.Log.LogInfo($"[VoiceSync][diag] mic='{Device}' avancando={flag} picoRMS={_diagPeakRms:F4} " + $"limiar={0.004f:F4} falando={IsSpeaking} pacotesEnviados={_diagPacketsSent} bytesEnviados={_diagBytesSent}");
			_diagPeakRms = 0f;
		}
	}

	private static void StartRecording()
	{
		string[] devices = Microphone.devices;
		if (devices.Length == 0)
		{
			Plugin.Log.LogWarning("[VoiceSync] nenhum microfone encontrado (Microphone.devices vazio).");
			return;
		}
		string text2 = (Device = ((Array.IndexOf(devices, Device) >= 0) ? Device : devices[0]));
		_micClip = Microphone.Start(text2, true, 1, 16000);
		if ((Object)(object)_micClip == (Object)null)
		{
			Plugin.Log.LogWarning("[VoiceSync] falha ao abrir o microfone '" + text2 + "'.");
			return;
		}
		_micReadPos = 0;
		_hangoverTimer = 0f;
		_recording = true;
		Plugin.Log.LogInfo($"[VoiceSync] gravando do microfone '{text2}' @ {16000}Hz.");
	}

	private static void StopRecording()
	{
		if (_recording)
		{
			Microphone.End(Device);
			_recording = false;
			_pendingSend.Clear();
			IsSpeaking = false;
			Plugin.Log.LogInfo("[VoiceSync] gravação do microfone encerrada.");
		}
	}

	// One playback chain per remote player: their own 3D AudioSource on their puppet, ring buffer and filters
	private sealed class Speaker
	{
		public int Slot;

		public GameObject Go;

		public AudioSource Source;

		public AudioClip Clip;

		public AudioReverbFilter Reverb;

		public AudioLowPassFilter LowPass;

		public readonly object RingLock = new object();

		public readonly float[] Ring = new float[32000];

		public int RingWrite;

		public int RingRead;

		public int RingCount;

		public float OcclusionTimer;

		public bool Occluded;

		public int PacketsThisWindow;

		public long BytesThisWindow;

		public void OnAudioRead(float[] data)
		{
			lock (RingLock)
			{
				int num = Mathf.Min(data.Length, RingCount);
				for (int i = 0; i < num; i++)
				{
					data[i] = Ring[RingRead];
					RingRead = (RingRead + 1) % Ring.Length;
				}
				RingCount -= num;
				for (int j = num; j < data.Length; j++)
				{
					data[j] = 0f;
				}
			}
		}
	}

	private static readonly Dictionary<int, Speaker> _speakers = new Dictionary<int, Speaker>();

	public static void ApplyVoiceData(PacketReader r, RemotePlayerPuppet peerPuppet)
	{
		byte[] array = r.Bytes();
		if (array.Length == 0 || (Object)(object)peerPuppet == (Object)null)
		{
			return;
		}
		Speaker speaker = EnsurePlayback(peerPuppet);
		if (speaker == null)
		{
			return;
		}
		int num = array.Length / 2;
		short[] array2 = new short[num];
		Buffer.BlockCopy(array, 0, array2, 0, num * 2);
		float num2 = 0f;
		lock (speaker.RingLock)
		{
			float[] ring = speaker.Ring;
			for (int i = 0; i < num; i++)
			{
				float num3 = (float)array2[i] / 32767f;
				num2 += num3 * num3;
				ring[speaker.RingWrite] = num3;
				speaker.RingWrite = (speaker.RingWrite + 1) % ring.Length;
				if (speaker.RingCount < ring.Length)
				{
					speaker.RingCount++;
				}
				else
				{
					speaker.RingRead = (speaker.RingRead + 1) % ring.Length;
				}
			}
		}
		if (AlertEnabled && num > 0)
		{
			float num5 = (LastAlertRms = Mathf.Sqrt(num2 / (float)num));
			float num6 = Mathf.Clamp01((num5 - 0.004f) / (LoudVoiceRms - 0.004f));
			float num7 = Mathf.Lerp(0f, 350f, num6);
			if (num7 > 0f)
			{
				Character.alertInArea(((Component)peerPuppet).transform.position, num7, false, 1f, false);
			}
		}
		speaker.PacketsThisWindow++;
		speaker.BytesThisWindow += array.Length;
	}

	public static void Tick(IReadOnlyList<RemotePlayerPuppet> puppets)
	{
		bool flag = Time.time >= _recvDiagNextLogAt;
		if (flag)
		{
			_recvDiagNextLogAt = Time.time + 10f;
		}
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		for (int i = 0; i < puppets.Count; i++)
		{
			RemotePlayerPuppet remotePlayerPuppet = puppets[i];
			if ((Object)(object)remotePlayerPuppet == (Object)null || !_speakers.TryGetValue(remotePlayerPuppet.Slot, out var value) || (Object)(object)value.Source == (Object)null)
			{
				continue;
			}
			Transform transform = ((Component)remotePlayerPuppet).transform;
			float num = Vector3.Distance(((Component)instance).transform.position, transform.position);
			if (flag && value.PacketsThisWindow > 0)
			{
				Plugin.Log.LogInfo($"[VoiceSync] '{remotePlayerPuppet.PeerName}': {value.PacketsThisWindow} packets / {value.BytesThisWindow} bytes in 10s, dist={num:F0}");
				value.PacketsThisWindow = 0;
				value.BytesThisWindow = 0L;
			}
			float num2 = 1f - Mathf.Clamp01((num - 300f) / 1200f);
			((CharBase)remotePlayerPuppet).checkGround();
			if ((Object)(object)value.Reverb != (Object)null)
			{
				((Behaviour)value.Reverb).enabled = ((CharBase)remotePlayerPuppet).isInside;
			}
			// walls muffle the voice; the raycast only runs 10 times a second per speaker
			value.OcclusionTimer -= Time.unscaledDeltaTime;
			if ((Object)(object)value.LowPass != (Object)null && value.OcclusionTimer <= 0f)
			{
				value.OcclusionTimer = 0.1f;
				Vector3 position = transform.position;
				Vector3 position2 = ((Component)instance).transform.position;
				float num3 = Core.trueDistance(position, position2);
				if (num3 <= 0f)
				{
					num3 = 1f;
				}
				RaycastHit val = default(RaycastHit);
				value.Occluded = Physics.Raycast(new Ray(position, position2 - position), out val, num3, 32769);
				((Behaviour)value.LowPass).enabled = value.Occluded;
				if (value.Occluded)
				{
					value.LowPass.cutoffFrequency = 1500f;
				}
			}
			value.Source.volume = (value.Occluded ? (num2 * 0.75f) : num2);
		}
	}

	private static Speaker EnsurePlayback(RemotePlayerPuppet puppet)
	{
		if (_speakers.TryGetValue(puppet.Slot, out var value) && (Object)(object)value.Go != (Object)null)
		{
			return value;
		}
		value = new Speaker
		{
			Slot = puppet.Slot
		};
		value.Go = new GameObject("VoiceChatPlayback_" + puppet.Slot);
		value.Go.transform.SetParent(((Component)puppet).transform, false);
		value.Go.transform.localPosition = Vector3.zero;
		value.Source = value.Go.AddComponent<AudioSource>();
		value.Source.spatialBlend = 1f;
		value.Source.rolloffMode = (AudioRolloffMode)1;
		value.Source.minDistance = 1f;
		value.Source.maxDistance = 100000f;
		value.Source.loop = true;
		value.Source.playOnAwake = false;
		value.Clip = AudioClip.Create("VoicePeer" + puppet.Slot, 16000, 1, 16000, true, new AudioClip.PCMReaderCallback(value.OnAudioRead));
		value.Source.clip = value.Clip;
		value.Source.Play();
		value.Reverb = value.Go.AddComponent<AudioReverbFilter>();
		((Behaviour)value.Reverb).enabled = false;
		value.LowPass = value.Go.AddComponent<AudioLowPassFilter>();
		value.LowPass.cutoffFrequency = 1500f;
		((Behaviour)value.LowPass).enabled = false;
		_speakers[puppet.Slot] = value;
		Plugin.Log.LogInfo($"[VoiceSync] first voice packet from '{puppet.PeerName}' - speaker created on their puppet.");
		return value;
	}

	public static void OnPeerLeft(int slot)
	{
		if (_speakers.TryGetValue(slot, out var value))
		{
			if ((Object)(object)value.Go != (Object)null)
			{
				Object.Destroy((Object)(object)value.Go);
			}
			_speakers.Remove(slot);
		}
	}

	public static void Reset()
	{
		StopRecording();
		_diagPeakRms = 0f;
		_diagPacketsSent = 0;
		_diagBytesSent = 0L;
		_recvDiagPacketsThisWindow = 0;
		_recvDiagBytesThisWindow = 0L;
		foreach (Speaker value in _speakers.Values)
		{
			if ((Object)(object)value.Go != (Object)null)
			{
				Object.Destroy((Object)(object)value.Go);
			}
		}
		_speakers.Clear();
		IsSpeaking = false;
	}
}
