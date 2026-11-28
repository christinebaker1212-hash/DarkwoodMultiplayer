using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx;
using UnityEngine;

namespace DarkwoodCoopOnline;

// Character-art export (default key F5, in a loaded world).
//
// The player is drawn with 2D Toolkit: a torso animator and a legs animator, each playing clips from an
// animation library (tk2dSpriteAnimation). The torso library changes with what you hold: every item names
// one (InvItem.aniLibrary, e.g. "PlayerAxeAnims"), and there are "PlayerNaked..." / "PlayerEpilogue..."
// variants (Player.switchAniLibrary). Every clip frame points at a sprite in a sprite collection, whose
// pixels sit on one or more atlas textures.
//
// This walks ALL of those libraries (not just what you hold now), and writes to <Darkwood>/CoopSkins/export:
//   atlases/<collection>__<n>.png  every atlas texture the player's frames use, exactly as the game has it
//   frames.json                    every frame: library, clip, frame number, collection, atlas, pixel rect
//   README.txt                     what the files are
// That folder is what the skin artist (or Claude) needs to paint skins that fit the game's frames.
//
// Collection/frame members aren't in the compile stubs (the original mod never used them), so they are
// read by reflection; the names were checked against the game's Assembly-CSharp.
public static class SkinExport
{
	private const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

	private static bool _running;

	public static string Root => Path.Combine(Paths.GameRootPath, "CoopSkins");

	public static string ExportDir => Path.Combine(Root, "export");

	public static void Tick()
	{
		KeyCode key = CoopConfig.SkinExportKey?.Value ?? KeyCode.None;
		if (key == KeyCode.None || !Input.GetKeyDown(key) || _running || CoopUI.IsOpen)
		{
			return;
		}
		if ((UnityEngine.Object)(object)Player.Instance == (UnityEngine.Object)null)
		{
			CoopNotice.Show("Load into a world first, then press " + key + " to export the character art.");
			return;
		}
		((MonoBehaviour)Plugin.Instance).StartCoroutine(Run());
	}

	private sealed class FrameRow
	{
		public string Library;
		public string Clip;
		public int Frame;
		public float Fps;
		public string Collection;
		public int SpriteId;
	}

	private sealed class CollectionInfo
	{
		public string Name;
		public object Data;
		public List<string> AtlasFiles = new List<string>();
		public List<int[]> AtlasSizes = new List<int[]>();
		public bool Premultiplied;
	}

	private static IEnumerator Run()
	{
		_running = true;
		CoopNotice.Show("Exporting the character art... (a few seconds)", 30f);
		yield return null;
		string error = null;
		string summary = null;
		IEnumerator work = null;
		try
		{
			work = Export(s => summary = s);
		}
		catch (Exception ex)
		{
			error = ex.ToString();
		}
		while (error == null && work != null)
		{
			bool more;
			try
			{
				more = work.MoveNext();
			}
			catch (Exception ex2)
			{
				error = ex2.ToString();
				break;
			}
			if (!more)
			{
				break;
			}
			yield return work.Current;
		}
		_running = false;
		if (error != null)
		{
			Plugin.Log.LogError("[SkinExport] failed: " + error);
			CoopNotice.Show("Character art export failed - see BepInEx/LogOutput.log", 10f);
			yield break;
		}
		Plugin.Log.LogInfo("[SkinExport] " + summary + " -> " + ExportDir);
		CoopNotice.Show("Character art exported: " + summary + ". Opening the CoopSkins/export folder.", 12f);
		try
		{
			Application.OpenURL("file:///" + ExportDir.Replace('\\', '/'));
		}
		catch
		{
		}
	}

