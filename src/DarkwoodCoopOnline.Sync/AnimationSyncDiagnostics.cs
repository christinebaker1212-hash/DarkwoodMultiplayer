using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class AnimationSyncDiagnostics
{
	private const float TickInterval = 1f;

	private const float MovingThreshold = 5f;

	private static float _timer;

	private static readonly Dictionary<Character, Vector3> _lastPos = new Dictionary<Character, Vector3>();

	public static void Tick()
	{
		_timer += Time.deltaTime;
		if (!(_timer < 1f))
		{
			_timer = 0f;
			CheckList(EnemySync.GetOwnedList(), "MEU(real)");
			CheckList(EnemySync.GetPuppetList(), "PUPPET(peer)");
		}
	}

	private static void CheckList(List<(Character c, string id)> list, string tag)
	{
		foreach (var (val, text) in list)
		{
			if ((Object)(object)val == (Object)null || !((CharBase)val).alive)
			{
				continue;
			}
			Vector3 position = ((Component)val).transform.position;
			Vector3 value;
			bool num = !_lastPos.TryGetValue(val, out value);
			float num2 = (num ? 0f : Vector3.Distance(position, value));
			_lastPos[val] = position;
			if (!num)
			{
				bool flag = num2 > 5f;
				tk2dSpriteAnimator val2 = EnemySync.DebugBodyAnim(val);
				string text2 = (((Object)(object)val2 != (Object)null && val2.CurrentClip != null) ? val2.CurrentClip.name : "(sem animator/clipe)");
				string text3 = (((Object)(object)val.legsAnimator != (Object)null && val.legsAnimator.CurrentClip != null) ? val.legsAnimator.CurrentClip.name : "(sem pernas/clipe)");
				bool flag2 = LooksLikeMoving(text2);
				bool flag3 = LooksLikeMoving(text3);
				if (flag && !flag3)
				{
					Plugin.Log.LogWarning($"[AnimSyncDiag][{tag}] '{((Object)((Component)val).gameObject).name}' (id={text}) pos=({position.x:F0},{position.z:F0}) parece andando " + $"(walking={val.walking} running={val.running} distMovida={num2:F1}) MAS pernas tocam '{text3}' " + $"(sem Walk/Run) - possível 'deslizando no chão'. behaviour={val.behaviour} clipeTorso='{text2}'.");
				}
				else if (!flag && flag3)
				{
					Plugin.Log.LogWarning($"[AnimSyncDiag][{tag}] '{((Object)((Component)val).gameObject).name}' (id={text}) pos=({position.x:F0},{position.z:F0}) parece PARADO " + $"(walking={val.walking} running={val.running} distMovida={num2:F1}) MAS pernas tocam '{text3}' " + $"(tem Walk/Run) - animação presa em movimento. behaviour={val.behaviour} clipeTorso='{text2}'.");
				}
				if (flag && !flag2 && !val.attacking)
				{
					Plugin.Log.LogWarning($"[AnimSyncDiag][{tag}] '{((Object)((Component)val).gameObject).name}' (id={text}) pos=({position.x:F0},{position.z:F0}) parece andando " + $"mas o TORSO toca '{text2}' (sem Walk/Run, e não está atacando). behaviour={val.behaviour} " + $"walking={val.walking} running={val.running} distMovida={num2:F1}.");
				}
			}
		}
	}

	private static bool LooksLikeMoving(string clipName)
	{
		if (clipName.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) < 0)
		{
			return clipName.IndexOf("Run", StringComparison.OrdinalIgnoreCase) >= 0;
		}
		return true;
	}

	public static void Reset()
	{
		_timer = 0f;
		_lastPos.Clear();
	}
}
