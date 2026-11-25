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

	private static GameObject _playbackGO;

	private static AudioSource _playbackSource;

	private static AudioClip _playbackClip;

	private static AudioReverbFilter _reverbFilter;

	private static AudioLowPassFilter _lowPassFilter;

	private static readonly object _ringLock = new object();

	private static float[] _ring;

	private static int _ringWrite;

	private static int _ringRead;

	private static int _ringCount;

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

	public static void ApplyVoiceData(PacketReader r, RemotePlayerPuppet peerPuppet)
	{
		//IL_0175: Unknown result type (might be due to invalid IL or missing references)
		byte[] array = r.Bytes();
		if (array.Length == 0)
		{
			return;
		}
		bool flag = (Object)(object)_playbackSource == (Object)null;
		EnsurePlayback(((Object)(object)peerPuppet != (Object)null) ? ((Component)peerPuppet).transform : null);
		if ((Object)(object)_playbackSource == (Object)null)
		{
			return;
		}
		if (flag)
		{
			Plugin.Log.LogInfo("[VoiceSync][diag] 1º pacote de voz do peer recebido - AudioSource criado no puppet.");
		}
		int num = array.Length / 2;
		short[] array2 = new short[num];
		Buffer.BlockCopy(array, 0, array2, 0, array.Length);
		float num2 = 0f;
		lock (_ringLock)
		{
			for (int i = 0; i < num; i++)
			{
				float num3 = (float)array2[i] / 32767f;
				num2 += num3 * num3;
				_ring[_ringWrite] = num3;
				_ringWrite = (_ringWrite + 1) % _ring.Length;
				if (_ringCount < _ring.Length)
				{
					_ringCount++;
				}
				else
				{
					_ringRead = (_ringRead + 1) % _ring.Length;
				}
			}
		}
		if (AlertEnabled && (Object)(object)peerPuppet != (Object)null && num > 0)
		{
			float num5 = (LastAlertRms = Mathf.Sqrt(num2 / (float)num));
			float num6 = Mathf.Clamp01((num5 - 0.004f) / (LoudVoiceRms - 0.004f));
			float num7 = Mathf.Lerp(0f, 350f, num6);
			if (num7 > 0f)
			{
				Character.alertInArea(((Component)peerPuppet).transform.position, num7, false, 1f, false);
			}
			if (Time.time >= _alertDiagNextLogAt)
			{
				_alertDiagNextLogAt = Time.time + 2f;
				Plugin.Log.LogInfo($"[VoiceSync][diag] alerta por voz: rms={num5:F4} raio={num7:F0} limiar={LoudVoiceRms:F3}");
			}
		}
		_recvDiagPacketsThisWindow++;
		_recvDiagBytesThisWindow += array.Length;
	}

	public static void Tick(RemotePlayerPuppet peerPuppet)
	{
		//IL_0170: Unknown result type (might be due to invalid IL or missing references)
		//IL_0176: Unknown result type (might be due to invalid IL or missing references)
		//IL_0071: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01de: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e5: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0202: Unknown result type (might be due to invalid IL or missing references)
		//IL_0204: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_020b: Unknown result type (might be due to invalid IL or missing references)
		//IL_020d: Unknown result type (might be due to invalid IL or missing references)
		//IL_020f: Unknown result type (might be due to invalid IL or missing references)
		Transform val = (((Object)(object)peerPuppet != (Object)null) ? ((Component)peerPuppet).transform : null);
		if (Time.time >= _recvDiagNextLogAt)
		{
			_recvDiagNextLogAt = Time.time + 2f;
			if (_recvDiagPacketsThisWindow > 0 || (Object)(object)_playbackSource != (Object)null)
			{
				float num = (((Object)(object)val != (Object)null && (Object)(object)Player.Instance != (Object)null) ? Vector3.Distance(((Component)Player.Instance).transform.position, val.position) : (-1f));
				Plugin.Log.LogInfo($"[VoiceSync][diag] recebido: pacotes={_recvDiagPacketsThisWindow} bytes={_recvDiagBytesThisWindow} " + string.Format("volumeAtual={0} distPeer={1:F0} ", ((Object)(object)_playbackSource != (Object)null) ? _playbackSource.volume.ToString("F2") : "n/a", num) + $"dentro={((CharBase)peerPuppet)?.isInside} abafado={(Object)(object)_lowPassFilter != (Object)null && ((Behaviour)_lowPassFilter).enabled}");
			}
			_recvDiagPacketsThisWindow = 0;
			_recvDiagBytesThisWindow = 0L;
		}
		if ((Object)(object)_playbackSource == (Object)null || (Object)(object)val == (Object)null || (Object)(object)Player.Instance == (Object)null)
		{
			return;
		}
		float num2 = Vector3.Distance(((Component)Player.Instance).transform.position, val.position);
		float num3 = 1f - Mathf.Clamp01((num2 - 300f) / 1200f);
		((CharBase)peerPuppet).checkGround();
		if ((Object)(object)_reverbFilter != (Object)null)
		{
			((Behaviour)_reverbFilter).enabled = ((CharBase)peerPuppet).isInside;
		}
		bool flag = false;
		if ((Object)(object)_lowPassFilter != (Object)null)
		{
			Vector3 position = val.position;
			Vector3 position2 = ((Component)Player.Instance).transform.position;
			float num4 = Core.trueDistance(position, position2);
			if (num4 <= 0f)
			{
				num4 = 1f;
			}
			Vector3 val2 = position2 - position;
			RaycastHit val3 = default(RaycastHit);
			flag = Physics.Raycast(new Ray(position, val2), out val3, num4, 32769);
			((Behaviour)_lowPassFilter).enabled = flag;
			if (flag)
			{
				_lowPassFilter.cutoffFrequency = 1500f;
			}
		}
		_playbackSource.volume = (flag ? (num3 * 0.75f) : num3);
	}

	private static void EnsurePlayback(Transform peerTransform)
	{
		//IL_003d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Expected O, but got Unknown
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00eb: Expected O, but got Unknown
		if (!((Object)(object)_playbackSource != (Object)null) && !((Object)(object)peerTransform == (Object)null))
		{
			_ring = new float[32000];
			_ringWrite = (_ringRead = (_ringCount = 0));
			_playbackGO = new GameObject("VoiceChatPlayback");
			_playbackGO.transform.SetParent(peerTransform, false);
			_playbackGO.transform.localPosition = Vector3.zero;
			_playbackSource = _playbackGO.AddComponent<AudioSource>();
			_playbackSource.spatialBlend = 1f;
			_playbackSource.rolloffMode = (AudioRolloffMode)1;
			_playbackSource.minDistance = 1f;
			_playbackSource.maxDistance = 100000f;
			_playbackSource.loop = true;
			_playbackSource.playOnAwake = false;
			_playbackClip = AudioClip.Create("VoicePeer", 16000, 1, 16000, true, new AudioClip.PCMReaderCallback(OnAudioRead));
			_playbackSource.clip = _playbackClip;
			_playbackSource.Play();
			_reverbFilter = _playbackGO.AddComponent<AudioReverbFilter>();
			((Behaviour)_reverbFilter).enabled = false;
			_lowPassFilter = _playbackGO.AddComponent<AudioLowPassFilter>();
			_lowPassFilter.cutoffFrequency = 1500f;
			((Behaviour)_lowPassFilter).enabled = false;
		}
	}

	private static void OnAudioRead(float[] data)
	{
		lock (_ringLock)
		{
			int num = Mathf.Min(data.Length, _ringCount);
			for (int i = 0; i < num; i++)
			{
				data[i] = _ring[_ringRead];
				_ringRead = (_ringRead + 1) % _ring.Length;
			}
			_ringCount -= num;
			for (int j = num; j < data.Length; j++)
			{
				data[j] = 0f;
			}
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
		if ((Object)(object)_playbackGO != (Object)null)
		{
			Object.Destroy((Object)(object)_playbackGO);
		}
		_playbackGO = null;
		_playbackSource = null;
		_playbackClip = null;
		_reverbFilter = null;
		_lowPassFilter = null;
		IsSpeaking = false;
	}
}
