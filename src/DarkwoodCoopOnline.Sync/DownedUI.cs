using DarkwoodCoopOnline;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DownedUI
{
	private static GUIStyle _countdownStyle;

	private static GUIStyle _barBgStyle;

	private static GUIStyle _barFillStyle;

	private static GUIStyle _hintStyle;

	private static GUIStyle _markStyle;

	private static GUIStyle _hintBg;

	public static void Draw(RemotePlayerPuppet peerPuppet)
	{
		EnsureStyles();
		if (DownedSync.Downed)
		{
			DrawCountdown();
			Hint(new Rect(((float)Screen.width - 560f) * 0.5f, (float)Screen.height * 0.3f + 130f, 560f, 30f), "You're down! A friend can revive you by holding F (or " + ReviveButtonName() + " on a controller) next to you.");
		}
		DrawDownedFriends();
		if (DownedSync.PeerDowned && DownedSync.HoldingRevive && (Object)(object)peerPuppet != (Object)null)
		{
			DrawReviveBar(peerPuppet);
		}
	}

	public static string ReviveButtonName()
	{
		switch ((int)(CoopConfig.ReviveButton?.Value ?? ((KeyCode)330)))
		{
		case 330:
			return "A";
		case 331:
			return "B";
		case 332:
			return "X";
		case 333:
			return "Y";
		case 0:
			return "interact";
		default:
			return CoopConfig.ReviveButton.Value.ToString();
		}
	}

	private static void Hint(Rect r, string text)
	{
		GUI.Box(r, GUIContent.none, _hintBg);
		GUI.Label(r, text, _hintStyle);
	}

	// Every downed friend: a line at the top with time left, and a marker on them (or at the edge of the
	// screen pointing their way): "hold F" when you're close enough
	private static void DrawDownedFriends()
	{
		Player instance = Player.Instance;
		Camera val = (((Object)(object)Singleton<CamMain>.Instance != (Object)null) ? ((Component)Singleton<CamMain>.Instance).GetComponent<Camera>() : null);
		if ((Object)(object)instance == (Object)null || (Object)(object)val == (Object)null)
		{
			return;
		}
		int num = 0;
		foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
		{
			if ((Object)(object)puppet == (Object)null || !puppet.HasState || !DownedSync.IsPeerDowned(puppet.Slot))
			{
				continue;
			}
			string text = (string.IsNullOrEmpty(puppet.PeerName) ? "A friend" : puppet.PeerName);
			int num2 = Mathf.CeilToInt(DownedSync.PeerTimeLeft(puppet.Slot));
			float num3 = Vector3.Distance(((Component)instance).transform.position, ((Component)puppet).transform.position);
			bool flag = num3 <= DownedSync.ReviveRange;
			if (!DownedSync.Downed)
			{
				Hint(new Rect(((float)Screen.width - 600f) * 0.5f, 54f + (float)num * 34f, 600f, 30f), flag ? $"{text} is down - HOLD F / {ReviveButtonName()} to revive them ({num2}s)" : $"{text} is down! Go to them and hold F / {ReviveButtonName()} to revive ({num2}s)");
				num++;
			}
			Vector3 val2 = val.WorldToScreenPoint(((Component)puppet).transform.position);
			float x = val2.x;
			float num4 = (float)Screen.height - val2.y;
			bool flag2 = val2.z > 0f && x >= 0f && x <= (float)Screen.width && num4 >= 0f && num4 <= (float)Screen.height;
			string text2 = (flag ? ("HOLD F / " + ReviveButtonName()) : ("DOWN " + num2 + "s"));
			if (flag2)
			{
				if (!DownedSync.HoldingRevive)
				{
					Hint(new Rect(x - 70f, num4 - 120f, 140f, 26f), text2);
				}
				continue;
			}
			// off screen: clamp to the edge in their direction
			Vector2 vector = new Vector2(x - (float)Screen.width * 0.5f, num4 - (float)Screen.height * 0.5f);
			if (val2.z <= 0f)
			{
				vector = -vector;
			}
			if (vector.sqrMagnitude < 1f)
			{
				vector = new Vector2(0f, -1f);
			}
			float num5 = Mathf.Min(((float)Screen.width * 0.5f - 90f) / Mathf.Max(Mathf.Abs(vector.x), 0.001f), ((float)Screen.height * 0.5f - 40f) / Mathf.Max(Mathf.Abs(vector.y), 0.001f));
			Vector2 vector2 = new Vector2((float)Screen.width * 0.5f, (float)Screen.height * 0.5f) + vector * num5;
			string text3 = ((Mathf.Abs(vector.x) > Mathf.Abs(vector.y)) ? ((vector.x > 0f) ? (text + " >") : ("< " + text)) : ((vector.y > 0f) ? (text + " v") : (text + " ^")));
			GUI.Box(new Rect(vector2.x - 80f, vector2.y - 14f, 160f, 28f), GUIContent.none, _hintBg);
			GUI.Label(new Rect(vector2.x - 80f, vector2.y - 14f, 160f, 28f), text3 + " (down)", _markStyle);
		}
	}

	private static void DrawCountdown()
	{
		float num = Mathf.Max(0f, 75f - DownedSync.DownedTimer);
		string text = Mathf.CeilToInt(num).ToString();
		float num2 = 260f;
		float num3 = 130f;
		Rect val = new Rect(((float)Screen.width - num2) * 0.5f, (float)Screen.height * 0.3f, num2, num3);
		bool flag = num <= 10f;
		_countdownStyle.normal.textColor = (flag ? new Color(1f, 0.15f, 0.15f) : new Color(1f, 0.55f, 0.2f));
		_countdownStyle.fontSize = (flag ? 96 : 84);
		GUI.Label(val, text, _countdownStyle);
	}

	private static void DrawReviveBar(RemotePlayerPuppet peerPuppet)
	{
		Camera val = (((Object)(object)Singleton<CamMain>.Instance != (Object)null) ? ((Component)Singleton<CamMain>.Instance).GetComponent<Camera>() : null);
		if (!((Object)(object)val == (Object)null))
		{
			Vector3 val2 = val.WorldToScreenPoint(((Component)peerPuppet).transform.position);
			if (!(val2.z <= 0f))
			{
				float x = val2.x;
				float num = (float)Screen.height - val2.y;
				float num2 = 180f;
				float num3 = 20f;
				Rect val3 = default(Rect);
				val3 = new Rect(x - num2 * 0.5f, num - 90f, num2, num3);
				GUI.Box(val3, GUIContent.none, _barBgStyle);
				float num4 = Mathf.Clamp01(DownedSync.ReviveHoldTimer / 3.5f);
				GUI.Box(new Rect(val3.x + 2f, val3.y + 2f, (num2 - 4f) * num4, num3 - 4f), GUIContent.none, _barFillStyle);
			}
		}
	}

	private static void EnsureStyles()
	{
		if (_hintStyle == null)
		{
			_hintStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 16,
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			_hintStyle.normal.textColor = new Color(1f, 0.82f, 0.45f);
			_markStyle = new GUIStyle(_hintStyle)
			{
				fontSize = 13
			};
			_markStyle.normal.textColor = new Color(1f, 0.45f, 0.35f);
			Texture2D val0 = new Texture2D(1, 1);
			val0.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.6f));
			val0.Apply();
			_hintBg = new GUIStyle();
			_hintBg.normal.background = val0;
		}
		if (_countdownStyle == null)
		{
			_countdownStyle = new GUIStyle
			{
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			Texture2D val = new Texture2D(1, 1);
			val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.65f));
			val.Apply();
			GUIStyle val2 = new GUIStyle();
			val2.normal.background = val;
			_barBgStyle = val2;
			Texture2D val3 = new Texture2D(1, 1);
			val3.SetPixel(0, 0, new Color(0.92f, 0.92f, 0.95f, 0.95f));
			val3.Apply();
			GUIStyle val4 = new GUIStyle();
			val4.normal.background = val3;
			_barFillStyle = val4;
		}
	}
}