	private static IEnumerator Export(Action<string> done)
	{
		string atlasDir = Path.Combine(ExportDir, "atlases");
		// clear the last export file by file: removing the folder itself fails on Windows while Explorer
		// has it open (which it will, we open it)
		Directory.CreateDirectory(atlasDir);
		foreach (string old in Directory.GetFiles(atlasDir, "*.png"))
		{
			File.Delete(old);
		}

		// --- which animation libraries the player can use
		List<string> names = LibraryNames();
		var libs = new List<UnityEngine.Object>();
		var seenLib = new HashSet<string>();
		var missing = new List<string>();
		tk2dSpriteAnimation[] inMemory = Resources.FindObjectsOfTypeAll<tk2dSpriteAnimation>();
		foreach (string n in names)
		{
			if (!seenLib.Add(n))
			{
				continue;
			}
			UnityEngine.Object lib = null;
			try
			{
				lib = Resources.Load(n, typeof(tk2dSpriteAnimation));
			}
			catch
			{
			}
			if (lib == (UnityEngine.Object)null)
			{
				// already in memory under that name (not every library has to sit in a Resources folder)
				foreach (tk2dSpriteAnimation l in inMemory)
				{
					if ((UnityEngine.Object)(object)l != (UnityEngine.Object)null && ((UnityEngine.Object)l).name == n)
					{
						lib = l;
						break;
					}
				}
			}
			if (lib != (UnityEngine.Object)null && !libs.Contains(lib))
			{
				libs.Add(lib);
			}
			else if (lib == (UnityEngine.Object)null)
			{
				missing.Add(n);
			}
		}
		if (missing.Count > 0)
		{
			Plugin.Log.LogInfo("[SkinExport] not found (normal for naked/epilogue variants that don't exist): " + string.Join(", ", missing.ToArray()));
		}
		// whatever is on the player right now, even if it isn't loadable by name
		AddLive(libs, seenLib, (Player.Instance.torsoAnimator != null) ? Player.Instance.torsoAnimator.Library : null);
		Transform legsT = ((Component)Player.Instance).transform.Find("PlayerLegs");
		tk2dSpriteAnimator legs = (legsT != null) ? ((Component)legsT).GetComponent<tk2dSpriteAnimator>() : null;
		AddLive(libs, seenLib, (legs != null) ? legs.Library : null);
		yield return null;

		// --- every frame of every clip -> (collection, sprite id)
		var rows = new List<FrameRow>();
		var collections = new Dictionary<int, CollectionInfo>();
		var collectionOrder = new List<CollectionInfo>();
		foreach (UnityEngine.Object lib in libs)
		{
			if (!(Get(lib, "clips") is Array clips))
			{
				continue;
			}
			foreach (object clip in clips)
			{
				if (clip == null || !(Get(clip, "frames") is Array frames))
				{
					continue;
				}
				string clipName = Get(clip, "name") as string;
				float fps = (Get(clip, "fps") is float f) ? f : 0f;
				for (int i = 0; i < frames.Length; i++)
				{
					object frame = frames.GetValue(i);
					UnityEngine.Object coll = (frame != null) ? (Get(frame, "spriteCollection") as UnityEngine.Object) : null;
					if (coll == (UnityEngine.Object)null)
					{
						continue;
					}
					coll = (Get(coll, "inst") as UnityEngine.Object) ?? coll;
					if (!collections.TryGetValue(coll.GetInstanceID(), out CollectionInfo info))
					{
						info = new CollectionInfo
						{
							Name = UniqueName(SafeName((Get(coll, "spriteCollectionName") as string) ?? coll.name), collectionOrder),
							Data = coll,
							Premultiplied = Get(coll, "premultipliedAlpha") is bool pm && pm
						};
						collections[coll.GetInstanceID()] = info;
						collectionOrder.Add(info);
					}
					rows.Add(new FrameRow
					{
						Library = lib.name,
						Clip = clipName,
						Frame = i,
						Fps = fps,
						Collection = info.Name,
						SpriteId = (Get(frame, "spriteId") is int id) ? id : -1
					});
				}
			}
		}
		yield return null;

		// --- atlas textures
		int written = 0;
		foreach (CollectionInfo info in collectionOrder)
		{
			List<Texture> textures = Textures(info.Data);
			for (int t = 0; t < textures.Count; t++)
			{
				Texture tex = textures[t];
				if (tex == (Texture)null)
				{
					info.AtlasFiles.Add(null);
					info.AtlasSizes.Add(new int[2]);
					continue;
				}
				string file = info.Name + "__" + t + ".png";
				File.WriteAllBytes(Path.Combine(atlasDir, file), ReadableCopy(tex).EncodeToPNG());
				info.AtlasFiles.Add("atlases/" + file);
				info.AtlasSizes.Add(new int[2] { tex.width, tex.height });
				written++;
				yield return null;
			}
		}

		// --- manifest
		File.WriteAllText(Path.Combine(ExportDir, "frames.json"), Manifest(libs, rows, collectionOrder));
		File.WriteAllText(Path.Combine(ExportDir, "README.txt"), Readme());
		try
		{
			Resources.UnloadUnusedAssets();
		}
		catch
		{
		}
		done($"{libs.Count} animation sets, {rows.Count} frames, {written} sheets");
	}

