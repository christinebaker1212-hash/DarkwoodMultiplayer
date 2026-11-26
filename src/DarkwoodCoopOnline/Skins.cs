using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline;

// Per-player character skins.
//
// A skin is a folder in <Darkwood>/CoopSkins (e.g. CoopSkins/bunny) holding repainted copies of the sheets
// that F5 exports, with the same file names: atlases/PlayerNoneSprites__0.png and so on (the PNGs may also
// sit directly in the skin folder). Sheets a skin doesn't have keep the game's own art. A sheet may be a
// whole multiple of the original size (e.g. 2048x2048 for a 1024x1024 sheet) for more detail: sprites are
// mapped by relative position, so the frames still line up.
//
// Every player picks one ([Skins] MySkin, or F6 in game to cycle through the folders) and the choice is sent
// to the others, who draw that player's character with the same skin - if they have the same folder.
//
// The game's player and all co-op puppets share ONE material per sprite sheet, so changing the material's
// texture would put the same skin on everybody. Instead each sprite's renderer gets a MaterialPropertyBlock
// that overrides _MainTex for that renderer only, picked from the sheet the current frame comes from.
public static class SkinLibrary
{
	private static readonly Dictionary<string, Dictionary<string, Texture2D>> Loaded = new Dictionary<string, Dictionary<string, Texture2D>>(StringComparer.OrdinalIgnoreCase);

	private static readonly HashSet<string> Styled = new HashSet<string>();

	private static readonly HashSet<string> Warned = new HashSet<string>();

	public static string Root => SkinExport.Root;

	// Skin folders available on this computer (not the export folder)
	public static List<string> Available()
	{
		var list = new List<string>();
		try
		{
			if (Directory.Exists(Root))
			{
				foreach (string dir in Directory.GetDirectories(Root))
				{
					string name = Path.GetFileName(dir);
					if (!string.Equals(name, "export", StringComparison.OrdinalIgnoreCase) && HasSheets(dir))
					{
						list.Add(name);
					}
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[Skins] couldn't list " + Root + ": " + ex.Message);
		}
		list.Sort(StringComparer.OrdinalIgnoreCase);
		return list;
	}

	public static bool Has(string skin)
	{
		return !string.IsNullOrEmpty(skin) && Sheets(skin).Count > 0;
	}

	private static bool HasSheets(string dir)
	{
		string atlases = Path.Combine(dir, "atlases");
		return Directory.GetFiles(dir, "*.png").Length > 0 || (Directory.Exists(atlases) && Directory.GetFiles(atlases, "*.png").Length > 0);
	}

	// sheet name ("PlayerNoneSprites__0") -> texture, loaded once per skin
	public static Dictionary<string, Texture2D> Sheets(string skin)
	{
		if (Loaded.TryGetValue(skin, out Dictionary<string, Texture2D> sheets))
		{
			return sheets;
		}
		sheets = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
		Loaded[skin] = sheets;
		string dir = Path.Combine(Root, skin);
		if (skin.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || skin.Contains("..") || !Directory.Exists(dir))
		{
			return sheets;
		}
		var files = new List<string>(Directory.GetFiles(dir, "*.png"));
		string atlases = Path.Combine(dir, "atlases");
		if (Directory.Exists(atlases))
		{
			files.AddRange(Directory.GetFiles(atlases, "*.png"));
		}
		foreach (string file in files)
		{
			try
			{
				var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
				if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(file), markNonReadable: true))
				{
					Plugin.Log.LogWarning("[Skins] not a PNG: " + file);
					continue;
				}
				tex.name = "CoopSkin:" + skin + "/" + Path.GetFileNameWithoutExtension(file);
				sheets[Path.GetFileNameWithoutExtension(file)] = tex;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[Skins] couldn't load " + file + ": " + ex.Message);
			}
		}
		Plugin.Log.LogInfo($"[Skins] loaded skin '{skin}': {sheets.Count} sheet(s)");
		return sheets;
	}

	// The skin's version of a sheet, styled like the game's own (filtering, wrapping) the first time it's used.
	public static Texture2D Sheet(string skin, string sheet, Texture original)
	{
		if (!Sheets(skin).TryGetValue(sheet, out Texture2D tex) || (Object)(object)tex == (Object)null)
		{
			return null;
		}
		if ((Object)(object)original != (Object)null && Styled.Add(tex.name))
		{
			tex.filterMode = original.filterMode;
			tex.wrapMode = original.wrapMode;
			tex.anisoLevel = original.anisoLevel;
			if (tex.width * original.height != tex.height * original.width || tex.width < original.width)
			{
				string key = tex.name + " size";
				if (Warned.Add(key))
				{
					Plugin.Log.LogWarning($"[Skins] {tex.name} is {tex.width}x{tex.height} but the game's sheet is {original.width}x{original.height} - frames will be misplaced. Paint over the exported sheet (same size, or exactly 2x/3x/4x).");
				}
			}
		}
		return tex;
	}

