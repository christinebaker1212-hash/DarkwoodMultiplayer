using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DarkwoodCoopOnline.Net;

// A player's connection to the host's relay server (TCP or Steam). Frames arrive on a network thread
// and wait in Inbox for the game's main thread.
public sealed class RelayClient
{
	private ILink _link;

	public readonly ConcurrentQueue<byte[]> Inbox = new ConcurrentQueue<byte[]>();

	private volatile int _rttMs = -1;

	private float _pingTimer;

	public volatile string CloseReason;

	public ILink Link => _link;

	public bool Connected
	{
		get
		{
			if (_link != null)
			{
				return !_link.Closed;
			}
			return false;
		}
	}

	public int RttMs => _rttMs;

	public string Address => _link?.Address ?? "";

	public void Attach(ILink link)
	{
		_link = link;
	}

	// Called by the link's reader (any thread)
	public void OnFrame(byte[] f)
	{
		if (f.Length == 0)
		{
			return;
		}
		if (f[0] == Proto.S_PONG && f.Length >= 5)
		{
			uint num = (uint)(f[1] | (f[2] << 8) | (f[3] << 16) | (f[4] << 24));
			_rttMs = Math.Max(0, Environment.TickCount - (int)num);
		}
		else
		{
			Inbox.Enqueue(f);
		}
	}

	public void Tick(float dt)
	{
		_pingTimer += dt;
		if (_pingTimer >= 1f && Connected)
		{
			_pingTimer = 0f;
			_link.Send(new MsgWriter(Proto.C_PING).U32((uint)Environment.TickCount).Done());
		}
	}

	public void Join(byte[] key, string name, string stableId)
	{
		_link?.Send(new MsgWriter(Proto.C_JOIN).U16(Proto.Version).Raw(key).Str(name)
			.Str(stableId)
			.Done());
	}

	public void Hello()
	{
		_link?.Send(new byte[1] { Proto.C_HELLO });
	}

	public void RequestSave()
	{
		_link?.Send(new byte[1] { Proto.C_SAVE_REQ });
	}

	public void SendState(byte[] packet)
	{
		_link?.Send(MsgWriter.Wrap(Proto.C_STATE, packet), droppable: true, 1);
	}

	// target: a slot, or -1 for everyone
	public void SendGame(int target, byte[] packet, bool droppable)
	{
		if (_link != null)
		{
			byte[] array = new byte[packet.Length + 3];
			array[0] = Proto.C_RELAY;
			array[1] = (byte)((target < 0) ? 255 : target);
			array[2] = (byte)(droppable ? 1u : 0u);
			Buffer.BlockCopy(packet, 0, array, 3, packet.Length);
			_link.Send(array, droppable, droppable ? (256 + packet[0]) : 0);
		}
	}

	public void Leave()
	{
		ILink link = _link;
		if (link == null)
		{
			return;
		}
		_link = null;
		try
		{
			link.Send(new byte[1] { Proto.C_LEAVE });
			if (link is TcpLink tcpLink)
			{
				ThreadPool.QueueUserWorkItem(delegate
				{
					tcpLink.CloseAfterFlush(500);
				});
			}
			else
			{
				link.Close();
			}
		}
		catch
		{
		}
	}
}
