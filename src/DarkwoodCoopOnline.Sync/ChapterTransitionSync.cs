using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using DarkwoodCoopOnline.Net;
using HarmonyLib;

namespace DarkwoodCoopOnline.Sync;

public static class ChapterTransitionSync
{
	[CompilerGenerated]
	private sealed class _003CBuildChapterSaveChunks_003Ed__8 : IEnumerable<byte[]>, IEnumerable, IEnumerator<byte[]>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private byte[] _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private int chapterId;

		public int _003C_003E3__chapterId;

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
		public _003CBuildChapterSaveChunks_003Ed__8(int _003C_003E1__state)
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
				_003Cblob_003E5__2 = WorldSync.SerializeWorld();
				_003Ctotal_003E5__3 = (_003Cblob_003E5__2.Length + 60000 - 1) / 60000;
				if (_003Ctotal_003E5__3 == 0)
				{
					_003Ctotal_003E5__3 = 1;
				}
				Plugin.Log.LogInfo($"[ChapterTransitionSync] save do capítulo {chapterId}: {_003Cblob_003E5__2.Length} bytes → {_003Ctotal_003E5__3} fatia(s).");
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
				_003C_003E2__current = PacketWriter.Begin(PacketType.ChapterSaveChunk).Int(chapterId).UShort((ushort)_003Ci_003E5__4)
					.UShort((ushort)_003Ctotal_003E5__3)
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
			_003CBuildChapterSaveChunks_003Ed__8 _003CBuildChapterSaveChunks_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CBuildChapterSaveChunks_003Ed__ = this;
			}
			else
			{
				_003CBuildChapterSaveChunks_003Ed__ = new _003CBuildChapterSaveChunks_003Ed__8(0);
			}
			_003CBuildChapterSaveChunks_003Ed__.chapterId = _003C_003E3__chapterId;
			return _003CBuildChapterSaveChunks_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<byte[]>)this).GetEnumerator();
		}
	}

	private static bool _patched;

	private static bool _applyingRemote;

	private static bool _waitingForOwnSceneLoad;

	private static int _pendingChapterId;

	private const int ChunkSize = 60000;

	private static byte[][] _chunks;

	private static int _received;

	private static int _chunkChapterId;

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Controller), "generateChapter", new Type[3]
			{
				typeof(int),
				typeof(bool),
				typeof(bool)
			}, (Type[])null), new HarmonyMethod(typeof(ChapterTransitionSync), "BeforeGenerateChapter", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool BeforeGenerateChapter(int _chapterId, bool generateSave, bool loadChapterSave)
	{
		Plugin.Log.LogWarning($"[ChapterTransitionSync][Diag] generateChapter() CHAMADO: chapter={_chapterId} " + $"generateSave={generateSave} loadChapterSave={loadChapterSave} applyingRemote={_applyingRemote} " + $"sessionActive={CoopRuntime.SessionActive} lobbyActive={LobbySync.IsActive} " + $"sessionLinked={CoopRuntime.Session?.Linked} role={CoopRuntime.Session?.Role}");
		if (_applyingRemote)
		{
			return true;
		}
		if (!CoopRuntime.SessionActive)
		{
			return true;
		}
		if (CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
		{
			_pendingChapterId = _chapterId;
			_waitingForOwnSceneLoad = true;
			Plugin.Log.LogInfo($"[ChapterTransitionSync] HOST disparando troca de capítulo de verdade: chapter={_chapterId} " + $"generateSave={generateSave} loadChapterSave={loadChapterSave}.");
			return true;
		}
		Plugin.Log.LogWarning($"[ChapterTransitionSync] CLIENTE alcançou o gatilho de troca de capítulo (chapter={_chapterId}) " + "— NÃO trocando sozinho, avisando o host.");
		CoopRuntime.Session.SendReliable(PacketWriter.Begin(PacketType.ChapterTransitionRequest).Int(_chapterId).Bool(generateSave)
			.Bool(loadChapterSave)
			.Finish());
		return false;
	}

	public static void Tick()
	{
		if (_waitingForOwnSceneLoad && Core.worldGenFinished())
		{
			_waitingForOwnSceneLoad = false;
			Plugin.Log.LogInfo($"[ChapterTransitionSync] troca de capítulo do HOST terminou (chapter={_pendingChapterId}) " + "— mandando o save gerado pro cliente (fatiado, pausado por tick).");
			CoopRuntime.Session.EnqueuePacedChunks(BuildChapterSaveChunks(_pendingChapterId));
		}
	}

	[IteratorStateMachine(typeof(_003CBuildChapterSaveChunks_003Ed__8))]
	private static IEnumerable<byte[]> BuildChapterSaveChunks(int chapterId)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CBuildChapterSaveChunks_003Ed__8(-2)
		{
			_003C_003E3__chapterId = chapterId
		};
	}

	public static void ApplyRequest(PacketReader r)
	{
		int num = r.Int();
		r.Bool();
		r.Bool();
		if (CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
		{
			Plugin.Log.LogInfo($"[ChapterTransitionSync] cliente pediu troca de capítulo (chapter={num}) — HOST executando agora.");
			Singleton<Controller>.Instance.generateChapter(num, true, false);
		}
	}

	public static void ApplyChapterSave(PacketReader r)
	{
		if (CoopRuntime.Session != null && CoopRuntime.Session.Role == NetRole.Host)
		{
			return;
		}
		int num = r.Int();
		int num2 = r.UShort();
		int num3 = r.UShort();
		byte[] array = r.Bytes();
		if (_chunks == null || _chunks.Length != num3)
		{
			_chunks = new byte[num3][];
			_received = 0;
		}
		_chunkChapterId = num;
		if (_chunks[num2] == null)
		{
			_chunks[num2] = array;
			_received++;
		}
		if (_received < num3)
		{
			return;
		}
		byte[] array3;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			byte[][] chunks = _chunks;
			foreach (byte[] array2 in chunks)
			{
				memoryStream.Write(array2, 0, array2.Length);
			}
			array3 = memoryStream.ToArray();
		}
		_chunks = null;
		_received = 0;
		Plugin.Log.LogInfo($"[ChapterTransitionSync] save do capítulo {num} completo ({array3.Length} bytes) - " + "gravando no próprio slot e carregando (sem gerar nada aqui).");
		string hostIdentity = CoopRuntime.Session?.ResolvedPeerIdentity;
		if (WorldSync.DeserializeWorld(array3, hostIdentity) < 0)
		{
			Plugin.Log.LogError("[ChapterTransitionSync] falha gravando o save do capítulo recebido - NÃO vou trocar de cena (ficaria com o capítulo velho, mais seguro que travar num estado misto).");
			return;
		}
		_applyingRemote = true;
		Singleton<Controller>.Instance.generateChapter(_chunkChapterId, false, true);
		_applyingRemote = false;
	}

	public static void Reset()
	{
		_waitingForOwnSceneLoad = false;
		_chunks = null;
		_received = 0;
	}
}
