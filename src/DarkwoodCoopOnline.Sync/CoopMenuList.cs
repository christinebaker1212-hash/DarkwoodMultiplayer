using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class CoopMenuList
{
	public sealed class Entry
	{
		public string Label;

		public Action OnPick;

		public bool KeepOpen;
	}

	private const float StepY = 60f;

	private const float TitleExtraGap = 20f;

	private static readonly List<GameObject> _spawned = new List<GameObject>();

	private static readonly List<GameObject> _hiddenNatives = new List<GameObject>();

	private static Action _onCancel;

	private static GameObject _textButtonTemplate;

	public static bool Active { get; private set; }

	public static void Tick()
	{
		if (Active && Input.GetKeyDown((KeyCode)27))
		{
			Action onCancel = _onCancel;
			Hide();
			onCancel?.Invoke();
		}
	}

	public static void Show(string title, List<Entry> entries, Action onCancel)
	{
		Hide();
		MainMenu instance = Singleton<MainMenu>.Instance;
		if ((Object)(object)instance == (Object)null || entries == null || entries.Count == 0)
		{
			Plugin.Log.LogWarning("[CoopMenuList] sem MainMenu ou sem itens - nada a mostrar.");
			return;
		}
		GameObject val = ResolveTextButtonTemplate();
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogWarning("[CoopMenuList] nenhum botão de TEXTO encontrado no jogo pra usar de molde - lista não pode ser criada.");
			return;
		}
		PositionMe anchorSource = (((Object)(object)instance.optionsBtn != (Object)null) ? instance.optionsBtn.GetComponent<PositionMe>() : null);
		Transform parent = (((Object)(object)instance.optionsBtn != (Object)null) ? instance.optionsBtn.transform.parent : val.transform.parent);
		_onCancel = onCancel;
		Active = true;
		entries = new List<Entry>(entries);
		entries.Add(new Entry
		{
			Label = "Back",
			OnPick = delegate
			{
				onCancel?.Invoke();
			}
		});
		HideNative(instance.playBtn);
		HideNative(instance.optionsBtn);
		HideNative(instance.creditsBtn);
		HideNative(instance.quitBtn);
		MainMenuNativeButtons.SetOwnButtonsVisible(visible: false, _hiddenNatives);
		int count = entries.Count;
		float num = (float)(count - 1) * 0.5f * 60f;
		if (!string.IsNullOrEmpty(title))
		{
			GameObject val2 = SpawnLabel(val, parent, anchorSource, instance.optionsBtn, title, num + 30f + 20f, null, keepOpen: false);
			if ((Object)(object)val2 != (Object)null)
			{
				_spawned.Add(val2);
			}
		}
		List<Button> list = new List<Button>();
		for (int i = 0; i < count; i++)
		{
			Entry entry = entries[i];
			float offsetY = num - (float)i * 60f;
			GameObject val3 = SpawnLabel(val, parent, anchorSource, instance.optionsBtn, entry.Label, offsetY, entry.OnPick, entry.KeepOpen);
			if (!((Object)(object)val3 == (Object)null))
			{
				_spawned.Add(val3);
				Button component = val3.GetComponent<Button>();
				if ((Object)(object)component != (Object)null)
				{
					list.Add(component);
				}
			}
		}
		if (instance.controllerButtons != null && list.Count > 0)
		{
			instance.controllerButtons.Clear();
			instance.controllerButtons.AddRange(list);
			instance.currentlySelectedControllerButton = 0;
			TrySelectFirst(instance);
		}
		Plugin.Log.LogInfo($"[CoopMenuList] lista '{title}' aberta com {count} item(ns).");
	}

	public static void Hide()
	{
		if (!Active && _spawned.Count == 0)
		{
			return;
		}
		foreach (GameObject item in _spawned)
		{
			if ((Object)(object)item != (Object)null)
			{
				Object.Destroy((Object)(object)item);
			}
		}
		_spawned.Clear();
		foreach (GameObject hiddenNative in _hiddenNatives)
		{
			if ((Object)(object)hiddenNative != (Object)null)
			{
				hiddenNative.SetActive(true);
			}
		}
		_hiddenNatives.Clear();
		_onCancel = null;
		Active = false;
		MainMenu instance = Singleton<MainMenu>.Instance;
		if ((Object)(object)instance != (Object)null)
		{
			try
			{
				AccessTools.Method(typeof(MainMenu), "populateMainOptionsButtons", (Type[])null, (Type[])null)?.Invoke(instance, null);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[CoopMenuList] falha repopulando a navegação nativa: " + ex.Message);
			}
		}
	}

	private static void TrySelectFirst(MainMenu mm)
	{
		try
		{
			AccessTools.Method(typeof(MainMenu), "selectMenuOption", new Type[1] { typeof(int) }, (Type[])null)?.Invoke(mm, new object[1] { 0 });
		}
		catch
		{
		}
	}

	private static void HideNative(GameObject go)
	{
		if (!((Object)(object)go == (Object)null) && go.activeSelf)
		{
			go.SetActive(false);
			_hiddenNatives.Add(go);
		}
	}

	private static GameObject ResolveTextButtonTemplate()
	{
		if ((Object)(object)_textButtonTemplate != (Object)null)
		{
			return _textButtonTemplate;
		}
		Button val = null;
		Button[] array = Object.FindObjectsOfType<Button>(true);
		foreach (Button val2 in array)
		{
			if (!((Object)(object)val2 == (Object)null) && !((Object)(object)((Component)val2).GetComponent<tk2dTextMesh>() == (Object)null))
			{
				if ((Object)(object)val == (Object)null)
				{
					val = val2;
				}
				switch (((Object)((Component)val2).gameObject).name)
				{
				case "OkBtn":
				case "YesBtn":
				case "NoBtn":
				case "cancelBtn":
					break;
				default:
					continue;
				}
				_textButtonTemplate = ((Component)val2).gameObject;
				break;
			}
		}
		if ((Object)(object)_textButtonTemplate == (Object)null && (Object)(object)val != (Object)null)
		{
			_textButtonTemplate = ((Component)val).gameObject;
		}
		if ((Object)(object)_textButtonTemplate != (Object)null)
		{
			Plugin.Log.LogInfo("[CoopMenuList] molde de botão de texto = '" + ((Object)_textButtonTemplate).name + "'.");
		}
		return _textButtonTemplate;
	}

	private static GameObject SpawnLabel(GameObject template, Transform parent, PositionMe anchorSource, GameObject layoutSource, string text, float offsetY, Action onPick, bool keepOpen)
	{
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_004c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0078: Unknown result type (might be due to invalid IL or missing references)
		//IL_0137: Unknown result type (might be due to invalid IL or missing references)
		//IL_013c: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_0111: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Unknown result type (might be due to invalid IL or missing references)
		//IL_0237: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bc: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_01cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_01d6: Unknown result type (might be due to invalid IL or missing references)
		//IL_01db: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e7: Unknown result type (might be due to invalid IL or missing references)
		//IL_01f8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0209: Unknown result type (might be due to invalid IL or missing references)
		//IL_021a: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = Object.Instantiate<GameObject>(template, parent);
		((Object)val).name = "CoopListItem_" + text;
		val.SetActive(true);
		if ((Object)(object)layoutSource != (Object)null)
		{
			val.transform.localPosition = layoutSource.transform.localPosition + new Vector3(0f, 0f, offsetY);
			val.transform.localRotation = layoutSource.transform.localRotation;
			val.transform.localScale = layoutSource.transform.localScale;
		}
		LocalizedText component = val.GetComponent<LocalizedText>();
		if ((Object)(object)component != (Object)null)
		{
			((Behaviour)component).enabled = false;
			Object.Destroy((Object)(object)component);
		}
		PositionMe component2 = val.GetComponent<PositionMe>();
		if ((Object)(object)component2 != (Object)null)
		{
			if ((Object)(object)anchorSource != (Object)null)
			{
				component2.screenAnchor = anchorSource.screenAnchor;
				component2.fitToScreen = anchorSource.fitToScreen;
				component2.fitToHeight = anchorSource.fitToHeight;
				component2.accountForSize = anchorSource.accountForSize;
				component2.IgnoreResolution = anchorSource.IgnoreResolution;
				component2.offset = new Vector2(anchorSource.offset.x, anchorSource.offset.y + offsetY);
			}
			else
			{
				component2.offset = new Vector2(component2.offset.x, component2.offset.y + offsetY);
			}
		}
		tk2dTextMesh component3 = val.GetComponent<tk2dTextMesh>();
		if ((Object)(object)component3 != (Object)null)
		{
			component3.anchor = (TextAnchor)4;
			if (component3.maxChars < text.Length + 4)
			{
				component3.maxChars = text.Length + 8;
			}
			component3.text = text;
			component3.Commit();
		}
		if (onPick != null)
		{
			BoxCollider val2 = val.GetComponent<BoxCollider>() ?? val.AddComponent<BoxCollider>();
			Renderer component4 = val.GetComponent<Renderer>();
			if ((Object)(object)component4 != (Object)null)
			{
				Bounds bounds = component4.bounds;
				Vector3 size = bounds.size;
				Transform transform = val.transform;
				bounds = component4.bounds;
				val2.center = transform.InverseTransformPoint(bounds.center);
				val2.size = new Vector3(Mathf.Max(size.x, 40f), Mathf.Max(size.y, 20f), Mathf.Max(size.z, 24f));
			}
			else
			{
				val2.size = new Vector3(200f, 20f, 30f);
			}
			((Collider)val2).enabled = true;
		}
		Button component5 = val.GetComponent<Button>();
		if ((Object)(object)component5 != (Object)null)
		{
			component5.function = "";
			component5.localized = false;
			component5.OnFire = null;
			if (onPick == null)
			{
				component5.disabled = true;
				Collider component6 = val.GetComponent<Collider>();
				if ((Object)(object)component6 != (Object)null)
				{
					component6.enabled = false;
				}
			}
			else
			{
				Action captured = onPick;
				component5.OnFire = delegate
				{
					if (keepOpen)
					{
						captured();
					}
					else
					{
						Hide();
						captured();
					}
				};
			}
		}
		return val;
	}

	public static void Reset()
	{
		Hide();
		_textButtonTemplate = null;
	}
}
