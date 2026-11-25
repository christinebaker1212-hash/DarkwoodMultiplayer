using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public sealed class ShadowPuppet : MonoBehaviour
{
	private tk2dSpriteAnimator _anim;

	private Vector3 _targetPos;

	private string _clip = "Float";

	private bool _init;

	private const float PosLerp = 8f;

	public void Bind(tk2dSpriteAnimator anim)
	{
		_anim = anim;
		if ((Object)(object)_anim != (Object)null)
		{
			_anim.IgnoreCameraVisibility = true;
		}
		_targetPos = ((Component)this).transform.position;
		_init = true;
	}

	public void SetTarget(Vector3 pos, string clip)
	{
		_targetPos = pos;
		_clip = clip;
	}

	private void Update()
	{
		if (_init)
		{
			((Component)this).transform.position = Vector3.Lerp(((Component)this).transform.position, _targetPos, 8f * Time.deltaTime);
			if ((Object)(object)_anim != (Object)null && !string.IsNullOrEmpty(_clip) && !_anim.IsPlaying(_clip) && _anim.GetClipByName(_clip) != null)
			{
				_anim.Play(_clip);
			}
		}
	}
}