	public static void Forget()
	{
		foreach (Dictionary<string, Texture2D> sheets in Loaded.Values)
		{
			foreach (Texture2D tex in sheets.Values)
			{
				if ((Object)(object)tex != (Object)null)
				{
					Object.Destroy((Object)(object)tex);
				}
			}
		}
		Loaded.Clear();
		Styled.Clear();
	}

	internal static bool WarnOnce(string key)
	{
		return Warned.Add(key);
	}
}

// Sits on one tk2d sprite (the player's torso or legs, or a co-op puppet's) and swaps in its owner's skin.
public sealed class SkinRenderer : MonoBehaviour
{
	private const BindingFlags Inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static readonly int MainTex = Shader.PropertyToID("_MainTex");

	private static FieldInfo _fCollection;

	private static FieldInfo _fCollectionInst;

	private static FieldInfo _fSpriteId;

	private static FieldInfo _fDefs;

	private static FieldInfo _fCollName;

	private static FieldInfo _fMatId;

	private static bool _reflectionFailed;

	// collection instance id -> (sheet name prefix, sprite definitions)
	private static readonly Dictionary<int, KeyValuePair<string, Array>> Collections = new Dictionary<int, KeyValuePair<string, Array>>();

	private static readonly Dictionary<long, string> SheetNames = new Dictionary<long, string>();

	private tk2dBaseSprite _sprite;

	private Renderer _rend;

	private MaterialPropertyBlock _block;

	private Texture _applied;

	private bool _dirty = true;

	private RemotePlayerPuppet _puppet;

	private float _ownerCheckAt = -1f;

	private void LateUpdate()
	{
		if ((Object)(object)_sprite == (Object)null)
		{
			_sprite = ((Component)this).GetComponent<tk2dBaseSprite>();
			_rend = ((Component)this).GetComponent<Renderer>();
			if ((Object)(object)_sprite == (Object)null || (Object)(object)_rend == (Object)null || !Reflect())
			{
				((Behaviour)this).enabled = false;
				return;
			}
		}
		if (Time.unscaledTime >= _ownerCheckAt)
		{
			_ownerCheckAt = Time.unscaledTime + 1f;
			_puppet = ((Component)this).GetComponentInParent<RemotePlayerPuppet>();
		}
		string skin = ((Object)(object)_puppet != (Object)null) ? SkinSync.SkinOf(_puppet.Slot) : SkinSync.MySkin;
		Texture want = null;
		if (!string.IsNullOrEmpty(skin))
		{
			string sheet = CurrentSheet();
			if (sheet != null)
			{
				Material mat = _rend.sharedMaterial;
				want = SkinLibrary.Sheet(skin, sheet, ((Object)(object)mat != (Object)null) ? mat.mainTexture : null);
			}
		}
		if ((Object)(object)want == (Object)(object)_applied && !_dirty)
		{
			return;
		}
		_dirty = false;
		if (_block == null)
		{
			_block = new MaterialPropertyBlock();
		}
		_block.Clear();
		if ((Object)(object)want != (Object)null)
		{
			_block.SetTexture(MainTex, want);
		}
		_rend.SetPropertyBlock(_block);
		_applied = want;
	}

	private void OnDisable()
	{
		// set it again when we're back
		_dirty = true;
	}

	// "PlayerNoneSprites__0": the exported sheet the current frame is drawn from
	private string CurrentSheet()
	{
		try
		{
			object coll = _fCollectionInst.GetValue(_sprite) ?? _fCollection.GetValue(_sprite);
			if (!(coll is Object unityColl) || unityColl == (Object)null)
			{
				return null;
			}
			int id = unityColl.GetInstanceID();
			if (!Collections.TryGetValue(id, out KeyValuePair<string, Array> info))
			{
				string name = _fCollName.GetValue(coll) as string;
				info = new KeyValuePair<string, Array>(SkinExport.SafeName(string.IsNullOrEmpty(name) ? unityColl.name : name), _fDefs.GetValue(coll) as Array);
				Collections[id] = info;
			}
			int spriteId = (int)_fSpriteId.GetValue(_sprite);
			int matId = 0;
			if (info.Value != null && spriteId >= 0 && spriteId < info.Value.Length)
			{
				object def = info.Value.GetValue(spriteId);
				if (def != null)
				{
					_fMatId ??= def.GetType().GetField("materialId", Inst);
					if (_fMatId != null)
					{
						matId = (int)_fMatId.GetValue(def);
					}
				}
			}
			long key = ((long)id << 8) | (uint)(matId & 0xFF);
			if (!SheetNames.TryGetValue(key, out string sheet))
			{
				sheet = info.Key + "__" + matId;
				SheetNames[key] = sheet;
			}
			return sheet;
		}
		catch (Exception ex)
		{
			if (SkinLibrary.WarnOnce("sheet-error"))
			{
				Plugin.Log.LogWarning("[Skins] couldn't read the current sprite: " + ex.Message);
			}
			return null;
		}
	}

