using System;
using System.Collections.Generic;
using DarkwoodCoopOnline.Net;
using DarkwoodCoopOnline.Sync;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

public sealed class RemotePlayerPuppet : CharBase
{
	internal static tk2dSpriteAnimator DebugTorso;

	internal static tk2dSpriteAnimator DebugLegs;

	private tk2dSpriteAnimator _torso;

	private tk2dSpriteAnimator _legs;

	private Renderer _torsoRend;

	private Renderer _legsRend;

	private CapsuleCollider _collider;

	private bool _pendingColliderIsTrigger;

	private CharacterSounds _sounds;

	private Vector3 _targetPos;

	private float _targetAim;

	private float _shownAim;

	// Which player this is (slot 0 = host) and their name
	public int Slot = -1;

	public string PeerName = "";

	// Snapshot interpolation: positions arrive ~20 times a second with uneven gaps; we show the player
	// where they were InterpDelay ago, between two real samples, instead of chasing the newest one.
	private struct PosSample
	{
		public float T;

		public Vector3 P;
	}

	private readonly PosSample[] _samples = new PosSample[16];

	private int _sampleCount;

	private const float InterpDelay = 0.1f;

	private const float MaxExtrapolate = 0.15f;

	private const float TeleportDistance = 600f;

	private void PushSample(Vector3 pos)
	{
		float unscaledTime = Time.unscaledTime;
		if (_sampleCount > 0 && Vector3.Distance(_samples[_sampleCount - 1].P, pos) > TeleportDistance)
		{
			// teleport (door to another area, F1, respawn): jump there instead of sliding across the map
			_sampleCount = 0;
			((Component)this).transform.position = pos;
		}
		if (_sampleCount == _samples.Length)
		{
			Array.Copy(_samples, 1, _samples, 0, _samples.Length - 1);
			_sampleCount--;
		}
		_samples[_sampleCount++] = new PosSample
		{
			T = unscaledTime,
			P = pos
		};
	}

	private Vector3 SampledPosition()
	{
		if (_sampleCount == 0)
		{
			return _targetPos;
		}
		float num = Time.unscaledTime - InterpDelay;
		if (num <= _samples[0].T)
		{
			return _samples[0].P;
		}
		for (int i = 0; i < _sampleCount - 1; i++)
		{
			PosSample posSample = _samples[i];
			PosSample posSample2 = _samples[i + 1];
			if (num >= posSample.T && num <= posSample2.T)
			{
				float num2 = posSample2.T - posSample.T;
				return Vector3.Lerp(posSample.P, posSample2.P, (num2 > 0.0001f) ? ((num - posSample.T) / num2) : 1f);
			}
		}
		// past the newest sample (a late packet): keep moving the same way for a moment, then stop
		PosSample posSample3 = _samples[_sampleCount - 1];
		if (_sampleCount >= 2)
		{
			PosSample posSample4 = _samples[_sampleCount - 2];
			float num3 = posSample3.T - posSample4.T;
			if (num3 > 0.0001f && num3 < 0.5f)
			{
				float num4 = Mathf.Min(num - posSample3.T, MaxExtrapolate);
				return posSample3.P + (posSample3.P - posSample4.P) * (num4 / num3);
			}
		}
		return posSample3.P;
	}

	private string _currentClip = "Idle";

	private bool _hasState;

	private string _attackClip = "";

	private float _attackHold;

	private string _currentLib = "";

	private string _hitClip = "";

	private float _hitHold;

	private const float HitHoldSeconds = 0.35f;

	private string _legsClip = "";

	private Quaternion _legsRot = Quaternion.identity;

	private bool _legsVisible;

	private bool _legsPlaying = true;

	private bool _torsoPlaying = true;

	private string _currentLightItemType = "";

	private GameObject _lightEmitterGO;

	private GameObject _particleEmitterGO;

	private InvItem _lightItem;

	private const int LanternPetalCount = 4;

	private Light2D[] _lanternPetals;

	private Light2D _flashlightLight;

	private bool _isFlashlightItemCached;

	private string _isFlashlightItemCachedFor = "\0";

