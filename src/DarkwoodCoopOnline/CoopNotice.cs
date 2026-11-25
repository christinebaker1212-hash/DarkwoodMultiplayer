using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline;

// Short messages in the corner of the screen ("Ann joined the game", "Invite code copied"...).
public static class CoopNotice
{
	private struct Item
	{
		public string Text;

		public float Until;
	}

	private static readonly List<Item> Items = new List<Item>();

	private static GUIStyle _label;

	private static GUIStyle _bg;

	public static void Show(string text, float seconds = 6f)
	{
		if (string.IsNullOrEmpty(text))
		{
			return;
		}
		Items.RemoveAll((Item i) => i.Text == text);
		Items.Add(new Item
		{
			Text = text,
			Until = Time.unscaledTime + seconds
		});
		if (Items.Count > 5)
		{
			Items.RemoveAt(0);
		}
		Plugin.Log?.LogInfo("[Notice] " + text);
	}

	public static void Draw()
	{
		if (Items.Count == 0)
		{
			return;
		}
		float unscaledTime = Time.unscaledTime;
		Items.RemoveAll((Item i) => i.Until < unscaledTime);
		if (_label == null)
		{
			_bg = CoopUI.BoxStyle(0.7f);
			_label = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				wordWrap = true,
				alignment = (TextAnchor)3
			};
			_label.normal.textColor = new Color(0.92f, 0.9f, 0.82f);
		}
		float num = 16f;
		float num2 = (float)Screen.height * 0.62f;
		for (int j = 0; j < Items.Count; j++)
		{
			Item item = Items[j];
			float num3 = Mathf.Clamp01(item.Until - unscaledTime);
			Color color = GUI.color;
			GUI.color = new Color(1f, 1f, 1f, num3);
			Rect rect = new Rect(num, num2 + (float)j * 34f, 460f, 30f);
			GUI.Box(rect, GUIContent.none, _bg);
			GUI.Label(new Rect(rect.x + 10f, rect.y, rect.width - 20f, rect.height), item.Text, _label);
			GUI.color = color;
		}
	}
}