	private static void AddLive(List<UnityEngine.Object> libs, HashSet<string> seen, tk2dSpriteAnimation lib)
	{
		if ((UnityEngine.Object)(object)lib != (UnityEngine.Object)null && !libs.Contains(lib))
		{
			seen.Add(((UnityEngine.Object)lib).name);
			libs.Add(lib);
		}
	}

	// Every torso/legs library the player can switch to: the bare-handed one, one per item, and the
	// naked / epilogue variants Player.switchAniLibrary derives from them ("Player" + suffix).
	private static List<string> LibraryNames()
	{
		var baseNames = new List<string> { "PlayerNoneAnims" };
		var log = new StringBuilder();
		void AddItem(InvItem item)
		{
			if ((UnityEngine.Object)(object)item != (UnityEngine.Object)null && Get(item, "aniLibrary") is string lib && !string.IsNullOrEmpty(lib) && !baseNames.Contains(lib))
			{
				baseNames.Add(lib);
			}
		}

		// 1) the item database: both its serialized key list and the dictionary getItem() actually uses
		ItemsDatabase db = null;
		try
		{
			db = Singleton<ItemsDatabase>.Instance;
		}
		catch (Exception ex)
		{
			log.Append(" db:" + ex.GetType().Name);
		}
		if ((UnityEngine.Object)(object)db == (UnityEngine.Object)null)
		{
			log.Append(" db:none");
		}
		else
		{
			var keys = new List<string>();
			if (Get(db, "itemsDict") is IDictionary dict)
			{
				foreach (object k in dict.Keys)
				{
					if (k is string s && !keys.Contains(s)) keys.Add(s);
				}
			}
			if (Get(db, "itemsKeys") is IEnumerable list)
			{
				foreach (object k in list)
				{
					if (k is string s && !keys.Contains(s)) keys.Add(s);
				}
			}
			int ok = 0, failed = 0;
			foreach (string key in keys)
			{
				try
				{
					InvItem item = db.getItem(key, false);
					if ((UnityEngine.Object)(object)item != (UnityEngine.Object)null) ok++;
					AddItem(item);
				}
				catch
				{
					failed++;
				}
			}
			log.Append($" db:{keys.Count} keys, {ok} items, {failed} errors");
		}

		// 2) every item prefab/instance already in memory (the game loads them all at startup)
		int loaded = 0;
		try
		{
			foreach (InvItem item in Resources.FindObjectsOfTypeAll<InvItem>())
			{
				loaded++;
				AddItem(item);
			}
		}
		catch (Exception ex2)
		{
			log.Append(" loadedItems:" + ex2.GetType().Name);
		}
		log.Append($" loadedItems:{loaded}");

		// 3) any player animation library already in memory
		int libsInMemory = 0;
		try
		{
			foreach (tk2dSpriteAnimation lib in Resources.FindObjectsOfTypeAll<tk2dSpriteAnimation>())
			{
				string n = ((UnityEngine.Object)(object)lib != (UnityEngine.Object)null) ? ((UnityEngine.Object)lib).name : null;
				if (!string.IsNullOrEmpty(n) && n.StartsWith("Player", StringComparison.Ordinal) && !baseNames.Contains(n))
				{
					baseNames.Add(n);
					libsInMemory++;
				}
			}
		}
		catch (Exception ex3)
		{
			log.Append(" loadedLibs:" + ex3.GetType().Name);
		}
		log.Append($" loadedLibs:+{libsInMemory}");
		Plugin.Log.LogInfo($"[SkinExport] animation set names: {baseNames.Count} ({log.ToString().Trim()}): {string.Join(", ", baseNames.ToArray())}");
		var all = new List<string>(baseNames);
		foreach (string n in baseNames)
		{
			if (n.StartsWith("Player", StringComparison.Ordinal) && n.Length > 6)
			{
				all.Add("PlayerNaked" + n.Substring(6));
				all.Add("PlayerEpilogue" + n.Substring(6));
			}
		}
		all.Add("PlayerNakedNoneAnims");
		all.Add("PlayerEpilogueAnims");
		return all;
	}