	private static bool Reflect()
	{
		if (_fSpriteId != null)
		{
			return true;
		}
		if (_reflectionFailed)
		{
			return false;
		}
		Type sprite = typeof(tk2dBaseSprite);
		_fCollection = sprite.GetField("collection", Inst);
		_fCollectionInst = sprite.GetField("collectionInst", Inst);
		_fSpriteId = sprite.GetField("_spriteId", Inst);
		Type coll = _fCollection?.FieldType;
		_fDefs = coll?.GetField("spriteDefinitions", Inst);
		_fCollName = coll?.GetField("spriteCollectionName", Inst);
		if (_fCollection == null || _fCollectionInst == null || _fSpriteId == null || _fDefs == null || _fCollName == null)
		{
			_reflectionFailed = true;
			_fSpriteId = null;
			Plugin.Log.LogError("[Skins] this game version's sprite code looks different - skins are off");
			return false;
		}
		return true;
	}
}

// Which skin each player wears, sent to everyone.
public static class SkinSync
{
	private static readonly Dictionary<int, string> Remote = new Dictionary<int, string>();

	private static string _lastSent;

	private static float _nextSend;

	private static float _nextAttach;

	public static string MySkin => CoopConfig.MySkin?.Value?.Trim() ?? "";

	public static string SkinOf(int slot)
	{
		return Remote.TryGetValue(slot, out string s) ? s : "";
	}

	// every frame, from Plugin.Update
	public static void Tick()
	{
		KeyCode key = CoopConfig.SkinCycleKey?.Value ?? KeyCode.None;
		if (key != KeyCode.None && Input.GetKeyDown(key) && (Object)(object)Player.Instance != (Object)null && !CoopUI.IsOpen)
		{
			Cycle();
		}
		if (Time.unscaledTime < _nextAttach)
		{
			return;
		}
		_nextAttach = Time.unscaledTime + 0.5f;
		Player p = Player.Instance;
		if ((Object)(object)p == (Object)null)
		{
			return;
		}
		if ((Object)(object)p.torsoAnimator != (Object)null)
		{
			Attach(((Component)p.torsoAnimator).gameObject);
		}
		Transform legs = ((Component)p).transform.Find("PlayerLegs");
		if ((Object)(object)legs != (Object)null)
		{
			Attach(((Component)legs).gameObject);
		}
		foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
		{
			if ((Object)(object)puppet != (Object)null)
			{
				Attach(puppet.TorsoObject);
				Attach(puppet.LegsObject);
			}
		}
	}

	private static void Attach(GameObject go)
	{
		if ((Object)(object)go != (Object)null && (Object)(object)go.GetComponent<SkinRenderer>() == (Object)null)
		{
			go.AddComponent<SkinRenderer>();
		}
	}

	private static void Cycle()
	{
		SkinLibrary.Forget();
		List<string> skins = SkinLibrary.Available();
		skins.Insert(0, "");
		int i = skins.FindIndex(s => string.Equals(s, MySkin, StringComparison.OrdinalIgnoreCase));
		string next = skins[(i + 1) % skins.Count];
		CoopConfig.MySkin.Value = next;
		if (skins.Count == 1)
		{
			CoopNotice.Show("No skins yet: put skin folders in " + SkinLibrary.Root);
		}
		else
		{
			CoopNotice.Show("Skin: " + (next == "" ? "the game's own look" : next));
		}
		_nextSend = 0f;
	}

	// from the co-op session's send loop: my skin when it changes, and again every few seconds for anyone
	// who joined (or whose puppet of me was rebuilt) since
	public static byte[] Poll()
	{
		string mine = MySkin;
		if (mine == _lastSent && Time.unscaledTime < _nextSend)
		{
			return null;
		}
		_lastSent = mine;
		_nextSend = Time.unscaledTime + 4f;
		return PacketWriter.Begin(PacketType.PlayerSkin).Str(mine).Finish();
	}

	public static void Apply(PacketReader r, int from, string peerName)
	{
		string skin = (r.Str() ?? "").Trim();
		if (skin.Length > 64)
		{
			skin = skin.Substring(0, 64);
		}
		if (Remote.TryGetValue(from, out string old) && old == skin)
		{
			return;
		}
		Remote[from] = skin;
		Plugin.Log.LogInfo($"[Skins] '{peerName}' wears '{(skin == "" ? "(none)" : skin)}'");
		if (skin != "" && !SkinLibrary.Has(skin) && SkinLibrary.WarnOnce("missing:" + skin))
		{
			CoopNotice.Show($"{peerName} wears the skin '{skin}', which you don't have in CoopSkins - they look normal to you.", 10f);
		}
	}

	public static void Forget(int slot)
	{
		Remote.Remove(slot);
	}

	public static void Reset()
	{
		Remote.Clear();
		_lastSent = null;
		_nextSend = 0f;
	}
}
