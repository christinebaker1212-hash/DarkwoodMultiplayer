using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class WorldSync
{
	private sealed class SaveFile
	{
		public string Name;

		public byte[] Data;
	}

	public readonly struct SlotSummary
	{
		public readonly int id;

		public readonly bool occupied;

		public readonly string label;

		public SlotSummary(int id, bool occupied, string label)
		{
			this.id = id;
			this.occupied = occupied;
			this.label = label;
		}
	}

	[CompilerGenerated]
	private sealed class _003CBuildSnapshotChunks_003Ed__1 : IEnumerable<byte[]>, IEnumerable, IEnumerator<byte[]>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private byte[] _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private byte[] _003Cblob_003E5__2;

		private int _003Ctotal_003E5__3;

		private int _003Ci_003E5__4;

		byte[] IEnumerator<byte[]>.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		object IEnumerator.Current
		{
			[DebuggerHidden]
			get
			{
				return _003C_003E2__current;
			}
		}

		[DebuggerHidden]
		public _003CBuildSnapshotChunks_003Ed__1(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003Cblob_003E5__2 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			switch (_003C_003E1__state)
			{
			default:
				return false;
			case 0:
				_003C_003E1__state = -1;
				_003Cblob_003E5__2 = SerializeWorld();
				_003Ctotal_003E5__3 = (_003Cblob_003E5__2.Length + 60000 - 1) / 60000;
				Plugin.Log.LogInfo($"[World] snapshot {_003Cblob_003E5__2.Length} bytes → {_003Ctotal_003E5__3} chunks");
				if (_003Ctotal_003E5__3 == 0)
				{
					_003Ctotal_003E5__3 = 1;
				}
				_003Ci_003E5__4 = 0;
				break;
			case 1:
				_003C_003E1__state = -1;
				_003Ci_003E5__4++;
				break;
			}
			if (_003Ci_003E5__4 < _003Ctotal_003E5__3)
			{
				int num = _003Ci_003E5__4 * 60000;
				int num2 = Math.Min(60000, _003Cblob_003E5__2.Length - num);
				byte[] array = new byte[Math.Max(num2, 0)];
				if (num2 > 0)
				{
					Array.Copy(_003Cblob_003E5__2, num, array, 0, num2);
				}
				_003C_003E2__current = PacketWriter.Begin(PacketType.SnapshotChunk).UShort((ushort)_003Ci_003E5__4).UShort((ushort)_003Ctotal_003E5__3)
					.Bytes(array)
					.Finish();
				_003C_003E1__state = 1;
				return true;
			}
			return false;
		}

		bool IEnumerator.MoveNext()
		{
			//ILSpy generated this explicit interface implementation from .override directive in MoveNext
			return this.MoveNext();
		}

		[DebuggerHidden]
		void IEnumerator.Reset()
		{
			throw new NotSupportedException();
		}

		[DebuggerHidden]
		IEnumerator<byte[]> IEnumerable<byte[]>.GetEnumerator()
		{
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				return this;
			}
			return new _003CBuildSnapshotChunks_003Ed__1(0);
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<byte[]>)this).GetEnumerator();
		}
	}

	private const int ChunkSize = 60000;

	private static byte[][] _chunks;

	private static int _received;

	private const string NewGameFlagFile = "coop_new_game.flag";

	private static bool _worldIdPatched;

	private const int MaxProfileSlots = 5;

	private const string HostSlotsFile = "coop_host_slots.json";

	private const string SlotOwnerFile = "coop_owner.txt";

	private const string WorldIdFile = "coop_world_id.txt";

	private const string HostWorldIdsFile = "coop_host_worldids.json";

	[IteratorStateMachine(typeof(_003CBuildSnapshotChunks_003Ed__1))]
	public static IEnumerable<byte[]> BuildSnapshotChunks()
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CBuildSnapshotChunks_003Ed__1(-2);
	}

	public static bool ReceiveChunk(PacketReader r, out byte[] fullBlob)
	{
		fullBlob = null;
		int num = r.UShort();
		int num2 = r.UShort();
		byte[] array = r.Bytes();
		if (_chunks == null || _chunks.Length != num2)
		{
			_chunks = new byte[num2][];
			_received = 0;
		}
		if (_chunks[num] == null)
		{
			_chunks[num] = array;
			_received++;
		}
		if (_received < num2)
		{
			return false;
		}
		using (MemoryStream memoryStream = new MemoryStream())
		{
			byte[][] chunks = _chunks;
			foreach (byte[] array2 in chunks)
			{
				memoryStream.Write(array2, 0, array2.Length);
			}
			fullBlob = memoryStream.ToArray();
		}
		_chunks = null;
		_received = 0;
		Plugin.Log.LogInfo($"[World] snapshot completo: {fullBlob.Length} bytes");
		return true;
	}

	private static string EnsureWorldId(out bool freshlyGenerated)
	{
		freshlyGenerated = false;
		try
		{
			GameProfile currentProfile = Core.currentProfile;
			if (currentProfile == null)
			{
				return null;
			}
			string text = Path.Combine(Path.Combine(Application.persistentDataPath, "1_4Save"), "prof" + currentProfile.id);
			string path = Path.Combine(text, "coop_world_id.txt");
			if (File.Exists(path))
			{
				return File.ReadAllText(path).Trim();
			}
			string path2 = Path.Combine(text, "coop_new_game.flag");
			bool flag = File.Exists(path2);
			string text2 = Guid.NewGuid().ToString("N");
			Directory.CreateDirectory(text);
			File.WriteAllText(path, text2);
			if (flag)
			{
				try
				{
					File.Delete(path2);
				}
				catch
				{
				}
				freshlyGenerated = true;
				Plugin.Log.LogInfo($"[World][WorldId] New Game confirmado no perfil {currentProfile.id} — ID gerado '{text2}', personagens salvos serão zerados.");
			}
			else
			{
				Plugin.Log.LogInfo($"[World][WorldId] perfil {currentProfile.id} sem ID do mod ainda (save de antes desta versão) — adotado com '{text2}', sem mexer em personagens salvos.");
			}
			return text2;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[World][WorldId] falha garantindo coop_world_id.txt: " + ex.Message);
			return null;
		}
	}

	private static void DeleteWorldIdIfInactive(int profileSlotId)
	{
		try
		{
			string text = Path.Combine(Path.Combine(Application.persistentDataPath, "1_4Save"), "prof" + profileSlotId);
			string path = Path.Combine(text, "coop_world_id.txt");
			if (File.Exists(path))
			{
				File.Delete(path);
			}
			Directory.CreateDirectory(text);
			File.WriteAllText(Path.Combine(text, "coop_new_game.flag"), "");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning($"[World][WorldId] falha marcando slot {profileSlotId} como New Game: {ex.Message}");
		}
	}

	public static void EnsurePatched(string modId)
	{
		if (!_worldIdPatched)
		{
			_worldIdPatched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(MainMenu), "displayProfile", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(WorldSync), "AfterDisplayProfile", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterDisplayProfile(int id)
	{
		GameProfile currentProfile = Core.currentProfile;
		if (currentProfile != null && !currentProfile.Active)
		{
			DeleteWorldIdIfInactive(id);
		}
	}

	public static string PeekWorldId(byte[] blob)
	{
		try
		{
			blob = Decompress(blob);
			using MemoryStream input = new MemoryStream(blob);
			using BinaryReader binaryReader = new BinaryReader(input);
			int num = binaryReader.ReadUInt16();
			for (int i = 0; i < num; i++)
			{
				string text = binaryReader.ReadString();
				int count = binaryReader.ReadInt32();
				byte[] bytes = binaryReader.ReadBytes(count);
				if (text == "coop_world_id.txt")
				{
					return Encoding.UTF8.GetString(bytes).Trim();
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[World][WorldId] falha lendo worldId do blob recebido: " + ex.Message);
		}
		return null;
	}

	public static string GetMappedWorldIdForHost(string hostIdentity)
	{
		Dictionary<string, string> dictionary = ReadHostWorldIds(Path.Combine(Application.persistentDataPath, "1_4Save"));
		if (string.IsNullOrEmpty(hostIdentity) || !dictionary.TryGetValue(hostIdentity, out var value))
		{
			return null;
		}
		return value;
	}

	private static void SetMappedWorldIdForHost(string baseDir, string hostIdentity, string worldId)
	{
		if (string.IsNullOrEmpty(hostIdentity) || string.IsNullOrEmpty(worldId))
		{
			return;
		}
		Dictionary<string, string> dictionary = ReadHostWorldIds(baseDir);
		dictionary[hostIdentity] = worldId;
		try
		{
			File.WriteAllText(Path.Combine(baseDir, "coop_host_worldids.json"), JsonConvert.SerializeObject((object)dictionary));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[World][WorldId] falha salvando coop_host_worldids.json: " + ex.Message);
		}
	}

	private static Dictionary<string, string> ReadHostWorldIds(string baseDir)
	{
		string path = Path.Combine(baseDir, "coop_host_worldids.json");
		try
		{
			if (File.Exists(path))
			{
				return JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path)) ?? new Dictionary<string, string>();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[World][WorldId] falha lendo coop_host_worldids.json: " + ex.Message);
		}
		return new Dictionary<string, string>();
	}

	public static byte[] SerializeWorld()
	{
		try
		{
			SaveManager instance = Singleton<SaveManager>.Instance;
			if ((Object)(object)instance == (Object)null)
			{
				Plugin.Log.LogWarning("[World][M2.2] SaveManager.Instance null — não dá pra ler o save agora.");
				return Array.Empty<byte>();
			}
			List<SaveFile> list = new List<SaveFile>();
			AddIfExists(list, "sav.dat", instance.dynamicFile);
			AddIfExists(list, "savs.dat", instance.staticFile);
			bool freshlyGenerated;
			string text = EnsureWorldId(out freshlyGenerated);
			if (!string.IsNullOrEmpty(text))
			{
				list.Add(new SaveFile
				{
					Name = "coop_world_id.txt",
					Data = Encoding.UTF8.GetBytes(text)
				});
			}
			if (freshlyGenerated)
			{
				Plugin.Log.LogInfo("[World][WorldId] mundo novo detectado (1ª vez servindo este perfil) - zerando personagens salvos de todo mundo.");
				CharacterSaveSync.ResetAllRecords();
			}
			string text2 = ((Core.currentProfile != null) ? JsonConvert.SerializeObject((object)Core.currentProfile) : null);
			using MemoryStream memoryStream = new MemoryStream();
			using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
			binaryWriter.Write((ushort)list.Count);
			foreach (SaveFile item in list)
			{
				binaryWriter.Write(item.Name);
				binaryWriter.Write(item.Data.Length);
				binaryWriter.Write(item.Data);
			}
			binaryWriter.Write(text2 ?? "");
			byte[] array = memoryStream.ToArray();
			byte[] array2 = Compress(array);
			Plugin.Log.LogInfo($"[World][M2.2] serializado save do perfil ativo: {list.Count} arquivo(s) + " + $"metadados de perfil, {array.Length} bytes totais ({array2.Length} comprimido, " + $"{((array.Length != 0) ? (100 - array2.Length * 100 / array.Length) : 0)}% menor).");
			return array2;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogError("[World][M2.2] falha lendo save do host: " + ex.GetType().Name + ": " + ex.Message);
			return Array.Empty<byte>();
		}
	}

	private static byte[] Compress(byte[] data)
	{
		using MemoryStream memoryStream = new MemoryStream();
		using (GZipStream gZipStream = new GZipStream(memoryStream, CompressionMode.Compress))
		{
			gZipStream.Write(data, 0, data.Length);
		}
		return memoryStream.ToArray();
	}

	private static byte[] Decompress(byte[] data)
	{
		using MemoryStream stream = new MemoryStream(data);
		using GZipStream gZipStream = new GZipStream(stream, CompressionMode.Decompress);
		using MemoryStream memoryStream = new MemoryStream();
		gZipStream.CopyTo(memoryStream);
		return memoryStream.ToArray();
	}

	private static void AddIfExists(List<SaveFile> list, string name, string path)
	{
		if (!string.IsNullOrEmpty(path) && File.Exists(path))
		{
			list.Add(new SaveFile
			{
				Name = name,
				Data = File.ReadAllBytes(path)
			});
			return;
		}
		Plugin.Log.LogWarning("[World][M2.2] '" + name + "' não existe em '" + path + "' — pulando.");
	}

	private static bool SlotStillOwnedBy(string baseDir, int slotId, string hostIdentity)
	{
		string path = Path.Combine(baseDir, "prof" + slotId, "coop_owner.txt");
		try
		{
			return File.Exists(path) && File.ReadAllText(path).Trim() == hostIdentity;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning(string.Format("[World][M2.2] falha lendo {0} do slot {1}: {2} - tratando como não-nosso.", "coop_owner.txt", slotId, ex.Message));
			return false;
		}
	}

	public static int GetMappedSlotForHost(string hostIdentity)
	{
		Dictionary<string, int> dictionary = ReadHostSlots(Path.Combine(Application.persistentDataPath, "1_4Save"));
		if (!string.IsNullOrEmpty(hostIdentity) && dictionary.TryGetValue(hostIdentity, out var value) && value >= 1 && value <= 5)
		{
			return value;
		}
		return -1;
	}

	public static SlotSummary[] GetSlotSummaries()
	{
		string path = Path.Combine(Application.persistentDataPath, "1_4Save");
		MainMenu.SaveState val = (MainMenu.SaveState)(((object)ReadProfiles(aes: new SimpleAES(), path: Path.Combine(path, "profs.dat"))) ?? ((object)new MainMenu.SaveState()));
		SlotSummary[] array = new SlotSummary[5];
		int id;
		for (id = 1; id <= 5; id++)
		{
			GameProfile val2 = val.profiles.Find((GameProfile x) => x != null && x.Active && x.id == id);
			array[id - 1] = ((val2 != null) ? new SlotSummary(id, occupied: true, $"Dia {val2.day}, cap {val2.chapter}") : new SlotSummary(id, occupied: false, "Vazio"));
		}
		return array;
	}

	public static int DeserializeWorld(byte[] blob, string hostIdentity, int explicitSlot = -1)
	{
		try
		{
			blob = Decompress(blob);
			string text = Path.Combine(Application.persistentDataPath, "1_4Save");
			SimpleAES val = new SimpleAES();
			List<SaveFile> list;
			string text2;
			using (MemoryStream input = new MemoryStream(blob))
			{
				using BinaryReader binaryReader = new BinaryReader(input);
				int num = binaryReader.ReadUInt16();
				list = new List<SaveFile>(num);
				for (int i = 0; i < num; i++)
				{
					string name = binaryReader.ReadString();
					int count = binaryReader.ReadInt32();
					list.Add(new SaveFile
					{
						Name = name,
						Data = binaryReader.ReadBytes(count)
					});
				}
				text2 = binaryReader.ReadString();
			}
			string text3 = Path.Combine(text, "profs.dat");
			MainMenu.SaveState val2 = (MainMenu.SaveState)(((object)ReadProfiles(text3, val)) ?? ((object)new MainMenu.SaveState()));
			int slotId;
			bool reused;
			if (explicitSlot >= 1 && explicitSlot <= 5)
			{
				slotId = explicitSlot;
				reused = false;
				Dictionary<string, int> dictionary = ReadHostSlots(text);
				dictionary[hostIdentity] = slotId;
				WriteHostSlots(text, dictionary);
			}
			else
			{
				slotId = ResolveSlotForHost(text, hostIdentity, val2, out reused);
				if (slotId < 0)
				{
					Plugin.Log.LogError($"[World][M2.2] todos os {5} slots de save estão " + "ocupados por saves próprios e nenhum é deste host — não vou sobrescrever um save seu. Apague um slot no menu do jogo e tente de novo.");
					return -1;
				}
			}
			string text4 = Path.Combine(text, "prof" + slotId);
			if (Directory.Exists(text4))
			{
				string[] files = Directory.GetFiles(text4);
				for (int j = 0; j < files.Length; j++)
				{
					File.Delete(files[j]);
				}
			}
			else
			{
				Directory.CreateDirectory(text4);
			}
			foreach (SaveFile item in list)
			{
				File.WriteAllBytes(Path.Combine(text4, item.Name), item.Data);
				Plugin.Log.LogInfo($"[World][M2.2] gravado '{item.Name}' ({item.Data.Length} bytes) em '{text4}'.");
			}
			if (!string.IsNullOrEmpty(hostIdentity))
			{
				try
				{
					File.WriteAllText(Path.Combine(text4, "coop_owner.txt"), hostIdentity);
				}
				catch (Exception ex)
				{
					Plugin.Log.LogWarning(string.Format("[World][M2.2] falha gravando {0} do slot {1}: {2}", "coop_owner.txt", slotId, ex.Message));
				}
				SaveFile saveFile = list.Find((SaveFile f) => f.Name == "coop_world_id.txt");
				if (saveFile != null)
				{
					SetMappedWorldIdForHost(text, hostIdentity, Encoding.UTF8.GetString(saveFile.Data).Trim());
				}
			}
			GameProfile val3 = (GameProfile)((!string.IsNullOrEmpty(text2)) ? ((object)JsonConvert.DeserializeObject<GameProfile>(text2)) : ((object)new GameProfile(slotId, true, 0)));
			val3.id = slotId;
			val3.Active = true;
			val2.profiles.RemoveAll((GameProfile p) => p != null && p.id == slotId);
			val2.profiles.Add(val3);
			if (File.Exists(text3))
			{
				File.Copy(text3, text3 + "_bak", overwrite: true);
			}
			File.WriteAllText(text3, val.Encrypt(JsonConvert.SerializeObject((object)val2)));
			Plugin.Log.LogInfo("[World][M2.2] save do host '" + hostIdentity + "' " + (reused ? "ATUALIZADO no" : "gravado no NOVO") + " " + $"perfil Nº{slotId} (dia {val3.day}, cap {val3.chapter}).");
			return slotId;
		}
		catch (Exception ex2)
		{
			Plugin.Log.LogError("[World][M2.2] falha gravando save recebido: " + ex2.GetType().Name + ": " + ex2.Message);
			return -1;
		}
	}

	private static int ResolveSlotForHost(string baseDir, string hostIdentity, MainMenu.SaveState saveState, out bool reused)
	{
		reused = false;
		Dictionary<string, int> dictionary = ReadHostSlots(baseDir);
		if (!string.IsNullOrEmpty(hostIdentity) && dictionary.TryGetValue(hostIdentity, out var value) && value >= 1 && value <= 5)
		{
			if (SlotStillOwnedBy(baseDir, value, hostIdentity))
			{
				reused = true;
				return value;
			}
			Plugin.Log.LogWarning($"[World][M2.2] slot {value} estava mapeado pro host '{hostIdentity}' mas não é " + "mais dono dele (conteúdo mudou por fora) - procurando outro slot em vez de sobrescrever.");
			dictionary.Remove(hostIdentity);
		}
		HashSet<int> hashSet = new HashSet<int>();
		foreach (GameProfile profile in saveState.profiles)
		{
			if (profile != null && profile.Active)
			{
				hashSet.Add(profile.id);
			}
		}
		foreach (KeyValuePair<string, int> item in dictionary)
		{
			hashSet.Add(item.Value);
		}
		for (int i = 1; i <= 5; i++)
		{
			if (!hashSet.Contains(i))
			{
				if (!string.IsNullOrEmpty(hostIdentity))
				{
					dictionary[hostIdentity] = i;
					WriteHostSlots(baseDir, dictionary);
				}
				return i;
			}
		}
		return -1;
	}

	private static Dictionary<string, int> ReadHostSlots(string baseDir)
	{
		string path = Path.Combine(baseDir, "coop_host_slots.json");
		try
		{
			if (File.Exists(path))
			{
				return JsonConvert.DeserializeObject<Dictionary<string, int>>(File.ReadAllText(path)) ?? new Dictionary<string, int>();
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[World][M2.2] falha lendo coop_host_slots.json: " + ex.Message);
		}
		return new Dictionary<string, int>();
	}

	private static void WriteHostSlots(string baseDir, Dictionary<string, int> map)
	{
		try
		{
			File.WriteAllText(Path.Combine(baseDir, "coop_host_slots.json"), JsonConvert.SerializeObject((object)map));
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[World][M2.2] falha salvando coop_host_slots.json: " + ex.Message);
		}
	}

	private static MainMenu.SaveState ReadProfiles(string path, SimpleAES aes)
	{
		if (!File.Exists(path))
		{
			return null;
		}
		string text = File.ReadAllText(path);
		try
		{
			return JsonConvert.DeserializeObject<MainMenu.SaveState>(aes.Decrypt(text));
		}
		catch
		{
			try
			{
				return JsonConvert.DeserializeObject<MainMenu.SaveState>(text);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[World][M2.2] falha lendo profs.dat existente: " + ex.Message + " — tratando como vazio.");
				return null;
			}
		}
	}

	public static void DumpSaveApi()
	{
		string[] array = new string[4] { "SaveManager", "SaveGameManager", "LevelSerializer", "Storage" };
		foreach (string text in array)
		{
			Type type = AccessTools_TypeByName(text);
			if (type == null)
			{
				Plugin.Log.LogInfo("[API] tipo " + text + " não encontrado");
				continue;
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine("=== " + type.FullName + " ===");
			MethodInfo[] methods = type.GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
			foreach (MethodInfo methodInfo in methods)
			{
				ParameterInfo[] parameters = methodInfo.GetParameters();
				string[] array2 = new string[parameters.Length];
				for (int k = 0; k < parameters.Length; k++)
				{
					array2[k] = parameters[k].ParameterType.Name + " " + parameters[k].Name;
				}
				stringBuilder.AppendLine("  " + methodInfo.ReturnType.Name + " " + methodInfo.Name + "(" + string.Join(", ", array2) + ")");
			}
			Plugin.Log.LogInfo(stringBuilder.ToString());
		}
	}

	private static Type AccessTools_TypeByName(string simpleName)
	{
		Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
		for (int i = 0; i < assemblies.Length; i++)
		{
			Type type = assemblies[i].GetType(simpleName, throwOnError: false);
			if (type != null)
			{
				return type;
			}
		}
		return null;
	}
}
