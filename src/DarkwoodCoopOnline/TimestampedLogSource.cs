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
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		_queue.Enqueue((level, Prefix(data)));
	}

	public void LogDebug(object data)
	{
		Enqueue((LogLevel)32, data);
	}

	public void LogInfo(object data)
	{
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
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
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
