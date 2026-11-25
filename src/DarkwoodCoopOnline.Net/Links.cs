using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DarkwoodCoopOnline.Net;

// One connection between two games, carrying protocol frames (see Proto).
// Sends never block the caller: frames go into a queue that a background thread writes out.
public interface ILink
{
	string Address { get; }

	bool Closed { get; }

	// droppable: a frame that is replaced by the next one of the same coalesceKey (positions, enemy
	// movement...). When the connection is slower than what we send, stale droppable frames are skipped
	// instead of queueing up (that queueing is what turns a slow moment into seconds of lag).
	void Send(byte[] frame, bool droppable = false, int coalesceKey = 0);

	void Close();

	// Bytes waiting to go out (to show a "slow connection" warning)
	int QueuedBytes { get; }
}

public sealed class TcpLink : ILink
{
	private struct Item
	{
		public byte[] Data;

		public int Key;

		public long Seq;
	}

	private const int DropAboveBytes = 192 * 1024;

	private const int KeepAliveMs = 2000;

	private readonly Socket _socket;

	private readonly Queue<Item> _queue = new Queue<Item>();

	private readonly Dictionary<int, long> _latest = new Dictionary<int, long>();

	private long _seq;

	private int _queued;

	private volatile bool _closed;

	private readonly Action<TcpLink, byte[]> _onFrame;

	private readonly Action<TcpLink> _onClosed;

	private int _closedFired;

	public object Tag;

	public string Address { get; }

	public bool Closed => _closed;

	public int QueuedBytes => _queued;

	public DateTime LastReceived { get; private set; } = DateTime.UtcNow;


