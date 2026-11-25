using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class SaveSlotPickerUI
{
	private static WorldSync.SlotSummary[] _slots;

	private static Action<int> _onChosen;

	private static int _confirmingSlot = -1;

	private static GUIStyle _boxStyle;

	private static GUIStyle _titleStyle;

	private static GUIStyle _hintStyle;

	public static bool Active => _slots != null;

	public static void Show(WorldSync.SlotSummary[] slots, Action<int> onChosen)
	{
		_slots = slots;
		_onChosen = onChosen;
		_confirmingSlot = -1;
		if (!Core.mainMenu || !((Object)(object)Singleton<MainMenu>.Instance != (Object)null))
		{
			return;
		}
		List<CoopMenuList.Entry> list = new List<CoopMenuList.Entry>();
		WorldSync.SlotSummary[] array = slots;
		for (int i = 0; i < array.Length; i++)
		{
			WorldSync.SlotSummary slotSummary = array[i];
			int id = slotSummary.id;
			bool occupied = slotSummary.occupied;
			string label = (occupied ? $"Slot {id} - {slotSummary.label}" : $"Slot {id} - Empty");
			list.Add(new CoopMenuList.Entry
			{
				Label = label,
				OnPick = delegate
				{
					if (!occupied)
					{
						Choose(id);
					}
					else
					{
						List<CoopMenuList.Entry> entries = new List<CoopMenuList.Entry>
						{
							new CoopMenuList.Entry
							{
								Label = "Yes, overwrite",
								OnPick = delegate
								{
									Choose(id);
								}
							},
							new CoopMenuList.Entry
							{
								Label = "Cancel",
								OnPick = delegate
								{
									Show(slots, onChosen);
								}
							}
						};
						CoopMenuList.Show($"Overwrite slot {id}?", entries, null);
					}
				}
			});
		}
		_slots = null;
		CoopMenuList.Show("Save this world to...", list, null);
	}

	public static void Draw()
	{
		if (_slots == null)
		{
			return;
		}
		EnsureStyles();
		float num = 320f;
		float num2 = 60f + (float)_slots.Length * 30f + ((_confirmingSlot >= 0) ? 70f : 0f);
		float num3 = ((float)Screen.width - num) * 0.5f;
		float num4 = ((float)Screen.height - num2) * 0.5f;
		GUI.Box(new Rect(num3, num4, num, num2), GUIContent.none, _boxStyle);
		GUI.Label(new Rect(num3, num4 + 6f, num, 24f), "Where should this host's world be saved?", _titleStyle);
		float num5 = num4 + 34f;
		WorldSync.SlotSummary[] slots = _slots;
		for (int i = 0; i < slots.Length; i++)
		{
			WorldSync.SlotSummary slotSummary = slots[i];
			string text = (slotSummary.occupied ? $"Slot {slotSummary.id} - {slotSummary.label}" : $"Slot {slotSummary.id} - Empty");
			if (GUI.Button(new Rect(num3 + 12f, num5, num - 24f, 26f), text))
			{
				if (slotSummary.occupied)
				{
					_confirmingSlot = slotSummary.id;
				}
				else
				{
					Choose(slotSummary.id);
				}
			}
			num5 += 30f;
		}
		if (_confirmingSlot >= 0)
		{
			WorldSync.SlotSummary slotSummary2 = Array.Find(_slots, (WorldSync.SlotSummary s) => s.id == _confirmingSlot);
			GUI.Label(new Rect(num3 + 12f, num5 + 4f, num - 24f, 34f), $"Are you sure you want to overwrite '{slotSummary2.label}' in slot {_confirmingSlot}?", _hintStyle);
			if (GUI.Button(new Rect(num3 + 12f, num5 + 40f, (num - 32f) / 2f, 26f), "Yes, overwrite"))
			{
				Choose(_confirmingSlot);
			}
			if (GUI.Button(new Rect(num3 + 20f + (num - 32f) / 2f, num5 + 40f, (num - 32f) / 2f, 26f), "Cancel"))
			{
				_confirmingSlot = -1;
			}
		}
	}

	private static void Choose(int slotId)
	{
		Action<int> onChosen = _onChosen;
		_slots = null;
		_onChosen = null;
		_confirmingSlot = -1;
		onChosen?.Invoke(slotId);
	}

	private static void EnsureStyles()
	{
		if (_boxStyle == null)
		{
			Texture2D val = new Texture2D(1, 1);
			val.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.05f, 0.92f));
			val.Apply();
			GUIStyle val2 = new GUIStyle();
			val2.normal.background = val;
			_boxStyle = val2;
			_titleStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 15,
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			_titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.85f);
			_hintStyle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				alignment = (TextAnchor)4,
				wordWrap = true
			};
			_hintStyle.normal.textColor = new Color(1f, 0.7f, 0.5f);
		}
	}

	public static void Reset()
	{
		_slots = null;
		_onChosen = null;
		_confirmingSlot = -1;
	}
}