	private int _torsoFrame;

	private bool _particlePositioned;

	private static Dictionary<string, tk2dSpriteAnimation> _libCache;

	private static float _libScanAt = -999f;

	private const float LibRescan = 2f;

	private const float FootstepFullVolumeDistance = 300f;

	private const float FootstepSilentDistance = 1500f;

	private bool _downedVisual;

	private const int RescuerSortingOrderBoost = 10;

	private int _sortingOrderBeforeRescuing;

	private bool _sortingBoosted;

	private bool _colliderTriggerBeforeDowned;

	private bool _downedColliderOverrideActive;

	private bool _cutsceneHidden;

	public bool HasState => _hasState;

	public float TargetAim => _targetAim;

	public Light2D DebugLanternLight
	{
		get
		{
			if (_lanternPetals == null || _lanternPetals.Length == 0)
			{
				return null;
			}
			return _lanternPetals[0];
		}
	}

	public void PlayHitReaction()
	{
		_hitClip = "Hit1";
		_hitHold = 0.35f;
	}

	public static RemotePlayerPuppet Create()
	{
		if ((Object)(object)Player.Instance == (Object)null)
		{
			return null;
		}
		GameObject val = new GameObject("RemotePlayer");
		val.tag = "Player";
		val.layer = ((Component)Player.Instance).gameObject.layer;
		val.transform.position = ((Component)Player.Instance).transform.position;
		GameObject val2 = new GameObject("Visual");
		val2.transform.SetParent(val.transform);
		val2.transform.localPosition = Vector3.zero;
		GameObject val3 = new GameObject("puppetHolder");
		val3.SetActive(false);
		GameObject torsoClone = Object.Instantiate<GameObject>(((Component)Player.Instance.torsoAnimator).gameObject, val3.transform);
		StripChildren(torsoClone);
		StripComponents(torsoClone);
		tk2dSpriteAnimator component = torsoClone.GetComponent<tk2dSpriteAnimator>();
		Renderer component2 = torsoClone.GetComponent<Renderer>();
		tk2dSpriteAnimator val4 = null;
		Renderer legsRend = null;
		GameObject val5 = null;
		Vector3 localPosition = Vector3.zero;
		Quaternion localRotation = Quaternion.identity;
		Transform val6 = ((Component)Player.Instance).transform.Find("PlayerLegs");
		if ((Object)(object)val6 != (Object)null)
		{
			localPosition = val6.localPosition;
			localRotation = val6.localRotation;
			val5 = Object.Instantiate<GameObject>(((Component)val6).gameObject, val3.transform);
			StripComponents(val5);
			val4 = val5.GetComponent<tk2dSpriteAnimator>();
			legsRend = val5.GetComponent<Renderer>();
		}
		torsoClone.transform.SetParent(val2.transform);
		torsoClone.transform.localPosition = Vector3.zero;
		torsoClone.transform.localRotation = Quaternion.identity;
		if ((Object)(object)val5 != (Object)null)
		{
			val5.transform.SetParent(torsoClone.transform);
			val5.transform.localPosition = localPosition;
			val5.transform.localRotation = localRotation;
			val5.layer = torsoClone.layer;
		}
		Object.Destroy((Object)(object)val3);
		tk2dBaseSprite component3 = torsoClone.GetComponent<tk2dBaseSprite>();
		if (component3 != null)
		{
			component3.ForceBuild();
		}
		if (val5 != null)
		{
			tk2dBaseSprite component4 = val5.GetComponent<tk2dBaseSprite>();
			if (component4 != null)
			{
				component4.ForceBuild();
			}
		}
		CapsuleCollider val7 = null;
		CapsuleCollider component5 = ((Component)Player.Instance).GetComponent<CapsuleCollider>();
		if ((Object)(object)component5 != (Object)null)
		{
			val7 = val.AddComponent<CapsuleCollider>();
			val7.radius = component5.radius;
			val7.height = component5.height;
			val7.center = component5.center;
			val7.direction = component5.direction;
			((Collider)val7).isTrigger = true;
			val.AddComponent<PuppetMeleeReceiver>();
		}
		RemotePlayerPuppet puppet = val.AddComponent<RemotePlayerPuppet>();
		puppet._collider = val7;
		puppet._pendingColliderIsTrigger = (Object)(object)component5 != (Object)null && ((Collider)component5).isTrigger;
		puppet._torso = component;
		DebugTorso = component;
		DebugLegs = val4;
		puppet._legs = val4;
		puppet._torsoRend = component2;
		puppet._legsRend = legsRend;
		puppet.InitEntity();
		CharacterSounds component6 = ((Component)Player.Instance).GetComponent<CharacterSounds>();
		if ((Object)(object)component6 != (Object)null)
		{
			CharacterSounds val8 = val.AddComponent<CharacterSounds>();
			val8.character = (CharBase)(object)puppet;
			val8.isPlayer = false;
			val8.footstepGrass = component6.footstepGrass;
			val8.footstepWater = component6.footstepWater;
			val8.footstepTiles = component6.footstepTiles;
			val8.footstepWood = component6.footstepWood;
			val8.footstepBridge = component6.footstepBridge;
			val8.footstepCarpet = component6.footstepCarpet;
			val8.footstepInfection = component6.footstepInfection;
			val8.footHitGroundSoundChance = component6.footHitGroundSoundChance;
			val8.footstepVolume = component6.footstepVolume;
			puppet._sounds = val8;
		}
		if ((Object)(object)component != (Object)null)
		{
			component.AnimationEventTriggered = (Action<tk2dSpriteAnimator, tk2dSpriteAnimationClip, int>)Delegate.Combine(component.AnimationEventTriggered, (Action<tk2dSpriteAnimator, tk2dSpriteAnimationClip, int>)delegate(tk2dSpriteAnimator anim, tk2dSpriteAnimationClip clip, int frame)
			{
				puppet.PlayFrameTriggers(clip, frame, torsoClone.transform);
			});
		}
		if ((Object)(object)val4 != (Object)null)
		{
			val4.AnimationEventTriggered = (Action<tk2dSpriteAnimator, tk2dSpriteAnimationClip, int>)Delegate.Combine(val4.AnimationEventTriggered, (Action<tk2dSpriteAnimator, tk2dSpriteAnimationClip, int>)delegate(tk2dSpriteAnimator anim, tk2dSpriteAnimationClip clip, int frame)
			{
				puppet.PlayFrameTriggers(clip, frame, torsoClone.transform);
			});
		}
		if ((Object)(object)component != (Object)null)
		{
			((Component)component).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		}
		Transform val9 = ((Component)Player.Instance).transform.Find("Flashlight");
		if ((Object)(object)val9 != (Object)null)
		{
			GameObject obj = Object.Instantiate<GameObject>(((Component)val9).gameObject, val.transform);
			obj.transform.localPosition = val9.localPosition;
			Light2D component7 = obj.GetComponent<Light2D>();
			if ((Object)(object)component7 != (Object)null)
			{
				component7.lightsPlayer = false;
				component7.updateGraph = false;
				puppet._flashlightLight = component7;
			}
			obj.SetActive(false);
		}
		if ((Object)(object)val9 != (Object)null)
		{
			puppet._lanternPetals = (Light2D[])(object)new Light2D[4];
			for (int i = 0; i < 4; i++)
			{
				float num = (float)i * 90f;
				GameObject obj2 = Object.Instantiate<GameObject>(((Component)val9).gameObject, val.transform);
				obj2.transform.localPosition = val9.localPosition;
				obj2.transform.localRotation = Quaternion.Euler(90f, num, 0f);
				Light2D component8 = obj2.GetComponent<Light2D>();
				if ((Object)(object)component8 != (Object)null)
				{
					component8.lightsPlayer = false;
					component8.updateGraph = false;
				}
				obj2.SetActive(false);
				puppet._lanternPetals[i] = component8;
			}
		}
		Plugin.Log.LogInfo("RemotePlayerPuppet criado.");
		return puppet;
	}

