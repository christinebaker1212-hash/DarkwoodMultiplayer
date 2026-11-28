using System;
using System.Collections.Concurrent;
using System.Threading;
using BepInEx.Logging;

namespace DarkwoodCoopOnline;

internal sealed class TimestampedLogSource
{
	private readonly ManualLogSource _inner;

	private readonly ConcurrentQueue<(LogLevel level, string msg)> _queue = new ConcurrentQueue<(LogLevel, string)>();

	public TimestampedLogSource(ManualLogSource inner)
	{
		_inner = inner;
		Thread thread = new Thread(WriterLoop);
		thread.IsBackground = true;
		thread.Name = "DarkwoodCoopLogWriter";
		thread.Start();
	}

	private static string Prefix(object data)
	{
		return $"[{DateTime.Now:HH:mm:ss.fff}] {data}";
	}

	private void Enqueue(LogLevel level, object data)
	{
		_queue.Enqueue((level, Prefix(data)));
	}

	public void LogDebug(object data)
	{
		Enqueue((LogLevel)32, data);
	}

	// Off by default: the mod's detailed per-event diagnostics are dropped (they cost string building and
	// disk writes every time something moves). Connection / session lines are always kept.
	public static bool Verbose;

	private static readonly string[] AlwaysKeep = new string[15] { "[SkinExport]", "[Skins]", "[Relay]", "[Net]", "[Join]", "[Host]", "[Session]", "[PortMap]", "[Steam]", "[Notice]", "[LanSaveTransfer]", "[World]", "[VersionCheck]", "[StableIdentity]", "===" };

	public void LogInfo(object data)
	{
		if (!Verbose)
		{
			string text = data as string;
			if (text == null)
			{
				return;
			}
			bool flag = false;
			for (int i = 0; i < AlwaysKeep.Length; i++)
			{
				if (text.StartsWith(AlwaysKeep[i], StringComparison.Ordinal))
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return;
			}
		}
		Enqueue((LogLevel)16, data);
	}

	public void LogMessage(object data)
	{
		Enqueue((LogLevel)8, data);
	}

	public void LogWarning(object data)
	{
		Enqueue((LogLevel)4, data);
	}

	public void LogError(object data)
	{
		Enqueue((LogLevel)2, data);
	}

	public void LogFatal(object data)
	{
		Enqueue((LogLevel)1, data);
	}

	private void WriterLoop()
	{
		while (true)
		{
			bool flag = false;
			(LogLevel, string) result;
			while (_queue.TryDequeue(out result))
			{
				flag = true;
				_inner.Log(result.Item1, (object)result.Item2);
			}
			if (!flag)
			{
				Thread.Sleep(20);
			}
		}
	}
}
