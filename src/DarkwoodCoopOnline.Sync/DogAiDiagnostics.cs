using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DogAiDiagnostics
{
	private const float SearchRange = 6000f;

	private const float TickInterval = 0.3f;

	private static bool _streaming;

	private static float _tickTimer;

	private static Character _tracked;

	private static string _lastTargetName = "";

	private static bool _lastAttacking;

	private static Vector3 _lastPos;

	private static float _distMoved;

	public static void ToggleStream()
	{
		_streaming = !_streaming;
		Plugin.Log.LogInfo($"[DogAiDiag] stream = {_streaming}");
		if (!_streaming)
		{
			_tracked = null;
		}
	}

	public static void Capture()
	{
		Character val = FindNearestDog();
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[DogAiDiag] nenhum inimigo com 'dog' no nome encontrado por perto.");
		}
		else
		{
			Dump(val, "CAPTURA");
		}
	}

	public static void Tick()
	{
		if (!_streaming)
		{
			return;
		}
		_tickTimer += Time.deltaTime;
		if (_tickTimer < 0.3f)
		{
			return;
		}
		_tickTimer = 0f;
		Character val = (((Object)(object)_tracked != (Object)null && ((CharBase)_tracked).alive) ? _tracked : FindNearestDog());
		if (!((Object)(object)val == (Object)null))
		{
			if ((Object)(object)_tracked != (Object)(object)val)
			{
				_tracked = val;
				_lastTargetName = "";
				_lastAttacking = false;
				_lastPos = ((Component)val).transform.position;
				Plugin.Log.LogInfo($"[DogAiDiag] agora rastreando '{((Object)((Component)val).gameObject).name}' (instanceId={((Object)val).GetInstanceID()}).");
			}
			_distMoved = Vector3.Distance(((Component)val).transform.position, _lastPos);
			_lastPos = ((Component)val).transform.position;
			string obj = (((Object)(object)val.target != (Object)null) ? ((Object)val.target).name : "(null)");
			bool flag = obj != _lastTargetName || val.attacking != _lastAttacking;
			_lastTargetName = obj;
			_lastAttacking = val.attacking;
			Dump(val, flag ? "MUDOU" : "tick");
		}
	}

	private static void Dump(Character dog, string tag)
	{
		Player instance = Player.Instance;
		Transform val = (((Object)(object)RemotePlayerPuppet.DebugTorso != (Object)null) ? ((Component)RemotePlayerPuppet.DebugTorso).transform : null);
		float num = (((Object)(object)instance != (Object)null) ? Vector3.Distance(((Component)dog).transform.position, ((Component)instance).transform.position) : (-1f));
		float num2 = (((Object)(object)val != (Object)null) ? Vector3.Distance(((Component)dog).transform.position, val.position) : (-1f));
		string text = (((Object)(object)dog.target != (Object)null) ? ((Object)dog.target).name : "(null)");
		string text2 = (((Object)(object)dog.superTarget != (Object)null) ? ((Object)dog.superTarget).name : "(null)");
		Plugin.Log.LogInfo($"[DogAiDiag][{tag}] '{((Object)((Component)dog).gameObject).name}' hp={((CharBase)dog).health:F0} alive={((CharBase)dog).alive} " + $"behaviour={dog.behaviour} aggressiveness={dog.aggressiveness} " + "target='" + text + "' superTarget='" + text2 + "' " + $"attacking={dog.attacking} recoveringFromAttack={dog.recoveringFromAttack} recoveringFromHit={dog.recoveringFromHit} " + $"walking={dog.walking} running={dog.running} distMovido={_distMoved:F0} " + $"enemyInSight={dog.enemyInSight} canSeeEnemyNear={dog.canSeeEnemyNear} canSeeEnemyFar={dog.canSeeEnemyFar} " + $"distHost={num:F0} distPuppet={num2:F0} " + $"hpLocal={(((Object)(object)instance != (Object)null) ? ((CharBase)instance).health : (-1f)):F0} hpPeer={PlayerSync.PeerHealth:F0}");
	}

	public static void CaptureAnimators()
	{
		Character val = FindNearestTracked();
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[DogAiDiag][Anim] nenhuma criatura rastreada (owned/puppet) encontrada por perto.");
			return;
		}
		tk2dSpriteAnimator val2 = EnemySync.DebugBodyAnim(val);
		Plugin.Log.LogInfo($"[DogAiDiag][Anim] --- '{((Object)((Component)val).gameObject).name}' (instanceId={((Object)val).GetInstanceID()}) ---");
		Plugin.Log.LogInfo("[DogAiDiag][Anim] animador USADO pelo sync: '" + (((Object)(object)val2 != (Object)null) ? ((Object)((Component)val2).gameObject).name : "(null)") + "' clipAtual='" + (((Object)(object)val2 != (Object)null && val2.CurrentClip != null) ? val2.CurrentClip.name : "(null)") + "'");
		Plugin.Log.LogInfo("[DogAiDiag][Anim] legsAnimator nativo: '" + (((Object)(object)val.legsAnimator != (Object)null) ? ((Object)((Component)val.legsAnimator).gameObject).name : "(null)") + "' clipAtual='" + (((Object)(object)val.legsAnimator != (Object)null && val.legsAnimator.CurrentClip != null) ? val.legsAnimator.CurrentClip.name : "(null)") + "'");
		if ((Object)(object)val2 != (Object)null)
		{
			Plugin.Log.LogInfo($"[DogAiDiag][Anim] estado do animador usado: Playing={val2.Playing} " + $"isVisible={val2.isVisible} IgnoreCameraVisibility={val2.IgnoreCameraVisibility}");
			if ((Object)(object)val2.Library != (Object)null && val2.Library.clips != null)
			{
				Plugin.Log.LogInfo($"[DogAiDiag][Anim] clipes da library ({val2.Library.clips.Length}):");
				tk2dSpriteAnimationClip[] clips = val2.Library.clips;
				foreach (tk2dSpriteAnimationClip val3 in clips)
				{
					if (val3 != null)
					{
						int num = ((val3.frames != null) ? val3.frames.Length : 0);
						Plugin.Log.LogInfo($"[DogAiDiag][Anim]  '{val3.name}' wrapMode={val3.wrapMode} frames={num} fps={val3.fps:F0}");
					}
				}
			}
		}
		tk2dSpriteAnimator[] componentsInChildren = ((Component)val).GetComponentsInChildren<tk2dSpriteAnimator>(true);
		Plugin.Log.LogInfo($"[DogAiDiag][Anim] {componentsInChildren.Length} tk2dSpriteAnimator na hierarquia:");
		tk2dSpriteAnimator[] array = componentsInChildren;
		foreach (tk2dSpriteAnimator val4 in array)
		{
			bool flag = (Object)(object)val4 == (Object)(object)val2;
			bool num2 = (Object)(object)val4 == (Object)(object)val.legsAnimator;
			string text = ((val4.CurrentClip != null) ? val4.CurrentClip.name : "(null)");
			bool flag2 = val4.GetClipByName("Walk") != null;
			bool flag3 = val4.GetClipByName("Run") != null;
			int num3 = (((Object)(object)val4.Library != (Object)null && val4.Library.clips != null) ? val4.Library.clips.Length : 0);
			string text2 = (flag ? "[USADO-BODY] " : "             ");
			string text3 = (num2 ? "[LEGS] " : "       ");
			Plugin.Log.LogInfo("[DogAiDiag][Anim]  " + text2 + text3 + "'" + ((Object)((Component)val4).gameObject).name + "' " + $"clipAtual='{text}' temWalk={flag2} temRun={flag3} totalClipes={num3}");
		}
	}

	private static Character FindNearestDog()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		Character result = null;
		float num = 6000f;
		Character[] array = SceneCache.Characters();
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null) && ((CharBase)val).alive && ((Object)((Component)val).gameObject).name.IndexOf("dog", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, ((Component)instance).transform.position);
				if (num2 < num)
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}

	private static Character FindNearestTracked()
	{
		Player p = Player.Instance;
		if ((Object)(object)p == (Object)null)
		{
			return null;
		}
		Character best = null;
		float bestD = 6000f;
		foreach (var owned in EnemySync.GetOwnedList())
		{
			Consider(owned.c);
		}
		foreach (var puppet in EnemySync.GetPuppetList())
		{
			Consider(puppet.c);
		}
		return best;
		void Consider(Character c)
		{
			if (!((Object)(object)c == (Object)null) && ((CharBase)c).alive)
			{
				float num = Vector3.Distance(((Component)c).transform.position, ((Component)p).transform.position);
				if (num < bestD)
				{
					bestD = num;
					best = c;
				}
			}
		}
	}

	public static void Reset()
	{
		_streaming = false;
		_tracked = null;
		_tickTimer = 0f;
		_lastTargetName = "";
		_lastAttacking = false;
	}
}
