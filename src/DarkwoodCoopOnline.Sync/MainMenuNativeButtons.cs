using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class MainMenuNativeButtons
{
	private struct PendingLabel
	{
		public tk2dTextMesh textMesh;

		public string label;

		public int framesLeft;
	}

	private static bool _localizedTextWarned;

	private const float LabelPixelsPerMeter = 2.5f;

	private static readonly Color LabelIdleColor = new Color(0.42f, 0.42f, 0.42f, 1f);

	private static readonly Color LabelHighlightColor = Color.white;

	private static bool _built;

	private static Button _hostBtn;

	private static Button _joinBtn;

	private static bool _patched;

	private static readonly Dictionary<string, tk2dSpriteCollectionData> _labelCollections = new Dictionary<string, tk2dSpriteCollectionData>();

	private static readonly List<PendingLabel> _pendingLabels = new List<PendingLabel>();

	private static int _colliderGuardFrames;

	// HOST and JOIN exist and are on screen right now
	public static bool Visible
	{
		get
		{
			if (Created && ((Component)_hostBtn).gameObject.activeInHierarchy)
			{
				return ((Component)_joinBtn).gameObject.activeInHierarchy;
			}
			return false;
		}
	}

	public static bool Created
	{
		get
		{
			if ((Object)(object)_hostBtn != (Object)null)
			{
				return (Object)(object)_joinBtn != (Object)null;
			}
			return false;
		}
	}

	public static void Tick()
	{
		// the game rebuilt its menu and our buttons went with it: build them again
		if (_built && !Created && Core.mainMenu)
		{
			_built = false;
		}
		if (!_built && Core.mainMenu)
		{
			MainMenu instance = Singleton<MainMenu>.Instance;
			if (!((Object)(object)instance == (Object)null) && !((Object)(object)instance.Menu0 == (Object)null) && instance.Menu0.activeSelf && !((Object)(object)instance.optionsBtn == (Object)null) && !((Object)(object)instance.playBtn == (Object)null))
			{
				_built = true;
				Build(instance);
			}
		}
	}

	public static void DumpDiagnostics()
	{
		MainMenu instance = Singleton<MainMenu>.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance.Menu0 == (Object)null || !instance.Menu0.activeSelf)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons][Diag] menu principal (Menu0) não está ativo agora - abra o menu principal de verdade antes de clicar isto.");
			return;
		}
		GameObject optionsBtn = instance.optionsBtn;
		if ((Object)(object)optionsBtn == (Object)null)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons][Diag] mm.optionsBtn é null.");
			return;
		}
		Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag] === TEMPLATE (optionsBtn) ===");
		DumpGameObject(optionsBtn);
		if ((Object)(object)instance.playBtn != (Object)null)
		{
			Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag] === PLAY (comparação) ===");
			DumpGameObject(instance.playBtn);
		}
		if ((Object)(object)instance.quitBtn != (Object)null)
		{
			Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag] === QUIT (comparação) ===");
			DumpGameObject(instance.quitBtn);
		}
		Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag] === PROCURANDO botões com TEXTO de verdade (tk2dTextMesh) ===");
		int num = 0;
		Button[] array = Object.FindObjectsOfType<Button>(true);
		foreach (Button val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			tk2dTextMesh val2 = ((Component)val).GetComponent<tk2dTextMesh>() ?? ((Component)val).GetComponentInChildren<tk2dTextMesh>(true);
			if (!((Object)(object)val2 == (Object)null))
			{
				num++;
				Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag]   candidato='" + ((Object)((Component)val).gameObject).name + "' texto='" + val2.text + "' " + string.Format("ativo={0} pai='{1}' ", ((Component)val).gameObject.activeInHierarchy, ((Object)(object)((Component)val).transform.parent != (Object)null) ? ((Object)((Component)val).transform.parent).name : "(nenhum)") + $"mesmoGO={(Object)(object)((Component)val).GetComponent<tk2dTextMesh>() != (Object)null}");
				if (num >= 25)
				{
					Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag]   (parando em 25 candidatos)");
					break;
				}
			}
		}
		if (num == 0)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons][Diag]   NENHUM Button com tk2dTextMesh encontrado - todos os botões do jogo são baseados em sprite.");
		}
		Build(instance);
		Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag] fim do dump - clones ficam na cena, olhe a tela também.");
	}

	private static void DumpGameObject(GameObject go)
	{
		if ((Object)(object)go == (Object)null)
		{
			Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag]   (GameObject null)");
			return;
		}
		Transform transform = go.transform;
		Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag]   name='{((Object)go).name}' activeSelf={go.activeSelf} activeInHierarchy={go.activeInHierarchy} layer={go.layer}");
		Plugin.Log.LogInfo(string.Format("[MainMenuNativeButtons][Diag]   localPosition={0:F2} position(mundo)={1:F2} localScale={2:F2} lossyScale={3:F2} parent='{4}'", transform.localPosition, transform.position, transform.localScale, transform.lossyScale, ((Object)(object)transform.parent != (Object)null) ? ((Object)transform.parent).name : "(nenhum)"));
		tk2dBaseSprite component = go.GetComponent<tk2dBaseSprite>();
		if ((Object)(object)component != (Object)null)
		{
			Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag]   tk2dBaseSprite: color={component.color} enabled={((Behaviour)component).enabled} (renderer visível: verificar color.a e enabled)");
		}
		else
		{
			Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag]   (sem tk2dBaseSprite direto no GameObject - pode estar num filho)");
		}
		Renderer component2 = go.GetComponent<Renderer>();
		if ((Object)(object)component2 != (Object)null)
		{
			Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag]   Renderer: enabled={component2.enabled} sortingOrder={component2.sortingOrder} sortingLayerName='{component2.sortingLayerName}' bounds={component2.bounds}");
		}
		PositionMe component3 = go.GetComponent<PositionMe>();
		if ((Object)(object)component3 != (Object)null)
		{
			Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag]   PositionMe: offset={component3.offset:F2} screenAnchor={component3.screenAnchor} fitToScreen={component3.fitToScreen} fitToHeight={component3.fitToHeight} accountForSize={component3.accountForSize} flipZ={component3.flipZ} snapToInt={component3.snapToInt} IgnoreResolution={component3.IgnoreResolution}");
		}
		Component[] components = go.GetComponents<Component>();
		StringBuilder stringBuilder = new StringBuilder();
		Component[] array = components;
		foreach (Component val in array)
		{
			stringBuilder.Append(((Object)(object)val != (Object)null) ? ((object)val).GetType().Name : "(null)").Append(", ");
		}
		Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag]   componentes no GameObject raiz: {stringBuilder}");
		for (int j = 0; j < transform.childCount; j++)
		{
			Transform child = transform.GetChild(j);
			Component[] components2 = ((Component)child).GetComponents<Component>();
			StringBuilder stringBuilder2 = new StringBuilder();
			array = components2;
			foreach (Component val2 in array)
			{
				stringBuilder2.Append(((Object)(object)val2 != (Object)null) ? ((object)val2).GetType().Name : "(null)").Append(", ");
			}
			Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag]   filho[{j}]='{((Object)child).name}' activeSelf={((Component)child).gameObject.activeSelf} localPos={child.localPosition:F2} componentes: {stringBuilder2}");
		}
	}

	private static void Build(MainMenu mm)
	{
		GameObject optionsBtn = mm.optionsBtn;
		if ((Object)(object)optionsBtn == (Object)null || mm.controllerButtons == null)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] optionsBtn/controllerButtons não encontrado — não dá pra clonar o botão nativo, mod segue com o painel IMGUI antigo.");
			return;
		}
		Transform parent = optionsBtn.transform.parent;
		if ((Object)(object)parent != (Object)null)
		{
			for (int num = parent.childCount - 1; num >= 0; num--)
			{
				Transform child = parent.GetChild(num);
				if ((Object)(object)child != (Object)null && (((Object)child).name == "CoopHostButton" || ((Object)child).name == "CoopJoinButton"))
				{
					Object.Destroy((Object)(object)((Component)child).gameObject);
				}
			}
		}
		float num2 = 40f;
		if ((Object)(object)mm.quitBtn != (Object)null)
		{
			float num3 = Mathf.Abs(mm.quitBtn.transform.localPosition.y - optionsBtn.transform.localPosition.y);
			if (num3 > 1f)
			{
				num2 = num3;
			}
		}
		PositionMe component = optionsBtn.GetComponent<PositionMe>();
		PositionMe val = (((Object)(object)mm.playBtn != (Object)null) ? mm.playBtn.GetComponent<PositionMe>() : null);
		float num4 = 60f;
		if ((Object)(object)component != (Object)null && (Object)(object)val != (Object)null)
		{
			float num5 = Mathf.Abs(val.offset.y - component.offset.y);
			if (num5 > 1f)
			{
				num4 = num5;
			}
		}
		float num6 = (((Object)(object)val != (Object)null) ? val.offset.y : 0f);
		float num7 = (((Object)(object)component != (Object)null) ? component.offset.y : 0f);
		Button val2 = CloneButton(optionsBtn, "CoopHostButton", "HOST", new Vector2(0f, num6 + num4 * 2f - num7), TriggerHost);
		Button val3 = CloneButton(optionsBtn, "CoopJoinButton", "JOIN", new Vector2(0f, num6 + num4 - num7), TriggerJoin);
		_hostBtn = val2;
		_joinBtn = val3;
		_colliderGuardFrames = 120;
		Plugin.Log.LogInfo($"[MainMenuNativeButtons][Diag] spacing medido={num2:F2} === CLONE HOST ===");
		if ((Object)(object)val2 != (Object)null)
		{
			DumpGameObject(((Component)val2).gameObject);
		}
		Plugin.Log.LogInfo("[MainMenuNativeButtons][Diag] === CLONE JOIN ===");
		if ((Object)(object)val3 != (Object)null)
		{
			DumpGameObject(((Component)val3).gameObject);
		}
		RegisterInControllerNav(mm);
		Plugin.Log.LogInfo("[MainMenuNativeButtons] botões HOST/JOIN nativos criados e inseridos na navegação por controle.");
	}

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(MainMenu), "populateMainOptionsButtons", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(MainMenuNativeButtons), "ReinsertAfterPopulate", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void ReinsertAfterPopulate(MainMenu __instance)
	{
		if (!CoopMenuList.Active)
		{
			RegisterInControllerNav(__instance);
		}
	}

	public static void SetOwnButtonsVisible(bool visible, List<GameObject> hiddenTracker)
	{
		Transform val = (((Object)(object)_hostBtn != (Object)null) ? ((Component)_hostBtn).transform.parent : (((Object)(object)_joinBtn != (Object)null) ? ((Component)_joinBtn).transform.parent : null));
		if ((Object)(object)val == (Object)null)
		{
			return;
		}
		for (int i = 0; i < val.childCount; i++)
		{
			Transform child = val.GetChild(i);
			if (!((Object)(object)child == (Object)null) && (!(((Object)child).name != "CoopHostButton") || !(((Object)child).name != "CoopJoinButton")))
			{
				GameObject gameObject = ((Component)child).gameObject;
				if (visible)
				{
					gameObject.SetActive(true);
				}
				else if (gameObject.activeSelf)
				{
					gameObject.SetActive(false);
					hiddenTracker?.Add(gameObject);
				}
			}
		}
	}

	private static void RegisterInControllerNav(MainMenu mm)
	{
		if (!((Object)(object)mm == (Object)null) && mm.controllerButtons != null)
		{
			int index = 0;
			int num = 0;
			if ((Object)(object)_hostBtn != (Object)null && !mm.controllerButtons.Contains(_hostBtn))
			{
				mm.controllerButtons.Insert(index++, _hostBtn);
				num++;
			}
			if ((Object)(object)_joinBtn != (Object)null && !mm.controllerButtons.Contains(_joinBtn))
			{
				mm.controllerButtons.Insert(index, _joinBtn);
				num++;
			}
			if (num > 0)
			{
				mm.currentlySelectedControllerButton += num;
			}
		}
	}

	private static Button CloneButton(GameObject template, string goName, string label, Vector2 offsetDelta, Action onFire)
	{
		GameObject val = Object.Instantiate<GameObject>(template, template.transform.parent);
		((Object)val).name = goName;
		PositionMe component = val.GetComponent<PositionMe>();
		if ((Object)(object)component != (Object)null)
		{
			component.offset += offsetDelta;
		}
		Button component2 = val.GetComponent<Button>();
		if ((Object)(object)component2 == (Object)null)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] clone '" + goName + "' não tem componente Button — abortando esse botão.");
			Object.Destroy((Object)(object)val);
			return null;
		}
		ApplyCustomLabelSprite(val, label);
		component2.function = "";
		component2.OnFire = onFire;
		component2.localized = false;
		component2.doSpriteRolloverColor = true;
		component2.rolloverColor = LabelHighlightColor;
		tk2dSprite component3 = val.GetComponent<tk2dSprite>();
		if ((Object)(object)component3 != (Object)null)
		{
			((tk2dBaseSprite)component3).color = LabelIdleColor;
		}
		component2.baseColor = LabelIdleColor;
		LocalizedText component4 = val.GetComponent<LocalizedText>();
		if ((Object)(object)component4 != (Object)null)
		{
			((Behaviour)component4).enabled = false;
			Object.Destroy((Object)(object)component4);
			if (!_localizedTextWarned)
			{
				_localizedTextWarned = true;
				Plugin.Log.LogInfo("[MainMenuNativeButtons] LocalizedText removido dos clones (texto fixo HOST/JOIN, sem tradução automática).");
			}
		}
		if ((Object)(object)component2.textMesh != (Object)null)
		{
			component2.textMesh.text = label;
			component2.textMesh.Commit();
			_pendingLabels.Add(new PendingLabel
			{
				textMesh = component2.textMesh,
				label = label,
				framesLeft = 10
			});
		}
		return component2;
	}

	private static void ApplyCustomLabelSprite(GameObject clone, string label)
	{
		tk2dSprite component = clone.GetComponent<tk2dSprite>();
		if ((Object)(object)component == (Object)null)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] clone não tem tk2dSprite — rótulo '" + label + "' fica com a imagem do template.");
			return;
		}
		if (!_labelCollections.TryGetValue(label, out var value))
		{
			value = BuildLabelCollection(label);
			_labelCollections[label] = value;
		}
		if (!((Object)(object)value == (Object)null))
		{
			((tk2dBaseSprite)component).SetSprite(value, 0);
		}
	}

	private static tk2dSpriteCollectionData BuildLabelCollection(string label)
	{
		string text = "DarkwoodCoopOnline.assets." + label + ".png";
		byte[] array = null;
		try
		{
			using Stream stream = typeof(MainMenuNativeButtons).Assembly.GetManifestResourceStream(text);
			if (stream != null)
			{
				array = new byte[stream.Length];
				stream.Read(array, 0, array.Length);
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] falha lendo recurso embutido '" + text + "': " + ex.Message);
		}
		if (array == null || array.Length == 0)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] recurso '" + text + "' não encontrado na DLL — botão '" + label + "' vai ficar com a imagem do template (texto errado). Falta adicionar o PNG como EmbeddedResource no .csproj?");
			return null;
		}
		Texture2D val = new Texture2D(2, 2, (TextureFormat)5, false);
		if (!ImageConversion.LoadImage(val, array))
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] Texture2D.LoadImage falhou pro rótulo '" + label + "'.");
			return null;
		}
		((Texture)val).filterMode = (FilterMode)0;
		((Texture)val).wrapMode = (TextureWrapMode)1;
		((Object)val).name = "CoopLabel_" + label;
		tk2dSpriteCollectionData obj = tk2dSpriteCollectionData.CreateFromTexture((Texture)(object)val, tk2dSpriteCollectionSize.PixelsPerMeter(2.5f), new string[1] { label }, (Rect[])(object)new Rect[1]
		{
			new Rect(0f, 0f, (float)((Texture)val).width, (float)((Texture)val).height)
		}, (Vector2[])(object)new Vector2[1]
		{
			new Vector2((float)((Texture)val).width * 0.5f, (float)((Texture)val).height * 0.5f)
		});
		if ((Object)(object)obj == (Object)null)
		{
			Plugin.Log.LogWarning("[MainMenuNativeButtons] CreateFromTexture retornou null pro rótulo '" + label + "'.");
			return obj;
		}
		Plugin.Log.LogInfo($"[MainMenuNativeButtons] rótulo '{label}' carregado do PNG embutido ({((Texture)val).width}x{((Texture)val).height}).");
		return obj;
	}

	private static void EnsureColliders()
	{
		if (_colliderGuardFrames <= 0)
		{
			return;
		}
		_colliderGuardFrames--;
		Button[] array = (Button[])(object)new Button[2] { _hostBtn, _joinBtn };
		foreach (Button val in array)
		{
			if (!((Object)(object)val == (Object)null))
			{
				GameObject gameObject = ((Component)val).gameObject;
				BoxCollider val2 = gameObject.GetComponent<BoxCollider>();
				Renderer component = gameObject.GetComponent<Renderer>();
				if ((Object)(object)val2 == (Object)null)
				{
					val2 = gameObject.AddComponent<BoxCollider>();
					Plugin.Log.LogInfo("[MainMenuNativeButtons] BoxCollider recriado em '" + ((Object)gameObject).name + "' (o nativo tinha sumido - ver comentário).");
				}
				if ((Object)(object)component != (Object)null)
				{
					Bounds bounds = component.bounds;
					Vector3 size = bounds.size;
					BoxCollider obj = val2;
					Transform transform = gameObject.transform;
					bounds = component.bounds;
					obj.center = transform.InverseTransformPoint(bounds.center);
					val2.size = new Vector3(Mathf.Max(size.x, 1f), Mathf.Max(size.y, 20f), Mathf.Max(size.z, 1f));
				}
			}
		}
	}

	public static void TickPendingLabels()
	{
		EnsureColliders();
		for (int num = _pendingLabels.Count - 1; num >= 0; num--)
		{
			PendingLabel value = _pendingLabels[num];
			if ((Object)(object)value.textMesh == (Object)null || value.framesLeft <= 0)
			{
				_pendingLabels.RemoveAt(num);
			}
			else
			{
				if (value.textMesh.text != value.label)
				{
					value.textMesh.text = value.label;
					value.textMesh.Commit();
				}
				value.framesLeft--;
				_pendingLabels[num] = value;
			}
		}
	}

	private static void TriggerHost()
	{
		MainMenuHostJoinUI.TriggerHost();
	}

	private static void TriggerJoin()
	{
		MainMenuHostJoinUI.TriggerJoin();
	}

	public static void Reset()
	{
		_built = false;
		_hostBtn = null;
		_joinBtn = null;
		_colliderGuardFrames = 0;
		_pendingLabels.Clear();
	}
}