	private void InitEntity()
	{
		base.faction = (Faction)0;
		base.maxHealth = 100f;
		((CharBase)this).health = base.maxHealth;
		base.maxStamina = 100f;
		base.stamina = 100f;
		base.alive = true;
		base.invisible = false;
		base.ignoreMe = false;
		_targetPos = ((Component)this).transform.position;
	}

	public void ApplyState(Vector3 pos, float aimAngle, string clip, bool attacking, string lib, string legsClip, Quaternion legsRot, bool legsVisible, bool legsPlaying, bool torsoPlaying, string lightItemType, int torsoFrame, bool lanternOn, float lanternRadius, float flashlightRadius, float flashlightBrightness)
	{
		if (!_hasState && (Object)(object)_collider != (Object)null)
		{
			((Collider)_collider).isTrigger = _pendingColliderIsTrigger;
		}
		_targetPos = pos;
		_targetAim = aimAngle;
		if (!_hasState)
		{
			_shownAim = aimAngle;
			((Component)this).transform.position = pos;
		}
		PushSample(pos);
		_currentClip = (string.IsNullOrEmpty(clip) ? "Idle" : clip);
		_hasState = true;
		_torsoFrame = torsoFrame;
		_legsClip = legsClip;
		_legsRot = legsRot;
		_legsVisible = legsVisible;
		_legsPlaying = legsPlaying;
		_torsoPlaying = torsoPlaying;
		if (!string.IsNullOrEmpty(lib) && lib != _currentLib && (Object)(object)_torso != (Object)null)
		{
			tk2dSpriteAnimation val = FindLibrary(lib);
			if ((Object)(object)val != (Object)null)
			{
				_torso.Library = val;
				_currentLib = lib;
				_torso.Play(_currentClip);
			}
		}
		if (attacking)
		{
			_attackClip = _currentClip;
			_attackHold = 0.45f;
		}
		if (lightItemType != _currentLightItemType)
		{
			if ((Object)(object)_lightEmitterGO != (Object)null)
			{
				Object.Destroy((Object)(object)_lightEmitterGO);
				_lightEmitterGO = null;
			}
			if ((Object)(object)_particleEmitterGO != (Object)null)
			{
				Object.Destroy((Object)(object)_particleEmitterGO);
				_particleEmitterGO = null;
			}
			_lightItem = null;
			_particlePositioned = false;
			if (!string.IsNullOrEmpty(lightItemType))
			{
				SpawnEmittersForType(lightItemType);
			}
			_currentLightItemType = lightItemType;
		}
		if ((Object)(object)_flashlightLight != (Object)null)
		{
			if (lightItemType != _isFlashlightItemCachedFor)
			{
				_isFlashlightItemCachedFor = lightItemType;
				_isFlashlightItemCached = false;
				if (!string.IsNullOrEmpty(lightItemType))
				{
					InvItem val2 = (((Object)(object)Singleton<ItemsDatabase>.Instance != (Object)null) ? Singleton<ItemsDatabase>.Instance.getItem(lightItemType, false) : null);
					_isFlashlightItemCached = (Object)(object)val2 != (Object)null && val2.isFlashlight;
				}
			}
			if (_isFlashlightItemCached)
			{
				if (!((Component)_flashlightLight).gameObject.activeSelf)
				{
					((Component)_flashlightLight).gameObject.SetActive(true);
				}
				((Component)_flashlightLight).transform.rotation = Quaternion.Euler(90f, aimAngle, 0f);
				if (flashlightRadius > 0.01f)
				{
					_flashlightLight.LightRadius = flashlightRadius;
				}
				_flashlightLight.LightColor = new Color(flashlightBrightness, flashlightBrightness, flashlightBrightness, 0f);
			}
			else if (((Component)_flashlightLight).gameObject.activeSelf)
			{
				((Component)_flashlightLight).gameObject.SetActive(false);
			}
		}
		if (_lanternPetals != null)
		{
			Light2D[] lanternPetals = _lanternPetals;
			foreach (Light2D val3 in lanternPetals)
			{
				if ((Object)(object)val3 == (Object)null)
				{
					continue;
				}
				if (lanternOn)
				{
					if (!((Component)val3).gameObject.activeSelf)
					{
						((Component)val3).gameObject.SetActive(true);
					}
					if (lanternRadius > 0.01f)
					{
						val3.LightRadius = lanternRadius;
					}
				}
				else if (((Component)val3).gameObject.activeSelf)
				{
					((Component)val3).gameObject.SetActive(false);
				}
			}
		}
		if ((Object)(object)_legsRend != (Object)null)
		{
			_legsRend.enabled = _legsVisible;
		}
		if ((Object)(object)_torsoRend != (Object)null)
		{
			_torsoRend.enabled = true;
		}
	}

