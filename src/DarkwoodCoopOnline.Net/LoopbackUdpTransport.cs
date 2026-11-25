using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using BepInEx;

namespace DarkwoodCoopOnline.Net;

public sealed class LoopbackUdpTransport : ITransport
{
	private readonly int _localPort;

	private readonly int _remotePort;

	private UdpClient _udp;

	private IPEndPoint _remote;

	private Thread _rx;

	private volatile bool _running;

	private readonly ConcurrentQueue<byte[]> _inbox = new ConcurrentQueue<byte[]>();

	public bool Connected { get; private set; }

	public string LocalIdentity
	{
		get
		{
			int localPort = _localPort;
			return "loopback:" + localPort;
		}
	}

	public string PeerIdentity
	{
		get
		{
			int remotePort = _remotePort;
			return "loopback:" + remotePort;
		}
	}

	private static string PreferredRoleFilePath => Path.Combine(Paths.PluginPath, "DarkwoodCoopOnline_PreferredRole.txt");

	public LoopbackUdpTransport(int localPort, int remotePort, UdpClient preBound = null)
	{
		_localPort = localPort;
		_remotePort = remotePort;
		_udp = preBound;
		_remote = new IPEndPoint(IPAddress.Loopback, remotePort);
	}

	public static LoopbackUdpTransport CreateLanClient(int localPort, int remotePort, IPAddress hostAddress)
	{
		return new LoopbackUdpTransport(localPort, remotePort)
		{
			_remote = new IPEndPoint(hostAddress, remotePort)
		};
	}

	public static LoopbackUdpTransport CreateLanHost(int port)
	{
		return new LoopbackUdpTransport(port, port)
		{
			_remote = null
		};
	}

	public static LoopbackUdpTransport CreateAuto(int portA, int portB, out NetRole role)
	{
		string text = ReadPreferredRole();
		if (text == "Host")
		{
			UdpClient udpClient = TryBind(portA);
			if (udpClient != null)
			{
				role = NetRole.Host;
				Plugin.Log.LogInfo("[UDP] papel preferido (sessão anterior) = Host - sem corrida.");
				return new LoopbackUdpTransport(portA, portB, udpClient);
			}
			Plugin.Log.LogWarning("[UDP] eu era HOST na sessão anterior, mas a porta A já está ocupada agora - tentando a corrida normal.");
		}
		else if (text == "Client")
		{
			UdpClient udpClient2 = TryBind(portB);
			if (udpClient2 != null)
			{
				role = NetRole.Client;
				Plugin.Log.LogInfo("[UDP] papel preferido (sessão anterior) = Client - sem corrida.");
				return new LoopbackUdpTransport(portB, portA, udpClient2);
			}
			Plugin.Log.LogWarning("[UDP] eu era CLIENTE na sessão anterior, mas a porta B já está ocupada agora - tentando a corrida normal.");
		}
		UdpClient udpClient3 = TryBind(portA);
		if (udpClient3 != null)
		{
			role = NetRole.Host;
			PersistPreferredRole("Host");
			return new LoopbackUdpTransport(portA, portB, udpClient3);
		}
		UdpClient udpClient4 = TryBind(portB);
		if (udpClient4 != null)
		{
			role = NetRole.Client;
			PersistPreferredRole("Client");
			return new LoopbackUdpTransport(portB, portA, udpClient4);
		}
		role = NetRole.Host;
		Plugin.Log.LogError($"[UDP] portas {portA} e {portB} ocupadas — não deu para auto-conectar.");
		return null;
	}

	private static UdpClient TryBind(int port)
	{
		try
		{
			return new UdpClient(port);
		}
		catch (SocketException)
		{
			return null;
		}
	}

	private static string ReadPreferredRole()
	{
		try
		{
			string preferredRoleFilePath = PreferredRoleFilePath;
			return File.Exists(preferredRoleFilePath) ? File.ReadAllText(preferredRoleFilePath).Trim() : null;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[UDP] falha lendo papel preferido: " + ex.GetType().Name + ": " + ex.Message + " - corrida normal desta vez.");
			return null;
		}
	}

	private static void PersistPreferredRole(string role)
	{
		try
		{
			File.WriteAllText(PreferredRoleFilePath, role);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[UDP] falha gravando papel preferido: " + ex.GetType().Name + ": " + ex.Message + " - vai correr de novo na próxima sessão (inofensivo, só perde o determinismo).");
		}
	}

	public void Start(NetRole role)
	{
		if (_udp == null)
		{
			_udp = new UdpClient(_localPort);
		}
		_running = true;
		_rx = new Thread(RxLoop)
		{
			IsBackground = true,
			Name = "CoopUdpRx"
		};
		_rx.Start();
		Connected = true;
		string arg = ((_remote != null) ? _remote.ToString() : "(aprendendo com o 1º pacote)");
		Plugin.Log.LogInfo($"[UDP] escutando :{_localPort} → peer {arg} (role={role})");
	}

	private static bool IsHighFrequency(byte type)
	{
		if (type != 2 && type != 32)
		{
			return type == 92;
		}
		return true;
	}

	private void RxLoop()
	{
		IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
		while (_running)
		{
			try
			{
				byte[] array = _udp.Receive(ref remoteEP);
				if (_remote == null)
				{
					_remote = new IPEndPoint(remoteEP.Address, remoteEP.Port);
					Plugin.Log.LogInfo($"[UDP][LAN] peer aprendido: {_remote} (1º pacote recebido).");
				}
				if (array.Length != 0 && !IsHighFrequency(array[0]))
				{
					Plugin.Log.LogInfo($"[UDP][diag] recebido tipo=0x{array[0]:X2} tamanho={array.Length}");
				}
				_inbox.Enqueue(array);
			}
			catch (SocketException)
			{
			}
			catch (ObjectDisposedException)
			{
				break;
			}
		}
	}

	public void Send(byte[] data, bool reliable)
	{
		if (_udp == null || _remote == null)
		{
			return;
		}
		if (data.Length != 0 && !IsHighFrequency(data[0]))
		{
			Plugin.Log.LogInfo($"[UDP][diag] enviando tipo=0x{data[0]:X2} tamanho={data.Length} reliable={reliable}");
		}
		try
		{
			_udp.Send(data, data.Length, _remote);
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[UDP] send falhou: " + ex.Message);
		}
	}

	public bool TryReceive(out byte[] data)
	{
		return _inbox.TryDequeue(out data);
	}

	public void Pump()
	{
	}

	public void Stop()
	{
		_running = false;
		Connected = false;
		try
		{
			_udp?.Close();
		}
		catch
		{
		}
		_udp = null;
	}
}
