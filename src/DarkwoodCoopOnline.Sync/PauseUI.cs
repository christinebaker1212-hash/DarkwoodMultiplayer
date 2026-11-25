using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PauseUI
{
	private static GUIStyle _style;

	private static GUIStyle _bgStyle;

	public static void Draw(NetRole myRole)
	{
		if (PauseSync.PeerWantsPause)
		{
			EnsureStyles();
			string text2 = "PAUSED BY " + PauseSync.PausedByNames.ToUpperInvariant();
			float num = 420f;
			float num2 = 50f;
			Rect val = new Rect(((float)Screen.width - num) * 0.5f, 90f, num, num2);
			GUI.Box(val, GUIContent.none, _bgStyle);
			GUI.Label(val, text2, _style);
		}
	}

	private static void EnsureStyles()
	{
		if (_style == null)
		{
			_style = new GUIStyle
			{
				fontSize = 22,
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			_style.normal.textColor = new Color(1f, 0.9f, 0.3f);
			Texture2D val = new Texture2D(1, 1);
			val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.7f));
			val.Apply();
			GUIStyle val2 = new GUIStyle();
			val2.normal.background = val;
			_bgStyle = val2;
		}
	}
}