	private static List<Texture> Textures(object coll)
	{
		// what is actually drawn first: the runtime material instances (collections that unpack their PNGs at
		// load time only have textures there), then the asset materials, then the raw texture list
		foreach (string member in new[] { "materialInsts", "materials" })
		{
			if (Get(coll, member) is Material[] mats && mats.Length > 0)
			{
				var list = new List<Texture>();
				foreach (Material m in mats)
				{
					list.Add((m != (Material)null) ? m.mainTexture : null);
				}
				if (!list.TrueForAll(t => t == (Texture)null))
				{
					return list;
				}
			}
		}
		if (Get(coll, "textures") is Texture[] texs && texs.Length > 0)
		{
			return new List<Texture>(texs);
		}
		var one = new List<Texture>();
		if (Get(coll, "material") is Material mat && mat != (Material)null)
		{
			one.Add(mat.mainTexture);
		}
		return one;
	}

	// GPU copy so it works on textures that aren't CPU-readable; same size, so pixels are copied 1:1.
	private static Texture2D ReadableCopy(Texture src)
	{
		RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
		RenderTexture prev = RenderTexture.active;
		FilterMode oldFilter = src.filterMode;
		src.filterMode = FilterMode.Point;
		Graphics.Blit(src, rt);
		src.filterMode = oldFilter;
		RenderTexture.active = rt;
		var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, mipChain: false);
		tex.ReadPixels(new Rect(0f, 0f, src.width, src.height), 0, 0);
		tex.Apply();
		RenderTexture.active = prev;
		RenderTexture.ReleaseTemporary(rt);
		return tex;
	}

	private static string Manifest(List<UnityEngine.Object> libs, List<FrameRow> rows, List<CollectionInfo> colls)
	{
		var sb = new StringBuilder();
		sb.Append("{\n  \"format\": 1,\n  \"game\": ").Append(Str(Application.version)).Append(",\n  \"modVersion\": ").Append(Str(Plugin.Version));
		sb.Append(",\n  \"note\": \"rect = x,y,w,h in atlas pixels, y counted from the TOP of the PNG. rotated = the sprite is stored turned 90 degrees in the atlas.\"");
		sb.Append(",\n  \"libraries\": [");
		for (int i = 0; i < libs.Count; i++)
		{
			sb.Append((i == 0) ? "" : ", ").Append(Str(libs[i].name));
		}
		sb.Append("],\n  \"collections\": {\n");
		for (int c = 0; c < colls.Count; c++)
		{
			CollectionInfo info = colls[c];
			sb.Append("    ").Append(Str(info.Name)).Append(": {\"premultipliedAlpha\": ").Append(info.Premultiplied ? "true" : "false").Append(", \"atlases\": [");
			for (int a = 0; a < info.AtlasFiles.Count; a++)
			{
				sb.Append((a == 0) ? "" : ", ").Append("{\"file\": ").Append(Str(info.AtlasFiles[a])).Append(", \"w\": ").Append(info.AtlasSizes[a][0]).Append(", \"h\": ").Append(info.AtlasSizes[a][1]).Append('}');
			}
			sb.Append("], \"sprites\": [\n");
			Array defs = Get(info.Data, "spriteDefinitions") as Array;
			bool first = true;
			for (int s = 0; defs != null && s < defs.Length; s++)
			{
				object def = defs.GetValue(s);
				if (def == null || string.IsNullOrEmpty(Get(def, "name") as string))
				{
					continue;
				}
				int matId = (Get(def, "materialId") is int mid) ? mid : 0;
				int[] size = (matId >= 0 && matId < info.AtlasSizes.Count) ? info.AtlasSizes[matId] : new int[2];
				int sw = size[0], sh = size[1];
				string rect = "null";
				if (Get(def, "uvs") is Vector2[] uvs && uvs.Length > 0 && sw > 0)
				{
					float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
					foreach (Vector2 uv in uvs)
					{
						minX = Mathf.Min(minX, uv.x); maxX = Mathf.Max(maxX, uv.x);
						minY = Mathf.Min(minY, uv.y); maxY = Mathf.Max(maxY, uv.y);
					}
					int x0 = Mathf.RoundToInt(minX * sw), x1 = Mathf.RoundToInt(maxX * sw);
					int y0 = Mathf.RoundToInt((1f - maxY) * sh), y1 = Mathf.RoundToInt((1f - minY) * sh);
					rect = $"[{x0}, {y0}, {x1 - x0}, {y1 - y0}]";
				}
				object flipped = Get(def, "flipped");
				bool rotated = flipped != null && Convert.ToInt32(flipped) != 0;
				sb.Append(first ? "" : ",\n").Append("      {\"id\": ").Append(s).Append(", \"name\": ").Append(Str(Get(def, "name") as string))
					.Append(", \"atlas\": ").Append(matId).Append(", \"rect\": ").Append(rect).Append(", \"rotated\": ").Append(rotated ? "true" : "false").Append('}');
				first = false;
			}
			sb.Append("\n    ]}").Append((c < colls.Count - 1) ? ",\n" : "\n");
		}
		sb.Append("  },\n  \"frames\": [\n");
		for (int r = 0; r < rows.Count; r++)
		{
			FrameRow row = rows[r];
			sb.Append("    {\"library\": ").Append(Str(row.Library)).Append(", \"clip\": ").Append(Str(row.Clip)).Append(", \"frame\": ").Append(row.Frame)
				.Append(", \"fps\": ").Append(row.Fps.ToString(System.Globalization.CultureInfo.InvariantCulture))
				.Append(", \"collection\": ").Append(Str(row.Collection)).Append(", \"sprite\": ").Append(row.SpriteId).Append('}')
				.Append((r < rows.Count - 1) ? ",\n" : "\n");
		}
		sb.Append("  ]\n}\n");
		return sb.ToString();
	}

	private static string Readme()
	{
		return "Darkwood co-op - exported character art\r\n" +
			"=======================================\r\n\r\n" +
			"atlases\\   The game's own sprite sheets for the player character (torso and legs, for every\r\n" +
			"           item you can hold, plus the naked/epilogue variants). Each PNG is a sheet of small\r\n" +
			"           frames packed together; frames.json says which rectangle is which frame.\r\n" +
			"frames.json  Every animation frame: animation set (library), clip, frame number, which sheet\r\n" +
			"             and where on it.\r\n\r\n" +
			"Send this whole 'export' folder (zip it: right-click > Send to > Compressed (zipped) folder)\r\n" +
			"to whoever is making the skins. Don't edit these files in place - they are overwritten every\r\n" +
			"time you export.\r\n";
	}

	private static object Get(object o, string member)
	{
		if (o == null)
		{
			return null;
		}
		for (Type t = o.GetType(); t != null; t = t.BaseType)
		{
			FieldInfo f = t.GetField(member, All | BindingFlags.DeclaredOnly);
			if (f != null)
			{
				return f.GetValue(f.IsStatic ? null : o);
			}
			PropertyInfo p = t.GetProperty(member, All | BindingFlags.DeclaredOnly);
			if (p != null && p.GetIndexParameters().Length == 0)
			{
				try
				{
					return p.GetValue(o, null);
				}
				catch
				{
					return null;
				}
			}
		}
		return null;
	}

	public static string SafeName(string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return "collection";
		}
		var sb = new StringBuilder();
		foreach (char ch in s)
		{
			sb.Append((char.IsLetterOrDigit(ch) || ch == '-' || ch == '_') ? ch : '_');
		}
		return sb.ToString();
	}

	private static string UniqueName(string name, List<CollectionInfo> existing)
	{
		string n = name;
		int k = 2;
		while (existing.Exists(e => e.Name == n))
		{
			n = name + "_" + k++;
		}
		return n;
	}

	private static string Str(string s)
	{
		if (s == null)
		{
			return "null";
		}
		var sb = new StringBuilder("\"");
		foreach (char ch in s)
		{
			switch (ch)
			{
			case '"': sb.Append("\\\""); break;
			case '\\': sb.Append("\\\\"); break;
			case '\n': sb.Append("\\n"); break;
			case '\r': sb.Append("\\r"); break;
			case '\t': sb.Append("\\t"); break;
			default:
				if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
				else sb.Append(ch);
				break;
			}
		}
		return sb.Append('"').ToString();
	}
}
