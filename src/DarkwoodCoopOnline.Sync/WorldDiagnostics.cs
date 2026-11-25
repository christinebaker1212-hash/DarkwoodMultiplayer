using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class WorldDiagnostics
{
	public static void Dump()
	{
		//IL_0026: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0125: Unknown result type (might be due to invalid IL or missing references)
		//IL_012a: Unknown result type (might be due to invalid IL or missing references)
		Player instance = Player.Instance;
		TimestampedLogSource log = Plugin.Log;
		object obj;
		Vector3 position;
		if (!((Object)(object)instance != (Object)null))
		{
			obj = "null";
		}
		else
		{
			position = ((Component)instance).transform.position;
			obj = position.ToString("F0");
		}
		log.LogInfo("[Diag] Player pos=" + (string?)obj);
		Character[] array = Object.FindObjectsOfType<Character>();
		SaveGameManager instance2 = SaveGameManager.Instance;
		Plugin.Log.LogInfo(string.Format("[Diag] {0} Character(s) na cena (SaveGameManager={1}):", array.Length, ((Object)(object)instance2 != (Object)null) ? "ok" : "null"));
		int num = 0;
		Character[] array2 = array;
		foreach (Character val in array2)
		{
			if (!((Object)(object)val == (Object)null))
			{
				string text = "?";
				try
				{
					text = SaveGameManager.GetId(((Component)val).gameObject);
				}
				catch
				{
					text = "err";
				}
				TimestampedLogSource log2 = Plugin.Log;
				string text2 = $"[Diag]  #{num} type={((CharBase)val).type} faction={((CharBase)val).faction} alive={((CharBase)val).alive} ";
				object arg = ((CharBase)val).health;
				position = ((Component)val).transform.position;
				log2.LogInfo(text2 + string.Format("hp={0:F0} pos={1} id={2}", arg, position.ToString("F0"), text));
				if (++num >= 50)
				{
					Plugin.Log.LogInfo("[Diag]  ... (limitado a 50)");
					break;
				}
			}
		}
		Plugin.Log.LogInfo("[Diag] fim.");
	}

	public static void DumpPlayerTree()
	{
		if ((Object)(object)Player.Instance == (Object)null)
		{
			Plugin.Log.LogInfo("[Tree] Player null");
			return;
		}
		Plugin.Log.LogInfo("[Tree] === hierarquia do Player (com arma equipada) ===");
		DumpTree(((Component)Player.Instance).transform, 0);
		Plugin.Log.LogInfo("[Tree] fim.");
		DumpTorso();
	}

	public static void DumpTorso()
	{
		LogAnim("REAL  ", ((Object)(object)Player.Instance != (Object)null) ? Player.Instance.torsoAnimator : null);
		LogAnim("PUPPET", RemotePlayerPuppet.DebugTorso);
	}

	private static void LogAnim(string tag, tk2dSpriteAnimator a)
	{
		if ((Object)(object)a == (Object)null)
		{
			Plugin.Log.LogInfo("[Torso2] " + tag + ": (null)");
			return;
		}
		string text = (((Object)(object)a.Library != (Object)null) ? ((Object)a.Library).name : "?");
		string text2 = ((a.CurrentClip != null) ? a.CurrentClip.name : "?");
		string text3 = "?";
		string text4 = "?";
		tk2dBaseSprite sprite = a.Sprite;
		if ((Object)(object)sprite != (Object)null)
		{
			text3 = (((Object)(object)sprite.Collection != (Object)null) ? ((Object)sprite.Collection).name : "?");
			tk2dSpriteDefinition currentSprite = sprite.CurrentSprite;
			text4 = ((currentSprite != null) ? currentSprite.name : "?");
		}
		Plugin.Log.LogInfo("[Torso2] " + tag + ": lib=" + text + "  clip=" + text2 + "  coll=" + text3 + "  sprite=" + text4);
	}

	private static void DumpTree(Transform t, int depth)
	{
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c0: Expected O, but got Unknown
		List<string> list = new List<string>();
		Component[] components = ((Component)t).GetComponents<Component>();
		foreach (Component val in components)
		{
			if ((Object)(object)val != (Object)null && !(val is Transform))
			{
				list.Add(((object)val).GetType().Name);
			}
		}
		string text = new string(' ', depth * 2);
		Plugin.Log.LogInfo(string.Format("[Tree] {0}{1}  [{2}]  active={3}", text, ((Object)t).name, string.Join(",", list.ToArray()), ((Component)t).gameObject.activeSelf));
		if (depth >= 4)
		{
			return;
		}
		foreach (Transform item in t)
		{
			DumpTree(item, depth + 1);
		}
	}
}