	private void SpawnEmittersForType(string itemType)
	{
		InvItem val = (((Object)(object)Singleton<ItemsDatabase>.Instance != (Object)null) ? Singleton<ItemsDatabase>.Instance.getItem(itemType, false) : null);
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogWarning("[RemotePlayerPuppet] item '" + itemType + "' não encontrado na ItemsDatabase.");
			return;
		}
		if (val.lightEmitter != (Object)null)
		{
			_lightEmitterGO = SpawnEmitterObject(val.lightEmitter);
		}
		if (val._particleEmitter != (Object)null)
		{
			_particleEmitterGO = SpawnEmitterObject(val._particleEmitter);
			if ((Object)(object)_particleEmitterGO != (Object)null)
			{
				_particleEmitterGO.SetActive(false);
			}
		}
		_lightItem = val;
		UpdateParticleEmitterPosition(_currentClip);
	}

	private void UpdateParticleEmitterPosition(string clipName)
	{
		if ((Object)(object)_particleEmitterGO == (Object)null || _lightItem?.emitterPositions?.typesDict == null || !_lightItem.emitterPositions.typesDict.TryGetValue(clipName, out var value))
		{
			return;
		}
		int torsoFrame = _torsoFrame;
		if (value.positions != null && torsoFrame >= 0 && torsoFrame < value.positions.Count)
		{
			Vector2 val = value.positions[torsoFrame];
			Transform transform = _particleEmitterGO.transform;
			transform.localPosition = new Vector3(val.x, val.y, transform.localPosition.z);
			if (!_particlePositioned)
			{
				_particlePositioned = true;
				_particleEmitterGO.SetActive(true);
			}
		}
	}

	private GameObject SpawnEmitterObject(UnityEngine.Object prefab)
	{
		Transform val = (((Object)(object)_torso != (Object)null) ? ((Component)_torso).transform : ((Component)this).transform);
		Object obj = Object.Instantiate(prefab, val.position, Quaternion.Euler(90f, 0f, 0f));
		GameObject val2 = (GameObject)(object)((obj is GameObject) ? obj : null);
		if ((Object)(object)val2 == (Object)null)
		{
			return null;
		}
		val2.transform.SetParent(val);
		val2.transform.localPosition = Vector3.zero;
		return val2;
	}

	private static tk2dSpriteAnimation FindLibrary(string name)
	{
		if (_libCache != null && _libCache.TryGetValue(name, out var value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		if (Time.time - _libScanAt < 2f)
		{
			return null;
		}
		_libScanAt = Time.time;
		tk2dSpriteAnimation val = Resources.Load<tk2dSpriteAnimation>(name);
		_libCache = new Dictionary<string, tk2dSpriteAnimation>();
		tk2dSpriteAnimation[] array = Resources.FindObjectsOfTypeAll<tk2dSpriteAnimation>();
		foreach (tk2dSpriteAnimation val2 in array)
		{
			if ((Object)(object)val2 != (Object)null)
			{
				_libCache[((Object)val2).name] = val2;
			}
		}
		if ((Object)(object)val != (Object)null)
		{
			_libCache[name] = val;
		}
		_libCache.TryGetValue(name, out var value2);
		Plugin.Log.LogInfo(((Object)(object)value2 != (Object)null) ? ("[Lib] '" + name + "' resolvida via " + (((Object)(object)val != (Object)null) ? "Resources.Load" : "scan")) : ("[Lib] '" + name + "' NÃO encontrada (nem Resources.Load nem scan)"));
		return value2;
	}

	private void Update()
	{
		if (!_hasState)
		{
			return;
		}
		((Component)this).transform.position = SampledPosition();
		_shownAim = Mathf.LerpAngle(_shownAim, _targetAim, Mathf.Clamp01(25f * Time.unscaledDeltaTime));
		string text = _currentClip;
		if (_attackHold > 0f)
		{
			_attackHold -= Time.deltaTime;
			text = _attackClip;
		}
		if (_hitHold > 0f)
		{
			_hitHold -= Time.deltaTime;
			text = _hitClip;
		}
		if ((Object)(object)_torso != (Object)null)
		{
			((Component)_torso).transform.rotation = Quaternion.Euler(90f, _shownAim, 0f);
			SafePlay(_torso, text);
			if (_attackHold <= 0f && _hitHold <= 0f)
			{
				_torso.Paused = !_torsoPlaying;
			}
			UpdateParticleEmitterPosition(text);
		}
		if ((Object)(object)_legsRend != (Object)null)
		{
			_legsRend.enabled = _legsVisible;
		}
		if ((Object)(object)_legs != (Object)null && _legsVisible)
		{
			((Component)_legs).transform.rotation = _legsRot;
			SafePlay(_legs, _legsClip);
			_legs.Paused = !_legsPlaying;
			if ((Object)(object)_legsRend != (Object)null && (Object)(object)_legsRend.material != (Object)null)
			{
				_legsRend.material.color = Color.white;
			}
			if ((Object)(object)_legs.Sprite != (Object)null)
			{
				_legs.Sprite.color = Color.white;
			}
		}
	}

	private void LateUpdate()
	{
		if (!_hasState)
		{
			return;
		}
		if (_legsVisible && (Object)(object)_legs != (Object)null)
		{
			if ((Object)(object)_legs.Sprite != (Object)null)
			{
				_legs.Sprite.color = Color.white;
			}
			if ((Object)(object)_legsRend != (Object)null && (Object)(object)_legsRend.material != (Object)null)
			{
				_legsRend.material.color = Color.white;
			}
		}
		if ((Object)(object)_torso != (Object)null && (Object)(object)_torso.Sprite != (Object)null)
		{
			_torso.Sprite.color = Color.white;
		}
	}

	private void PlayFrameTriggers(tk2dSpriteAnimationClip clip, int frameNum, Transform anchor)
	{
		tk2dSpriteAnimationFrame frame = clip.GetFrame(frameNum);
		if (frame == null)
		{
			return;
		}
		if (frame.triggers != null)
		{
			for (int i = 0; i < frame.triggers.Count; i++)
			{
				tk2dSpriteAnimationFrame.Trigger val = frame.triggers[i];
				tk2dSpriteAnimationFrame.Trigger.Type type = val.type;
				Quaternion rotation;
				if ((int)type != 10)
				{
					if ((int)type != 15)
					{
						if ((int)type == 110 && !string.IsNullOrEmpty(val.eventInfo))
						{
							Plugin.Log.LogInfo($"[PuppetSound][Diag] playSound: clip='{clip.name}' frame={frameNum} som='{val.eventInfo}'");
							AudioController.Play(val.eventInfo, anchor);
						}
					}
					else if (val.prefab != (Object)null)
					{
						for (int j = 0; j < val.eventInt; j++)
						{
							Object prefab = val.prefab;
							Vector3 position = anchor.position;
							rotation = anchor.rotation;
							Core.AddPrefab(prefab, position + Quaternion.Euler(0f, rotation.eulerAngles.y, 0f) * val.vector, anchor.rotation * Quaternion.Euler(val.rotation), Core.ItemContainer, false);
						}
					}
				}
				else
				{
					for (int k = 0; k < val.eventInt; k++)
					{
						string eventInfo = val.eventInfo;
						Vector3 position2 = anchor.position;
						rotation = anchor.rotation;
						Core.AddPooledPrefab("FX", eventInfo, position2 + Quaternion.Euler(0f, rotation.eulerAngles.y, 0f) * val.vector, anchor.rotation * Quaternion.Euler(val.rotation), (Transform)null, false);
					}
				}
			}
		}
		if (!((Object)(object)_sounds != (Object)null) || (!(frame.eventInfo == "FootHitGround") && !(frame.eventInfo == "FootHitGroundRun")))
		{
			return;
		}
		bool flag = frame.eventInfo == "FootHitGroundRun";
		CoopSession session = CoopRuntime.Session;
		if (session != null && session.Role == NetRole.Host)
		{
			Character.alertInArea(((Component)this).transform.position, flag ? 350f : 150f, false, 1f, false);
		}
		float num = Vector3.Distance(((Component)Player.Instance).transform.position, ((Component)this).transform.position);
		float num2 = 1f - Mathf.Clamp01((num - 300f) / 1200f);
		if (!(num2 <= 0f))
		{
			if (!flag)
			{
				_sounds.playFootHitGround(num2);
			}
			else
			{
				_sounds.playFootHitGround(1.5f * num2);
			}
		}
	}

	private static void SafePlay(tk2dSpriteAnimator anim, string clip)
	{
		if (!((Object)(object)anim == (Object)null) && !string.IsNullOrEmpty(clip) && !anim.IsPlaying(clip) && anim.GetClipByName(clip) != null)
		{
			anim.Play(clip);
		}
	}

	private static void StripChildren(GameObject go)
	{
		List<GameObject> list = new List<GameObject>();
		foreach (Transform item in go.transform)
		{
			Transform val = item;
			list.Add(((Component)val).gameObject);
		}
		foreach (GameObject item2 in list)
		{
			Object.DestroyImmediate((Object)(object)item2);
		}
	}

	private static void StripComponents(GameObject go)
	{
		Component[] components = go.GetComponents<Component>();
		foreach (Component val in components)
		{
			if (!(val is Transform) && !(val is tk2dBaseSprite) && !(val is tk2dSpriteAnimator) && !(val is MeshFilter) && !(val is Renderer))
			{
				Object.DestroyImmediate((Object)(object)val);
			}
		}
	}

	public void SetReallyDead()
	{
		if ((Object)(object)_collider != (Object)null)
		{
			((Collider)_collider).enabled = false;
		}
		base.invisible = true;
	}

	public void SetDownedVisual(bool peerDowned, bool amIDowned)
	{
		if ((Object)(object)_collider != (Object)null)
		{
			if (peerDowned && !_downedColliderOverrideActive)
			{
				_colliderTriggerBeforeDowned = ((Collider)_collider).isTrigger;
				_downedColliderOverrideActive = true;
			}
			else if (!peerDowned && _downedColliderOverrideActive)
			{
				_downedColliderOverrideActive = false;
			}
			((Collider)_collider).isTrigger = peerDowned || _colliderTriggerBeforeDowned;
		}
		base.invisible = peerDowned;
		bool flag = amIDowned && !peerDowned;
		if (flag == _sortingBoosted)
		{
			_downedVisual = peerDowned;
			return;
		}
		_sortingBoosted = flag;
		_downedVisual = peerDowned;
		if (!((Object)(object)_torso == (Object)null) && !((Object)(object)_torso.Sprite == (Object)null))
		{
			if (flag)
			{
				_sortingOrderBeforeRescuing = _torso.Sprite.SortingOrder;
				_torso.Sprite.SortingOrder = _sortingOrderBeforeRescuing + 10;
			}
			else
			{
				_torso.Sprite.SortingOrder = _sortingOrderBeforeRescuing;
			}
		}
	}

	public void SetCutsceneHidden(bool hidden)
	{
		if (hidden != _cutsceneHidden)
		{
			_cutsceneHidden = hidden;
			if ((Object)(object)_torsoRend != (Object)null)
			{
				_torsoRend.enabled = !hidden;
			}
			if ((Object)(object)_legsRend != (Object)null)
			{
				_legsRend.enabled = !hidden && _legsVisible;
			}
			if ((Object)(object)_collider != (Object)null)
			{
				((Collider)_collider).enabled = !hidden;
			}
		}
	}

	public override void updateHealthBar()
	{
	}
}
