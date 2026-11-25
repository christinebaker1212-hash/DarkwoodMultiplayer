using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using DarkwoodCoopOnline.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class WorkbenchSync
{
	[CompilerGenerated]
	private sealed class _003CNamesOf_003Ed__7 : IEnumerable<string>, IEnumerable, IEnumerator<string>, IDisposable, IEnumerator
	{
		private int _003C_003E1__state;

		private string _003C_003E2__current;

		private int _003C_003El__initialThreadId;

		private Workbench[] all;

		public Workbench[] _003C_003E3__all;

		private Workbench[] _003C_003E7__wrap1;

		private int _003C_003E7__wrap2;

		string IEnumerator<string>.Current
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
		public _003CNamesOf_003Ed__7(int _003C_003E1__state)
		{
			this._003C_003E1__state = _003C_003E1__state;
			_003C_003El__initialThreadId = Environment.CurrentManagedThreadId;
		}

		[DebuggerHidden]
		void IDisposable.Dispose()
		{
			_003C_003E7__wrap1 = null;
			_003C_003E1__state = -2;
		}

		private bool MoveNext()
		{
			int num = _003C_003E1__state;
			if (num != 0)
			{
				if (num != 1)
				{
					return false;
				}
				_003C_003E1__state = -1;
				goto IL_0064;
			}
			_003C_003E1__state = -1;
			_003C_003E7__wrap1 = all;
			_003C_003E7__wrap2 = 0;
			goto IL_0072;
			IL_0064:
			_003C_003E7__wrap2++;
			goto IL_0072;
			IL_0072:
			if (_003C_003E7__wrap2 < _003C_003E7__wrap1.Length)
			{
				Workbench val = _003C_003E7__wrap1[_003C_003E7__wrap2];
				if ((Object)(object)val != (Object)null)
				{
					_003C_003E2__current = ((Object)((Component)val).gameObject).name;
					_003C_003E1__state = 1;
					return true;
				}
				goto IL_0064;
			}
			_003C_003E7__wrap1 = null;
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
		IEnumerator<string> IEnumerable<string>.GetEnumerator()
		{
			_003CNamesOf_003Ed__7 _003CNamesOf_003Ed__;
			if (_003C_003E1__state == -2 && _003C_003El__initialThreadId == Environment.CurrentManagedThreadId)
			{
				_003C_003E1__state = 0;
				_003CNamesOf_003Ed__ = this;
			}
			else
			{
				_003CNamesOf_003Ed__ = new _003CNamesOf_003Ed__7(0);
			}
			_003CNamesOf_003Ed__.all = _003C_003E3__all;
			return _003CNamesOf_003Ed__;
		}

		[DebuggerHidden]
		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable<string>)this).GetEnumerator();
		}
	}

	private const float Range = 4000f;

	private const float MatchRadius = 250f;

	private static readonly Dictionary<string, int> _known = new Dictionary<string, int>();

	public static byte[] PollChanges(Vector3 p1, Vector3 p2)
	{
		List<(string, Vector3, int)> list = null;
		Workbench[] array = SceneCache.Workbenches();
		foreach (Workbench val in array)
		{
			if ((Object)(object)val == (Object)null || CoopPeers.MinDistance(((Component)val).transform.position, p1) > 4000f)
			{
				continue;
			}
			string key = KeyOf(((Object)((Component)val).gameObject).name, ((Component)val).transform.position);
			if (_known.TryGetValue(key, out var value) && value == val.currentLevel)
			{
				continue;
			}
			_known[key] = val.currentLevel;
			if (val.currentLevel > 0)
			{
				if (list == null)
				{
					list = new List<(string, Vector3, int)>();
				}
				list.Add((((Object)((Component)val).gameObject).name, ((Component)val).transform.position, val.currentLevel));
				Plugin.Log.LogInfo($"[WorkbenchSync] detectei upgrade LOCAL '{((Object)((Component)val).gameObject).name}' pos={((Component)val).transform.position:F0} " + $"nível={val.currentLevel} (prevConhecido={value}) - mandando pro peer.");
			}
		}
		if (list == null)
		{
			return null;
		}
		PacketWriter packetWriter = PacketWriter.Begin(PacketType.WorkbenchUpgraded).UShort((ushort)list.Count);
		foreach (var item in list)
		{
			packetWriter.Str(item.Item1).Vec3(item.Item2).Int(item.Item3);
		}
		return packetWriter.Finish();
	}

	public static void ApplyChanges(PacketReader r)
	{
		int num = r.UShort();
		for (int i = 0; i < num; i++)
		{
			string text = r.Str();
			Vector3 val = r.Vec3();
			int num2 = r.Int();
			_known[KeyOf(text, val)] = num2;
			Workbench val2 = FindNearest(text, val);
			if ((Object)(object)val2 != (Object)null && val2.currentLevel < num2)
			{
				Controller instance = Singleton<Controller>.Instance;
				if ((Object)(object)instance != (Object)null)
				{
					instance.workbenchLevel = num2;
				}
				val2.setRecipes();
				val2.refreshWorkbenchUpgrade();
				Plugin.Log.LogInfo($"[WorkbenchSync] '{text}' upado pelo peer (nível {num2}) - receitas repopuladas.");
			}
			else if ((Object)(object)val2 == (Object)null)
			{
				Plugin.Log.LogWarning($"[WorkbenchSync][Diag] peer avisou upgrade de '{text}' pos={val:F0} nível={num2}, " + $"mas NENHUMA bancada foi achada num raio de {250f:F0}un aqui - upgrade NÃO aplicado. " + "Bancadas conhecidas nesta máquina: " + DumpKnownWorkbenches(text));
			}
			else
			{
				Plugin.Log.LogInfo($"[WorkbenchSync][Diag] peer avisou upgrade de '{text}' nível={num2}, " + $"mas a bancada local já está no nível {val2.currentLevel} (>=) - ignorado (esperado se o eco chegou depois de eu mesmo já ter upado).");
			}
		}
	}

	public static void ForceRebroadcast()
	{
		_known.Clear();
	}

	private static string DumpKnownWorkbenches(string name)
	{
		Workbench[] array = SceneCache.Workbenches();
		List<string> list = new List<string>();
		Workbench[] array2 = array;
		foreach (Workbench val in array2)
		{
			if (!((Object)(object)val == (Object)null) && ((Object)((Component)val).gameObject).name == name)
			{
				list.Add($"pos={((Component)val).transform.position:F0} nível={val.currentLevel}");
			}
		}
		if (array.Length == 0)
		{
			return "(SceneCache não tem NENHUMA bancada cacheada aqui)";
		}
		if (list.Count == 0)
		{
			return string.Format("(nenhuma com esse NOME - {0} bancada(s) aqui, nomes: {1})", array.Length, string.Join(", ", NamesOf(array)));
		}
		return string.Join(" | ", list);
	}

	[IteratorStateMachine(typeof(_003CNamesOf_003Ed__7))]
	private static IEnumerable<string> NamesOf(Workbench[] all)
	{
		//yield-return decompiler failed: Unexpected instruction in Iterator.Dispose()
		return new _003CNamesOf_003Ed__7(-2)
		{
			_003C_003E3__all = all
		};
	}

	private static Workbench FindNearest(string name, Vector3 pos)
	{
		Workbench result = null;
		float num = 250f;
		Workbench[] array = SceneCache.Workbenches();
		foreach (Workbench val in array)
		{
			if (!((Object)(object)val == (Object)null) && !(((Object)((Component)val).gameObject).name != name))
			{
				float num2 = Vector3.Distance(((Component)val).transform.position, pos);
				if (num2 < num)
				{
					num = num2;
					result = val;
				}
			}
		}
		return result;
	}

	private static string KeyOf(string name, Vector3 p)
	{
		return $"{name}@{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static void Reset()
	{
		_known.Clear();
	}
}
