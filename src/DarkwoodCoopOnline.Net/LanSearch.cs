using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace DarkwoodCoopOnline.Net;

// Finds games hosted on the local network (the host's RelayServer answers UDP probes).
public static class LanSearch
{
	public sealed class Game
	{
		public string Name;

		public string Host;

		public int Port;

		public int Players;

		public int Max;

		public bool Locked;

		public ushort Version;
	}

	private static readonly object Gate = new object();

	private static readonly List<Game> Found = new List<Game>();

	private static UdpClient _udp;

	private static volatile bool _running;

	public static bool Searching => _running;

	public static List<Game> Games
	{
		get
		{
			lock (Gate)
			{
				return new List<Game>(Found);
			}
		}
	}

	public static void Start()
	{
		Stop();
		lock (Gate)
		{
			Found.Clear();
		}
		try
		{
			_udp = new UdpClient(0)
			{
				EnableBroadcast = true
			};
			_running = true;
			new Thread(Loop)
			{
				IsBackground = true,
				Name = "CoopLanSearch"
			}.Start();
		}
		catch (Exception ex)
		{
			Plugin.Log?.LogWarning("[LanSearch] " + ex.Message);
			_running = false;
		}
	}

	public static void Stop()
	{
		_running = false;
		try
		{
			_udp?.Close();
		}
		catch
		{
		}
		_udp = null;
	}

	private static void Probe(UdpClient udp)
	{
		byte[] array = new byte[9];
		Encoding.ASCII.GetBytes("DWCOOP?").CopyTo(array, 0);
		array[7] = (byte)Proto.Version;
		array[8] = (byte)(Proto.Version >> 8);
		List<IPAddress> list = new List<IPAddress>
		{
			IPAddress.Broadcast,
			IPAddress.Loopback
		};
		// directed broadcast on every adapter (Windows sends the plain broadcast through one adapter only)
		try
		{
			foreach (System.Net.NetworkInformation.NetworkInterface item in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
			{
				if (item.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up)
				{
					continue;
				}
				foreach (System.Net.NetworkInformation.UnicastIPAddressInformation unicastAddress in item.GetIPProperties().UnicastAddresses)
				{
					if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork && unicastAddress.IPv4Mask != null)
					{
						byte[] addressBytes = unicastAddress.Address.GetAddressBytes();
						byte[] addressBytes2 = unicastAddress.IPv4Mask.GetAddressBytes();
						byte[] array2 = new byte[4];
						for (int i = 0; i < 4; i++)
						{
							array2[i] = (byte)(addressBytes[i] | ~addressBytes2[i]);
						}
						list.Add(new IPAddress(array2));
					}
				}
			}
		}
		catch
		{
		}
		foreach (IPAddress item2 in list)
		{
			try
			{
				udp.Send(array, array.Length, new IPEndPoint(item2, Proto.DiscoveryPort));
			}
			catch
			{
			}
		}
	}

	private static void Loop()
	{
		UdpClient udp = _udp;
		DateTime dateTime = DateTime.MinValue;
		udp.Client.ReceiveTimeout = 250;
		while (_running)
		{
			if ((DateTime.UtcNow - dateTime).TotalSeconds > 2.0)
			{
				dateTime = DateTime.UtcNow;
				Probe(udp);
			}
			byte[] array;
			IPEndPoint remoteEP = null;
			try
			{
				array = udp.Receive(ref remoteEP);
			}
			catch
			{
				continue;
			}
			try
			{
				if (array.Length < 14 || Encoding.ASCII.GetString(array, 0, 7) != "DWCOOP!")
				{
					continue;
				}
				MsgReader msgReader = new MsgReader(array, 7);
				Game game = new Game
				{
					Version = msgReader.U16(),
					Port = msgReader.U16(),
					Players = msgReader.U8(),
					Max = msgReader.U8(),
					Locked = (msgReader.U8() != 0),
					Name = msgReader.Str(),
					Host = remoteEP.Address.ToString()
				};
				lock (Gate)
				{
					int num = Found.FindIndex((Game g) => g.Name == game.Name && g.Port == game.Port);
					if (num < 0)
					{
						Found.Add(game);
					}
					else if (game.Host != "127.0.0.1")
					{
						// prefer the real address over loopback when a game shows up through both
						Found[num] = game;
					}
				}
			}
			catch
			{
			}
		}
	}
}
