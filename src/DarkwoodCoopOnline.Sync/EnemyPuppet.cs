using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public sealed class EnemyPuppet : MonoBehaviour
{
	private tk2dSpriteAnimator _body;

	private tk2dSpriteAnimator _legs;

	private Vector3 _targetPos;

	private Quaternion _targetRot;

	private string _bodyClip = "";

	private string _legsClip = "";

	private bool _init;

	private const float PosLerp = 18f;

	private const float RotLerp = 18f;

	private const float HitOverrideSeconds = 0.35f;

	private float _hitOverrideUntil = -1f;

	private string _lastMissingClipWarned = "";

	private string _lastMissingLegsClipWarned = "";

	private string _lastDiagnosedClip = "";

	private string _lastDiagnosedLegsClip = "";

	public Vector3 DebugTargetPos => _targetPos;

	// Snapshot interpolation (see RemotePlayerPuppet): show the enemy where its owner saw it ~100 ms ago,
	// between two real updates, instead of chasing the newest one (which stutters with uneven arrivals).
	private readonly float[] _sampleT = new float[12];

	private readonly Vector3[] _sampleP = new Vector3[12];

	private int _samples;

	private void PushSample(Vector3 pos)
	{
		if (_samples > 0 && Vector3.Distance(_sampleP[_samples - 1], pos) > 400f)
		{
			_samples = 0;
		}
		else if (_samples > 0 && Time.unscaledTime - _sampleT[_samples - 1] > 0.3f)
		{
			// it stood still (no updates): restart motion from where it stood, not from long ago
			Vector3 vector = _sampleP[_samples - 1];
			_samples = 0;
			_sampleT[0] = Time.unscaledTime - 0.05f;
			_sampleP[0] = vector;
			_samples = 1;
		}
		if (_samples == _sampleT.Length)
		{
			System.Array.Copy(_sampleT, 1, _sampleT, 0, _samples - 1);
			System.Array.Copy(_sampleP, 1, _sampleP, 0, _samples - 1);
			_samples--;
		}
		_sampleT[_samples] = Time.unscaledTime;
		_sampleP[_samples] = pos;
		_samples++;
	}

	private Vector3 Sampled()
	{
		if (_samples == 0)
		{
			return _targetPos;
		}
		float num = Time.unscaledTime - 0.1f;
		if (num <= _sampleT[0])
		{
			return _sampleP[0];
		}
		for (int i = 0; i < _samples - 1; i++)
		{
			if (num >= _sampleT[i] && num <= _sampleT[i + 1])
			{
				float num2 = _sampleT[i + 1] - _sampleT[i];
				return Vector3.Lerp(_sampleP[i], _sampleP[i + 1], (num2 > 0.0001f) ? ((num - _sampleT[i]) / num2) : 1f);
			}
		}
		if (_samples >= 2)
		{
			float num3 = _sampleT[_samples - 1] - _sampleT[_samples - 2];
			if (num3 > 0.0001f && num3 < 0.5f)
			{
				float num4 = Mathf.Min(num - _sampleT[_samples - 1], 0.15f);
				return _sampleP[_samples - 1] + (_sampleP[_samples - 1] - _sampleP[_samples - 2]) * (num4 / num3);
			}
		}
		return _sampleP[_samples - 1];
	}

	public void Bind(Character c, tk2dSpriteAnimator body)
	{
		c.init();
		_body = body;
		_legs = c.legsAnimator;
		Rigidbody component = ((Component)c).GetComponent<Rigidbody>();
		if ((Object)(object)component != (Object)null)
		{
			component.isKinematic = true;
		}
		_targetPos = ((Component)c).transform.position;
		_targetRot = (((Object)(object)body != (Object)null) ? ((Component)body).transform.rotation : ((Component)c).transform.rotation);
		_init = true;
	}

	public void SetTarget(Vector3 pos, Quaternion rot, string bodyClip, string legsClip)
	{
		_targetPos = pos;
		PushSample(pos);
		_targetRot = rot;
		_bodyClip = bodyClip;
		_legsClip = legsClip;
	}

	public void SnapToTarget()
	{
		((Component)this).transform.position = _targetPos;
		_samples = 0;
		PushSample(_targetPos);
		if ((Object)(object)_body != (Object)null)
		{
			((Component)_body).transform.rotation = _targetRot;
		}
	}

	public void PlayHitReaction(string clip)
	{
		if (!((Object)(object)_body == (Object)null) && !string.IsNullOrEmpty(clip) && _body.GetClipByName(clip) != null)
		{
			bool flag = Time.time < _hitOverrideUntil;
			Plugin.Log.LogInfo("[EnemyPuppet][HitReaction] '" + ((Object)((Component)this).gameObject).name + "' tocando '" + clip + "' " + $"(override já ativo/travando SafePlay={flag}, faltavam {Mathf.Max(0f, _hitOverrideUntil - Time.time):F2}s pra liberar).");
			PuppetAnimSuppress.WeAreCalling = true;
			_body.Play(clip);
			PuppetAnimSuppress.WeAreCalling = false;
			_hitOverrideUntil = Time.time + 0.35f;
		}
	}

	private void Update()
	{
		if (!_init)
		{
			return;
		}
		Vector3 position = ((Component)this).transform.position;
		Vector3 vector = Sampled();
		((Component)this).transform.position = new Vector3(vector.x, position.y, vector.z);
		if ((Object)(object)_body != (Object)null)
		{
			((Component)_body).transform.rotation = Quaternion.Slerp(((Component)_body).transform.rotation, _targetRot, 18f * Time.deltaTime);
			if (Time.time >= _hitOverrideUntil)
			{
				SafePlay(_body, _bodyClip, ((Object)((Component)this).gameObject).name, "BODY", ref _lastMissingClipWarned, ref _lastDiagnosedClip, ((Component)this).transform.position);
			}
		}
		if ((Object)(object)_legs != (Object)null)
		{
			((Component)_legs).transform.rotation = Quaternion.Slerp(((Component)_legs).transform.rotation, _targetRot, 18f * Time.deltaTime);
			SafePlay(_legs, _legsClip, ((Object)((Component)this).gameObject).name, "LEGS", ref _lastMissingLegsClipWarned, ref _lastDiagnosedLegsClip, ((Component)this).transform.position);
		}
	}

	private static void SafePlay(tk2dSpriteAnimator a, string clip, string ownerName, string tag, ref string lastMissingWarned, ref string lastDiagnosedClip, Vector3 pos)
	{
		if ((Object)(object)a == (Object)null || string.IsNullOrEmpty(clip))
		{
			return;
		}
		bool flag = clip != lastDiagnosedClip;
		if (flag)
		{
			lastDiagnosedClip = clip;
			string text = ((a.CurrentClip != null) ? a.CurrentClip.name : "(nenhum)");
			if (TimestampedLogSource.Verbose)
			{
				Plugin.Log.LogInfo($"[EnemyPuppet][{tag}][diag] '{ownerName}' pos=({pos.x:F0},{pos.z:F0}) - pedido NOVO clipe='{clip}' " + $"| ANTES: Playing={a.Playing} CurrentClip='{text}' IsPlaying('{clip}')={a.IsPlaying(clip)}");
			}
		}
		if (a.IsPlaying(clip))
		{
			if (flag)
			{
				if (TimestampedLogSource.Verbose)
				{
					Plugin.Log.LogInfo("[EnemyPuppet][" + tag + "][diag] '" + ownerName + "' - IGNOROU Play('" + clip + "') pq IsPlaying já é True.");
				}
			}
		}
		else if (a.GetClipByName(clip) != null)
		{
			PuppetAnimSuppress.WeAreCalling = true;
			a.Play(clip);
			PuppetAnimSuppress.WeAreCalling = false;
			if (flag)
			{
				if (TimestampedLogSource.Verbose)
				{
					Plugin.Log.LogInfo("[EnemyPuppet][" + tag + "][diag] '" + ownerName + "' - Play('" + clip + "') chamado. DEPOIS: " + string.Format("Playing={0} CurrentClip='{1}'", a.Playing, (a.CurrentClip != null) ? a.CurrentClip.name : "(nenhum)"));
				}
			}
		}
		else if (clip != lastMissingWarned)
		{
			lastMissingWarned = clip;
			Plugin.Log.LogWarning($"[EnemyPuppet][{tag}] '{ownerName}' pos=({pos.x:F0},{pos.z:F0}) pediu '{clip}' mas GetClipByName retornou NULO (não existe na library atual!).");
		}
	}
}
