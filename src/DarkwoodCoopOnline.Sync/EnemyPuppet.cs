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
		_targetRot = rot;
		_bodyClip = bodyClip;
		_legsClip = legsClip;
	}

	public void SnapToTarget()
	{
		((Component)this).transform.position = _targetPos;
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
		Vector3 val = default(Vector3);
		val = new Vector3(_targetPos.x, position.y, _targetPos.z);
		((Component)this).transform.position = Vector3.Lerp(position, val, 18f * Time.deltaTime);
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
			Plugin.Log.LogInfo($"[EnemyPuppet][{tag}][diag] '{ownerName}' pos=({pos.x:F0},{pos.z:F0}) - pedido NOVO clipe='{clip}' " + $"| ANTES: Playing={a.Playing} CurrentClip='{text}' IsPlaying('{clip}')={a.IsPlaying(clip)}");
		}
		if (a.IsPlaying(clip))
		{
			if (flag)
			{
				Plugin.Log.LogInfo("[EnemyPuppet][" + tag + "][diag] '" + ownerName + "' - IGNOROU Play('" + clip + "') pq IsPlaying já é True.");
			}
		}
		else if (a.GetClipByName(clip) != null)
		{
			PuppetAnimSuppress.WeAreCalling = true;
			a.Play(clip);
			PuppetAnimSuppress.WeAreCalling = false;
			if (flag)
			{
				Plugin.Log.LogInfo("[EnemyPuppet][" + tag + "][diag] '" + ownerName + "' - Play('" + clip + "') chamado. DEPOIS: " + string.Format("Playing={0} CurrentClip='{1}'", a.Playing, (a.CurrentClip != null) ? a.CurrentClip.name : "(nenhum)"));
			}
		}
		else if (clip != lastMissingWarned)
		{
			lastMissingWarned = clip;
			Plugin.Log.LogWarning($"[EnemyPuppet][{tag}] '{ownerName}' pos=({pos.x:F0},{pos.z:F0}) pediu '{clip}' mas GetClipByName retornou NULO (não existe na library atual!).");
		}
	}
}
