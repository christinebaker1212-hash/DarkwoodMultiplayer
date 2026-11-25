using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class PerfDiagnostics
{
	public static bool Enabled = true;

	private const float SpikeMs = 15f;

	private static readonly Stopwatch _wallClock = new Stopwatch();

	private static double _lastMarkMs = -1.0;

	private static float _trueFrameMs;

	private static readonly Stopwatch _ownSw = new Stopwatch();

	private static int _gc0;

	private static int _gc1;

	private static int _gc2;

	private static readonly Dictionary<string, double> _sections = new Dictionary<string, double>();

	private static readonly Stopwatch _sectionSw = new Stopwatch();

	private static string _curSection;

	public static void ToggleEnabled()
	{
		Enabled = !Enabled;
		Plugin.Log.LogInfo($"[PerfDiag] profiling = {Enabled} (limiar={15f}ms)");
	}

	public static void FrameStart()
	{
		if (!_wallClock.IsRunning)
		{
			_wallClock.Start();
		}
		double totalMilliseconds = _wallClock.Elapsed.TotalMilliseconds;
		_trueFrameMs = ((_lastMarkMs >= 0.0) ? ((float)(totalMilliseconds - _lastMarkMs)) : 0f);
		_lastMarkMs = totalMilliseconds;
		if (Enabled)
		{
			_gc0 = GC.CollectionCount(0);
			_gc1 = GC.CollectionCount(1);
			_gc2 = GC.CollectionCount(2);
			_sections.Clear();
			_ownSw.Restart();
		}
	}

	public static void Begin(string section)
	{
		if (Enabled)
		{
			_curSection = section;
			_sectionSw.Restart();
		}
	}

	public static void End()
	{
		if (Enabled && _curSection != null)
		{
			_sectionSw.Stop();
			_sections.TryGetValue(_curSection, out var value);
			_sections[_curSection] = value + _sectionSw.Elapsed.TotalMilliseconds;
			_curSection = null;
		}
	}

	public static void FrameEnd()
	{
		if (!Enabled)
		{
			return;
		}
		_ownSw.Stop();
		float num = (float)_ownSw.Elapsed.TotalMilliseconds;
		if (_trueFrameMs < 15f && num < 15f)
		{
			return;
		}
		int num2 = GC.CollectionCount(0) - _gc0;
		int num3 = GC.CollectionCount(1) - _gc1;
		int num4 = GC.CollectionCount(2) - _gc2;
		string arg = "?";
		int num5 = -1;
		try
		{
			if ((Object)(object)Singleton<Controller>.Instance != (Object)null)
			{
				arg = (Core.isDay() ? $"dia(t={Singleton<Controller>.Instance.CurrentTime})" : $"noite(t={Singleton<Controller>.Instance.CurrentTime})");
			}
			num5 = SceneCache.Characters().Length;
		}
		catch
		{
		}
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append($"[PerfSpike] true={_trueFrameMs:F1}ms own={num:F1}ms " + $"unscaledDt={Time.unscaledDeltaTime * 1000f:F1}ms {arg} characters={num5} " + $"GC(gen0={num2},gen1={num3},gen2={num4})");
		foreach (KeyValuePair<string, double> section in _sections)
		{
			stringBuilder.Append($" | {section.Key}={section.Value:F2}ms");
		}
		Plugin.Log.LogWarning(stringBuilder.ToString());
	}
}
