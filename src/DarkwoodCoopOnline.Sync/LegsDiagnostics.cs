using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkwoodCoopOnline.Sync;

public static class LegsDiagnostics
{
	private const BindingFlags AllInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static FieldInfo _meshColorsField;

	private static bool _streaming;

	private static float _lastLog = -999f;

	private const float LogInterval = 0.125f;

	public static void Capture()
	{
		Plugin.Log.LogInfo("========================= [LegsDiag] CAPTURA =========================");
		tk2dSpriteAnimator a = (((Object)(object)Player.Instance != (Object)null) ? Player.Instance.torsoAnimator : null);
		tk2dSpriteAnimator a2 = null;
		Transform val = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.Find("PlayerLegs") : null);
		if ((Object)(object)val != (Object)null)
		{
			a2 = ((Component)val).GetComponent<tk2dSpriteAnimator>();
		}
		DumpPart("REAL torso ", a);
		DumpPart("REAL legs  ", a2);
		DumpPart("PUPPET torso", RemotePlayerPuppet.DebugTorso);
		DumpPart("PUPPET legs ", RemotePlayerPuppet.DebugLegs);
		Plugin.Log.LogInfo("[LegsDiag] --- hierarquia REAL PlayerLegs (filhos) ---");
		if ((Object)(object)val != (Object)null)
		{
			DumpTree(val, 0);
		}
		else
		{
			Plugin.Log.LogInfo("[LegsDiag]   (REAL legs null)");
		}
		Plugin.Log.LogInfo("[LegsDiag] --- hierarquia PUPPET legs (filhos) ---");
		if ((Object)(object)RemotePlayerPuppet.DebugLegs != (Object)null)
		{
			DumpTree(((Component)RemotePlayerPuppet.DebugLegs).transform, 0);
		}
		else
		{
			Plugin.Log.LogInfo("[LegsDiag]   (PUPPET legs null)");
		}
		Plugin.Log.LogInfo("======================================================================");
	}

	private static void DumpPart(string tag, tk2dSpriteAnimator a)
	{
		//IL_03cb: Unknown result type (might be due to invalid IL or missing references)
		//IL_03d0: Unknown result type (might be due to invalid IL or missing references)
		//IL_03ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_03f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_03fd: Unknown result type (might be due to invalid IL or missing references)
		//IL_041c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0421: Unknown result type (might be due to invalid IL or missing references)
		//IL_02cc: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d1: Unknown result type (might be due to invalid IL or missing references)
		//IL_0391: Unknown result type (might be due to invalid IL or missing references)
		//IL_0351: Unknown result type (might be due to invalid IL or missing references)
		//IL_0374: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)a == (Object)null)
		{
			Plugin.Log.LogInfo("[LegsDiag] " + tag + ": (null)");
			return;
		}
		GameObject gameObject = ((Component)a).gameObject;
		Renderer component = ((Component)a).GetComponent<Renderer>();
		string text = ((a.CurrentClip != null) ? a.CurrentClip.name : "?");
		string text2 = (((Object)(object)a.Library != (Object)null) ? ((Object)a.Library).name : "?");
		tk2dSpriteAnimator legs = a.legs;
		string text3 = (((Object)(object)legs != (Object)null) ? $"'{((Object)((Component)legs).gameObject).name}'(id={((Object)legs).GetInstanceID()})" : "null");
		Plugin.Log.LogInfo($"[LegsDiag] {tag}: go='{((Object)gameObject).name}'(id={((Object)gameObject).GetInstanceID()}) " + $"active={gameObject.activeSelf}/{gameObject.activeInHierarchy} layer={gameObject.layer} " + "lib=" + text2 + " clip=" + text + " animator.legs→" + text3);
		List<string> list = new List<string>();
		Component[] components = gameObject.GetComponents<Component>();
		foreach (Component val in components)
		{
			if ((Object)(object)val != (Object)null && !(val is Transform))
			{
				list.Add(((object)val).GetType().Name);
			}
		}
		Plugin.Log.LogInfo("[LegsDiag] " + tag + ":   componentes=[" + string.Join(",", list.ToArray()) + "]");
		if ((Object)(object)component != (Object)null)
		{
			Material sharedMaterial = component.sharedMaterial;
			string text4 = (((Object)(object)sharedMaterial != (Object)null) ? ((Object)sharedMaterial).name : "null");
			string text5 = (((Object)(object)sharedMaterial != (Object)null && (Object)(object)sharedMaterial.shader != (Object)null) ? ((Object)sharedMaterial.shader).name : "null");
			Plugin.Log.LogInfo($"[LegsDiag] {tag}:   renderer enabled={component.enabled} sortLayer={component.sortingLayerName} " + $"sortOrder={component.sortingOrder} sharedMaterial='{text4}'(id={(((Object)(object)sharedMaterial != (Object)null) ? ((Object)sharedMaterial).GetInstanceID() : 0)}) shader='{text5}'");
			DumpShaderProps(tag, sharedMaterial);
		}
		else
		{
			Plugin.Log.LogInfo("[LegsDiag] " + tag + ":   sem Renderer");
		}
		tk2dBaseSprite sprite = a.Sprite;
		if ((Object)(object)sprite != (Object)null)
		{
			Color color = sprite.color;
			string arg = "n/a";
			tk2dSprite val2 = (tk2dSprite)(object)((sprite is tk2dSprite) ? sprite : null);
			if (val2 != null)
			{
				if (_meshColorsField == null)
				{
					_meshColorsField = typeof(tk2dSprite).GetField("meshColors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}
				if (_meshColorsField?.GetValue(val2) is Color32[] array && array.Length != 0)
				{
					arg = $"len={array.Length} v0={array[0]} v{array.Length / 2}={array[array.Length / 2]}";
				}
			}
			Plugin.Log.LogInfo($"[LegsDiag] {tag}:   sprite.color={color} meshColors[{arg}]");
		}
		TimestampedLogSource log = Plugin.Log;
		string[] obj = new string[8] { "[LegsDiag] ", tag, ":   wpos=", null, null, null, null, null };
		Vector3 val3 = gameObject.transform.position;
		obj[3] = val3.ToString("F1");
		obj[4] = " wrot=";
		Quaternion rotation = gameObject.transform.rotation;
		val3 = rotation.eulerAngles;
		obj[5] = val3.ToString("F1");
		obj[6] = " lossyScale=";
		val3 = gameObject.transform.lossyScale;
		obj[7] = val3.ToString("F2");
		log.LogInfo(string.Concat(obj));
	}

	private static void DumpShaderProps(string tag, Material mat)
	{
		//IL_0037: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Unknown result type (might be due to invalid IL or missing references)
		//IL_003e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0059: Expected I4, but got Unknown
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0065: Unknown result type (might be due to invalid IL or missing references)
		//IL_007a: Unknown result type (might be due to invalid IL or missing references)
		//IL_007f: Unknown result type (might be due to invalid IL or missing references)
		//IL_010c: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)mat == (Object)null || (Object)(object)mat.shader == (Object)null)
		{
			return;
		}
		Shader shader = mat.shader;
		int propertyCount = shader.GetPropertyCount();
		for (int i = 0; i < propertyCount; i++)
		{
			string propertyName = shader.GetPropertyName(i);
			ShaderPropertyType propertyType = shader.GetPropertyType(i);
			string text;
			switch ((int)propertyType)
			{
			case 0:
			{
				Color color = mat.GetColor(propertyName);
				text = ((object)color).ToString();
				break;
			}
			case 1:
			{
				Vector4 vector = mat.GetVector(propertyName);
				text = ((object)vector).ToString();
				break;
			}
			case 2:
			case 3:
				text = mat.GetFloat(propertyName).ToString("F3");
				break;
			case 4:
			{
				Texture texture = mat.GetTexture(propertyName);
				text = (((Object)(object)texture != (Object)null) ? (((Object)texture).name + "(" + ((object)texture).GetType().Name + ")") : "null");
				break;
			}
			default:
				text = "?";
				break;
			}
			Plugin.Log.LogInfo($"[LegsDiag] {tag}:   shaderProp[{propertyName}] {propertyType} = {text}");
		}
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
		Plugin.Log.LogInfo(string.Format("[LegsDiag]   {0}'{1}' [{2}] active={3}", text, ((Object)t).name, string.Join(",", list.ToArray()), ((Component)t).gameObject.activeSelf));
		if (depth >= 3)
		{
			return;
		}
		foreach (Transform item in t)
		{
			DumpTree(item, depth + 1);
		}
	}

	public static void ToggleStream()
	{
		_streaming = !_streaming;
		Plugin.Log.LogInfo($"[LegsDiag] streaming = {_streaming}");
		if (_streaming)
		{
			Plugin.Log.LogInfo("[LegsWatch] t | tag | active | rendEnabled | clip | color | v0 | pos");
		}
	}

	public static void Tick()
	{
		if (_streaming && !(Time.time - _lastLog < 0.125f))
		{
			_lastLog = Time.time;
			Transform val = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.Find("PlayerLegs") : null);
			tk2dSpriteAnimator legs = (((Object)(object)val != (Object)null) ? ((Component)val).GetComponent<tk2dSpriteAnimator>() : null);
			LogLine("REAL  ", legs);
			LogLine("PUPPET", RemotePlayerPuppet.DebugLegs);
		}
	}

	private static void LogLine(string tag, tk2dSpriteAnimator legs)
	{
		//IL_004b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0043: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Unknown result type (might be due to invalid IL or missing references)
		//IL_011f: Unknown result type (might be due to invalid IL or missing references)
		//IL_012e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		if ((Object)(object)legs == (Object)null)
		{
			return;
		}
		GameObject gameObject = ((Component)legs).gameObject;
		string arg = ((legs.CurrentClip != null) ? legs.CurrentClip.name : "?");
		Renderer component = ((Component)legs).GetComponent<Renderer>();
		tk2dBaseSprite sprite = legs.Sprite;
		Color val = (((Object)(object)sprite != (Object)null) ? sprite.color : Color.clear);
		string text = "?";
		tk2dSprite val2 = (tk2dSprite)(object)((sprite is tk2dSprite) ? sprite : null);
		if (val2 != null)
		{
			if (_meshColorsField == null)
			{
				_meshColorsField = typeof(tk2dSprite).GetField("meshColors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (_meshColorsField?.GetValue(val2) is Color32[] array && array.Length != 0)
			{
				text = ((object)array[0]).ToString();
			}
		}
		TimestampedLogSource log = Plugin.Log;
		string text2 = $"[LegsWatch] t={Time.time:F2} {tag} active={gameObject.activeSelf} ";
		string text3 = $"rendEnabled={(((Object)(object)component != (Object)null) ? new bool?(component.enabled) : null)} clip={arg} ";
		object arg2 = val;
		string arg3 = text;
		Vector3 position = gameObject.transform.position;
		log.LogInfo(text2 + text3 + string.Format("color={0} v0={1} pos={2}", arg2, arg3, position.ToString("F1")));
	}

	public static void Reset()
	{
		_streaming = false;
	}
}
