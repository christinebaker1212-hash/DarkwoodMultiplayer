using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class EnemyAiDiagnostics
{
	private const float Range = 4000f;

	private static bool _streaming;

	private static readonly Dictionary<Character, Transform> _lastTarget = new Dictionary<Character, Transform>();

	public static void ToggleStream()
	{
		_streaming = !_streaming;
		Plugin.Log.LogInfo($"[EnemyAiDiag] stream de troca de alvo = {_streaming}");
	}

	public static void Capture()
	{
		//IL_004d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ba: Unknown result type (might be due to invalid IL or missing references)
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Plugin.Log.LogInfo("[EnemyAiDiag] --- captura ---");
		Character[] array = SceneCache.Characters();
		foreach (Character val in array)
		{
			if (!((Object)(object)val == (Object)null) && ((CharBase)val).alive)
			{
				float num = Vector3.Distance(((Component)val).transform.position, ((Component)instance).transform.position);
				if (!(num > 4000f))
				{
					string arg = (((Object)(object)val.target != (Object)null) ? ((Object)val.target).name : "(null)");
					Plugin.Log.LogInfo($"[EnemyAiDiag] '{((Object)((Component)val).gameObject).name}' aggressiveness={val.aggressiveness} " + $"behaviour={val.behaviour} target='{arg}' dist={num:F0}");
				}
			}
		}
	}

	public static void Tick()
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0110: Unknown result type (might be due to invalid IL or missing references)
		//IL_011b: Unknown result type (might be due to invalid IL or missing references)
		if (!_streaming)
		{
			return;
		}
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		Character[] array = SceneCache.Characters();
		foreach (Character val in array)
		{
			if ((Object)(object)val == (Object)null || !((CharBase)val).alive)
			{
				continue;
			}
			float num = Vector3.Distance(((Component)val).transform.position, ((Component)instance).transform.position);
			if (!(num > 4000f))
			{
				_lastTarget.TryGetValue(val, out var value);
				if ((Object)(object)val.target != (Object)(object)value)
				{
					string text = (((Object)(object)value != (Object)null) ? ((Object)value).name : "(null)");
					string text2 = (((Object)(object)val.target != (Object)null) ? ((Object)val.target).name : "(null)");
					Plugin.Log.LogInfo("[EnemyAiDiag] '" + ((Object)((Component)val).gameObject).name + "' trocou de alvo: '" + text + "' → '" + text2 + "' " + $"(aggressiveness={val.aggressiveness} behaviour={val.behaviour} dist={num:F0})");
					_lastTarget[val] = val.target;
				}
			}
		}
	}

	public static void Reset()
	{
		_lastTarget.Clear();
	}
}