	public TcpLink(Socket socket, Action<TcpLink, byte[]> onFrame, Action<TcpLink> onClosed)
	{
		_socket = socket;
		_onFrame = onFrame;
		_onClosed = onClosed;
		try
		{
			_socket.NoDelay = true;
			_socket.SendBufferSize = 256 * 1024;
			_socket.ReceiveBufferSize = 256 * 1024;
			_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, optionValue: true);
		}
		catch
		{
		}
		try
		{
			Address = ((IPEndPoint)_socket.RemoteEndPoint).Address.ToString();
		}
		catch
		{
			Address = "?";
		}
	}

	public void Start()
	{
		new Thread(ReadLoop)
		{
			IsBackground = true,
			Name = "CoopLinkRx"
		}.Start();
		new Thread(WriteLoop)
		{
			IsBackground = true,
			Name = "CoopLinkTx"
		}.Start();
	}

	// Connects with a timeout on a background thread; calls back with the link (started) or the error.
	public static void ConnectAsync(string host, int port, int timeoutMs, Action<TcpLink, byte[]> onFrame, Action<TcpLink> onClosed, Action<TcpLink, string> done)
	{
		new Thread((ThreadStart)delegate
		{
			Socket socket = null;
			try
			{
				IPAddress[] array = (IPAddress.TryParse(host, out IPAddress address) ? new IPAddress[1] { address } : Dns.GetHostAddresses(host));
				Exception ex = null;
				foreach (IPAddress iPAddress in array)
				{
					socket = new Socket(iPAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
					IAsyncResult asyncResult = socket.BeginConnect(new IPEndPoint(iPAddress, port), null, null);
					if (asyncResult.AsyncWaitHandle.WaitOne(timeoutMs) && socket.Connected)
					{
						socket.EndConnect(asyncResult);
						TcpLink tcpLink = new TcpLink(socket, onFrame, onClosed);
						tcpLink.Start();
						done(tcpLink, null);
						return;
					}
					try
					{
						socket.Close();
					}
					catch
					{
					}
					socket = null;
					ex = new TimeoutException("no answer");
				}
				done(null, (ex != null) ? ex.Message : "address not found");
			}
			catch (Exception ex2)
			{
				try
				{
					socket?.Close();
				}
				catch
				{
				}
				done(null, ex2.Message);
			}
		})
		{
			IsBackground = true,
			Name = "CoopConnect"
		}.Start();
	}

	public void Send(byte[] frame, bool droppable = false, int coalesceKey = 0)
	{
		if (_closed)
		{
			return;
		}
		lock (_queue)
		{
			if (droppable && _queued > DropAboveBytes)
			{
				return;
			}
			long num = ++_seq;
			if (droppable && coalesceKey != 0)
			{
				_latest[coalesceKey] = num;
			}
			_queue.Enqueue(new Item
			{
				Data = frame,
				Key = (droppable ? coalesceKey : 0),
				Seq = num
			});
			_queued += frame.Length + 4;
			Monitor.Pulse(_queue);
		}
	}

	private void WriteLoop()
	{
		byte[] array = new byte[64 * 1024];
		byte[] ping = new byte[1] { Proto.KEEPALIVE };
		try
		{
			while (!_closed)
			{
				int num = 0;
				lock (_queue)
				{
					if (_queue.Count == 0)
					{
						Monitor.Wait(_queue, KeepAliveMs);
					}
					if (_closed)
					{
						break;
					}
					if (_queue.Count == 0)
					{
						// keep-alive (also keeps NAT mappings and the other side's timeout happy while a world loads)
						num = Put(ref array, 0, ping);
					}
					// batch everything that's queued into one send (fewer packets on the wire)
					while (_queue.Count > 0 && num < 48 * 1024)
					{
						Item item = _queue.Dequeue();
						_queued -= item.Data.Length + 4;
						if (item.Key != 0 && _latest.TryGetValue(item.Key, out var value) && value != item.Seq)
						{
							continue;
						}
						num = Put(ref array, num, item.Data);
					}
				}
				int num2 = 0;
				while (num2 < num)
				{
					int num3 = _socket.Send(array, num2, num - num2, SocketFlags.None);
					if (num3 <= 0)
					{
						throw new IOException("send failed");
					}
					num2 += num3;
				}
			}
		}
		catch
		{
		}
		Close();
	}

	private static int Put(ref byte[] buf, int at, byte[] frame)
	{
		int num = at + 4 + frame.Length;
		if (num > buf.Length)
		{
			Array.Resize(ref buf, Math.Max(num, buf.Length * 2));
		}
		int num2 = frame.Length;
		buf[at] = (byte)num2;
		buf[at + 1] = (byte)(num2 >> 8);
		buf[at + 2] = (byte)(num2 >> 16);
		buf[at + 3] = (byte)(num2 >> 24);
		Buffer.BlockCopy(frame, 0, buf, at + 4, frame.Length);
		return num;
	}

	private void ReadLoop()
	{
		byte[] array = new byte[4];
		try
		{
			while (!_closed)
			{
				ReadExactly(array, 4);
				int num = array[0] | (array[1] << 8) | (array[2] << 16) | (array[3] << 24);
				if (num <= 0 || num > Proto.MaxFrame)
				{
					throw new IOException("bad frame length " + num);
				}
				byte[] array2 = new byte[num];
				ReadExactly(array2, num);
				LastReceived = DateTime.UtcNow;
				if (num == 1 && array2[0] == Proto.KEEPALIVE)
				{
					continue;
				}
				_onFrame?.Invoke(this, array2);
			}
		}
		catch
		{
		}
		Close();
	}

	private void ReadExactly(byte[] buf, int n)
	{
		int num = 0;
		while (num < n)
		{
			int num2 = _socket.Receive(buf, num, n - num, SocketFlags.None);
			if (num2 <= 0)
			{
				throw new IOException("closed");
			}
			num += num2;
		}
	}

	public void Close()
	{
		if (!_closed)
		{
			_closed = true;
			lock (_queue)
			{
				Monitor.PulseAll(_queue);
			}
		}
		try
		{
			_socket.Shutdown(SocketShutdown.Both);
		}
		catch
		{
		}
		try
		{
			_socket.Close();
		}
		catch
		{
		}
		if (Interlocked.Exchange(ref _closedFired, 1) == 0)
		{
			_onClosed?.Invoke(this);
		}
	}

	// Flushes what's queued (best effort, up to timeoutMs) then closes. Used for a polite goodbye.
	public void CloseAfterFlush(int timeoutMs)
	{
		DateTime dateTime = DateTime.UtcNow.AddMilliseconds(timeoutMs);
		while (_queued > 0 && !_closed && DateTime.UtcNow < dateTime)
		{
			Thread.Sleep(5);
		}
		Close();
	}
}
