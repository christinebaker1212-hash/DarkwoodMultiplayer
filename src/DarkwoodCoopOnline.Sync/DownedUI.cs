using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DownedUI
{
	private static GUIStyle _countdownStyle;

	private static GUIStyle _barBgStyle;

	private static GUIStyle _barFillStyle;

	public static void Draw(RemotePlayerPuppet peerPuppet)
	{
		EnsureStyles();
		if (DownedSync.Downed)
		{
			DrawCountdown();
		}
		if (DownedSync.PeerDowned && DownedSync.HoldingRevive && (Object)(object)peerPuppet != (Object)null)
		{
			DrawReviveBar(peerPuppet);
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
