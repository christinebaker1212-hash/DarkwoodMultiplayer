using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class VoiceSettingsUI
{
	private static bool _expanded;

	private static Vector2 _scroll;

	private static GUIStyle _boxStyle;

	private static GUIStyle _titleStyle;

	private static GUIStyle _hintStyle;

	private static GUIStyle _selectedStyle;

	public static void Draw()
	{
		EnsureStyles();
		float num = (float)Screen.width - 170f - 10f;
		float num2 = (float)Screen.height - 26f - 10f;
		string text = ((!VoiceSync.Enabled) ? "Voice chat: OFF" : (VoiceSync.IsSpeaking ? "Voice chat: ON (speaking)" : "Voice chat: ON"));
		if (GUI.Button(new Rect(num, num2, 170f, 26f), text))
		{
			_expanded = !_expanded;
		}
		if (!_expanded)
		{
			return;
		}
		string[] devices = Microphone.devices;
		float num3 = Mathf.Clamp((float)devices.Length * 22f, 22f, 110f);
		float num4 = 74f + num3;
		float num5 = num2 - num4 - 4f;
		GUI.Box(new Rect(num, num5, 170f, num4), GUIContent.none, _boxStyle);
		float num6 = num5 + 6f;
		GUI.Label(new Rect(num + 6f, num6, 158f, 18f), "Proximity voice chat", _titleStyle);
		num6 += 20f;
		if (GUI.Button(new Rect(num + 6f, num6, 158f, 22f), VoiceSync.Enabled ? "Disable" : "Enable"))
		{
			Plugin.Instance?.SetVoiceEnabled(!VoiceSync.Enabled);
		}
		num6 += 26f;
		GUI.Label(new Rect(num + 6f, num6, 158f, 16f), "Microphone:", _hintStyle);
		num6 += 18f;
		if (devices.Length == 0)
		{
			GUI.Label(new Rect(num + 6f, num6, 158f, 20f), "No microphone found.", _hintStyle);
			return;
		}
		Rect val = new Rect(num + 6f, num6, 158f, num3);
		Rect val2 = default(Rect);
		val2 = new Rect(0f, 0f, 140f, (float)devices.Length * 22f);
		_scroll = GUI.BeginScrollView(val, _scroll, val2);
		for (int i = 0; i < devices.Length; i++)
		{
			bool flag = devices[i] == VoiceSync.Device;
			if (GUI.Button(new Rect(0f, (float)i * 22f, 140f, 20f), devices[i], flag ? _selectedStyle : GUI.skin.button))
			{
				Plugin.Instance?.SetVoiceDevice(devices[i]);
			}
		}
		GUI.EndScrollView();
	}

	private static void EnsureStyles()
	{
		if (_boxStyle == null)
		{
			Texture2D val = new Texture2D(1, 1);
			val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.8f));
			val.Apply();
			GUIStyle val2 = new GUIStyle();
			val2.normal.background = val;
			_boxStyle = val2;
			_titleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				fontStyle = (FontStyle)1
			};
			_titleStyle.normal.textColor = Color.white;
			_hintStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11
			};
			_hintStyle.normal.textColor = new Color(0.8f, 0.8f, 0.75f);
			_selectedStyle = new GUIStyle(GUI.skin.button);
			_selectedStyle.normal.textColor = new Color(0.5f, 1f, 0.5f);
		}
	}
}
