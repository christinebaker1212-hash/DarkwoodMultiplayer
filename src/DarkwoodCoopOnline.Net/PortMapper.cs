using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using BepInEx;

namespace DarkwoodCoopOnline.Net;

// Opens the host's router to the internet automatically (UPnP IGD, or NAT-PMP), finds the home's
// public address and turns it into an invite code. The mapping has a lease and is recorded in a file,
// so a crash can't leave it open for long. Runs on a background thread; the game never waits on it.
// (A port of Moemon Emerald Kai Co-op's mp_portmap.c.)
public static class PortMapper
{
	public enum State
	{
		Idle,
		Working,
		Open,
		Failed
	}

	public enum Error
	{
		None,
		NoRouter,
		Refused,
		Cgnat
	}

	private const int LeaseSeconds = 7200;

	private const int RenewSeconds = 1800;

	private const string Description = "Darkwood Co-op";

	private static volatile State _state;

	private static volatile Error _error;

	private static volatile bool _stop;

	private static int _internalPort;

	private static int _externalPort;

	private static int _lease;

	private static string _method = "";

	private static uint _publicIp;

	private static string _controlUrl;

	private static string _service;

	private static string _localIp;

	private static uint _gateway;

	private static Thread _thread;

	public static State Status => _state;

	public static Error LastError => _error;

	public static string Method => _method;

	public static string Code { get; private set; } = "";


	public static string PublicAddress { get; private set; } = "";


	public static int ExternalPort => _externalPort;

	private static string RecordFile => Path.Combine(Paths.ConfigPath, "DarkwoodCoopOnline_portmap.txt");

	public static void Open(int internalPort)
	{
		if (_state == State.Working || _state == State.Open)
		{
			return;
		}
		_internalPort = internalPort;
		_stop = false;
		_state = State.Working;
		_error = Error.None;
		Code = "";
		PublicAddress = "";
		_thread = new Thread(Work)
		{
			IsBackground = true,
			Name = "CoopPortMap"
		};
		_thread.Start();
	}

	public static void Close()
	{
		_stop = true;
		Thread thread = _thread;
		if (thread != null && thread.IsAlive)
		{
			thread.Join(4000);
		}
		_thread = null;
		Code = "";
	}

	private static void Log(string s)
	{
		Plugin.Log?.LogInfo("[PortMap] " + s);
	}

	private static void Work()
	{
		try
		{
			_error = Error.NoRouter;
			_method = "";
			if (!TryUpnp() && (_stop || !TryNatPmp()))
			{
				_state = ((!_stop) ? State.Failed : State.Idle);
				Log("could not open the router (" + _error.ToString() + ").");
				return;
			}
			try
			{
				File.WriteAllText(RecordFile, _method + " " + _externalPort);
			}
			catch
			{
			}
			Log($"opened port {_externalPort} -> {_internalPort} on the router ({_method}, lease {_lease}s)");
			if (_publicIp == 0 || IsNonPublic(_publicIp))
			{
				uint seen;
				if (AskPublicIp(out seen) && !IsNonPublic(seen) && _publicIp == 0)
				{
					_publicIp = seen;
				}
				else
				{
					Log("the router's internet address isn't public: carrier-grade or double NAT.");
					RemoveMapping();
					_method = "";
					_error = Error.Cgnat;
					_state = State.Failed;
					return;
				}
			}
			PublicAddress = InviteCode.IpToString(_publicIp);
			Code = InviteCode.Make(_publicIp, _externalPort);
			_state = State.Open;
			int num = 0;
			while (!_stop)
			{
				Thread.Sleep(1000);
				if (++num >= RenewSeconds)
				{
					num = 0;
					Renew();
				}
			}
			RemoveMapping();
			_state = State.Idle;
		}
		catch (Exception ex)
		{
			Log("error: " + ex.Message);
			_state = State.Failed;
		}
	}

	private static bool IsNonPublic(uint ip)
	{
		if (ip >> 24 != 10 && ip >> 24 != 127 && ip >> 24 != 0 && ip >> 20 != 2753 && ip >> 16 != 49320 && ip >> 22 != 401)
		{
			return ip >> 16 == 43518;
		}
		return true;
	}

	private static void RemoveMapping()
	{
		try
		{
			if (_method == "UPnP")
			{
				UpnpDelete(_externalPort);
			}
			else if (_method == "NAT-PMP")
			{
				PmpMap(_gateway, _internalPort, _externalPort, 0);
			}
			File.Delete(RecordFile);
			Log($"closed port {_externalPort} on the router");
		}
		catch
		{
		}
	}

	private static void Renew()
	{
		if (_method == "UPnP" && _lease > 0)
		{
			int lease = _lease;
			UpnpAdd(_externalPort, _internalPort, ref lease);
		}
		else if (_method == "NAT-PMP")
		{
			PmpMap(_gateway, _internalPort, _externalPort, LeaseSeconds);
		}
	}

	// ---- HTTP / XML helpers ----

	private static string Http(string method, string url, string soapAction, string body, out int status, out string localIp)
	{
		status = 0;
		localIp = null;
		Uri uri = new Uri(url);
		using TcpClient tcpClient = new TcpClient();
		tcpClient.ReceiveTimeout = 3000;
		tcpClient.SendTimeout = 3000;
		IAsyncResult asyncResult = tcpClient.BeginConnect(uri.Host, uri.Port, null, null);
		if (!asyncResult.AsyncWaitHandle.WaitOne(3000))
		{
			return null;
		}
		tcpClient.EndConnect(asyncResult);
		localIp = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Address.ToString();
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append(method).Append(' ').Append(uri.PathAndQuery)
			.Append(" HTTP/1.1\r\n");
		stringBuilder.Append("Host: ").Append(uri.Host).Append(':')
			.Append(uri.Port)
			.Append("\r\n");
		byte[] array = ((body != null) ? Encoding.UTF8.GetBytes(body) : new byte[0]);
		if (soapAction != null)
		{
			stringBuilder.Append("Content-Type: text/xml; charset=\"utf-8\"\r\nSOAPAction: \"").Append(soapAction).Append("\"\r\n");
			stringBuilder.Append("Content-Length: ").Append(array.Length).Append("\r\n");
		}
		stringBuilder.Append("Connection: close\r\n\r\n");
		NetworkStream stream = tcpClient.GetStream();
		byte[] bytes = Encoding.ASCII.GetBytes(stringBuilder.ToString());
		stream.Write(bytes, 0, bytes.Length);
		if (array.Length != 0)
		{
			stream.Write(array, 0, array.Length);
		}
		MemoryStream memoryStream = new MemoryStream();
		byte[] array2 = new byte[8192];
		try
		{
			int count;
			while ((count = stream.Read(array2, 0, array2.Length)) > 0 && memoryStream.Length < 1048576)
			{
				memoryStream.Write(array2, 0, count);
			}
		}
		catch (IOException)
		{
		}
		string @string = Encoding.UTF8.GetString(memoryStream.ToArray());
		Match match = Regex.Match(@string, "^HTTP/\\d\\.\\d (\\d+)");
		if (match.Success)
		{
			status = int.Parse(match.Groups[1].Value);
		}
		return @string;
	}

	private static string Xml(string text, string tag)
	{
		if (text == null)
		{
			return null;
		}
		Match match = Regex.Match(text, "<(?:\\w+:)?" + tag + ">\\s*(.*?)\\s*</(?:\\w+:)?" + tag + ">", RegexOptions.IgnoreCase | RegexOptions.Singleline);
		if (!match.Success)
		{
			return null;
		}
		return match.Groups[1].Value;
	}

	// ---- UPnP ----

	private static List<string> SsdpDiscover()
	{
		List<string> list = new List<string>();
		string[] array = new string[4] { "urn:schemas-upnp-org:device:InternetGatewayDevice:1", "urn:schemas-upnp-org:device:InternetGatewayDevice:2", "urn:schemas-upnp-org:service:WANIPConnection:1", "urn:schemas-upnp-org:service:WANPPPConnection:1" };
		using UdpClient udpClient = new UdpClient(0);
		udpClient.Client.ReceiveTimeout = 400;
		IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
		for (int i = 0; i < 2; i++)
		{
			if (list.Count != 0)
			{
				break;
			}
			string[] array2 = array;
			foreach (string text in array2)
			{
				byte[] bytes = Encoding.ASCII.GetBytes("M-SEARCH * HTTP/1.1\r\nHOST: 239.255.255.250:1900\r\nMAN: \"ssdp:discover\"\r\nMX: 2\r\nST: " + text + "\r\n\r\n");
				try
				{
					udpClient.Send(bytes, bytes.Length, endPoint);
				}
				catch
				{
				}
			}
			for (int k = 0; k < 6; k++)
			{
				try
				{
					while (true)
					{
						IPEndPoint remoteEP = null;
						string @string = Encoding.ASCII.GetString(udpClient.Receive(ref remoteEP));
						Match match = Regex.Match(@string, "^LOCATION:\\s*(\\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
						if (match.Success && !list.Contains(match.Groups[1].Value))
						{
							list.Add(match.Groups[1].Value);
						}
					}
				}
				catch
				{
				}
				if (list.Count > 0 && k >= 2)
				{
					break;
				}
			}
		}
		return list;
	}

	private static bool LoadIgd(string location)
	{
		int status;
		string localIp;
		string text = Http("GET", location, null, null, out status, out localIp);
		if (status != 200 || text == null)
		{
			return false;
		}
		_localIp = localIp;
		Uri uri = new Uri(location);
		string text2 = Xml(text, "URLBase");
		Uri baseUri = ((!string.IsNullOrEmpty(text2) && Uri.TryCreate(text2, UriKind.Absolute, out Uri result)) ? result : uri);
		foreach (Match item in Regex.Matches(text, "<service>(.*?)</service>", RegexOptions.IgnoreCase | RegexOptions.Singleline))
		{
			string value = item.Groups[1].Value;
			string text3 = Xml(value, "serviceType");
			string text4 = Xml(value, "controlURL");
			if (text3 != null && text4 != null && (text3.Contains("WANIPConnection:") || text3.Contains("WANPPPConnection:")))
			{
				_service = text3;
				_controlUrl = new Uri(baseUri, text4).ToString();
				return true;
			}
		}
		return false;
	}

	private static int Soap(string action, string args, out string reply)
	{
		string body = "<?xml version=\"1.0\"?>\r\n<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\"><s:Body><u:" + action + " xmlns:u=\"" + _service + "\">" + args + "</u:" + action + "></s:Body></s:Envelope>\r\n";
		reply = Http("POST", _controlUrl, _service + "#" + action, body, out var status, out var _);
		if (status != 200)
		{
			int.TryParse(Xml(reply, "errorCode") ?? "0", out var result);
			return -result;
		}
		return status;
	}

	// 1 ok, 0 refused, -1 port taken by another PC
	private static int UpnpAdd(int ext, int intPort, ref int lease)
	{
		for (int i = 0; i < 2; i++)
		{
			string reply;
			int num = Soap("AddPortMapping", $"<NewRemoteHost></NewRemoteHost><NewExternalPort>{ext}</NewExternalPort><NewProtocol>TCP</NewProtocol><NewInternalPort>{intPort}</NewInternalPort><NewInternalClient>{_localIp}</NewInternalClient><NewEnabled>1</NewEnabled><NewPortMappingDescription>{Description}</NewPortMappingDescription><NewLeaseDuration>{lease}</NewLeaseDuration>", out reply);
			if (num == 200)
			{
				return 1;
			}
			if (num == -725 && lease != 0)
			{
				lease = 0;
				continue;
			}
			Log($"UPnP refused port {ext} (error {-num})");
			return (num == -718 || num == -724) ? (-1) : 0;
		}
		return 0;
	}

	private static void UpnpDelete(int ext)
	{
		Soap("DeletePortMapping", $"<NewRemoteHost></NewRemoteHost><NewExternalPort>{ext}</NewExternalPort><NewProtocol>TCP</NewProtocol>", out var _);
	}

	private static bool TryUpnp()
	{
		foreach (string item in SsdpDiscover())
		{
			if (_stop)
			{
				return false;
			}
			try
			{
				if (!LoadIgd(item))
				{
					continue;
				}
				Log("UPnP router: " + _controlUrl);
				try
				{
					if (File.Exists(RecordFile))
					{
						string[] array = File.ReadAllText(RecordFile).Split(' ');
						if (array.Length == 2 && array[0] == "UPnP" && int.TryParse(array[1], out var result))
						{
							UpnpDelete(result);
						}
					}
				}
				catch
				{
				}
				bool flag = true;
				for (int i = 0; i < Proto.PortTries; i++)
				{
					int lease = LeaseSeconds;
					int num = UpnpAdd(Proto.DefaultPort + i, _internalPort, ref lease);
					if (num == 1)
					{
						_externalPort = Proto.DefaultPort + i;
						_lease = lease;
						_method = "UPnP";
						string reply;
						uint ip;
						_publicIp = ((Soap("GetExternalIPAddress", "", out reply) == 200 && InviteCode.TryParseIpv4(Xml(reply, "NewExternalIPAddress"), out ip)) ? ip : 0u);
						return true;
					}
					if (num == 0)
					{
						break;
					}
					flag = false;
				}
				if (flag)
				{
					_error = Error.Refused;
				}
			}
			catch (Exception ex)
			{
				Log("UPnP error: " + ex.Message);
			}
		}
		return false;
	}

	// ---- NAT-PMP (RFC 6886) ----

	private static bool DefaultGateway(out uint gw)
	{
		gw = 0u;
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface networkInterface in allNetworkInterfaces)
			{
				if (networkInterface.OperationalStatus != OperationalStatus.Up)
				{
					continue;
				}
				foreach (GatewayIPAddressInformation gatewayAddress in networkInterface.GetIPProperties().GatewayAddresses)
				{
					if (gatewayAddress.Address.AddressFamily == AddressFamily.InterNetwork && InviteCode.TryParseIpv4(gatewayAddress.Address.ToString(), out gw) && gw != 0)
					{
						return true;
					}
				}
			}
		}
		catch
		{
		}
		// (Mono can't always list gateways) the usual home router: our LAN address with .1 at the end
		string text = LanAddress();
		if (text != null && InviteCode.TryParseIpv4(text, out var ip))
		{
			gw = (ip & 0xFFFFFF00u) | 1u;
			return true;
		}
		return false;
	}

	private static byte[] PmpRequest(uint gw, byte[] req)
	{
		using UdpClient udpClient = new UdpClient(0);
		IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(InviteCode.IpToString(gw)), 5351);
		int num = 250;
		for (int i = 0; i < 4; i++)
		{
			udpClient.Client.ReceiveTimeout = num;
			try
			{
				udpClient.Send(req, req.Length, endPoint);
				IPEndPoint remoteEP = null;
				return udpClient.Receive(ref remoteEP);
			}
			catch
			{
			}
			num *= 2;
		}
		return null;
	}

	private static int PmpMap(uint gw, int intPort, int extPort, int lifetime)
	{
		byte[] array = new byte[12]
		{
			0,
			2,
			0,
			0,
			(byte)(intPort >> 8),
			(byte)intPort,
			(byte)(extPort >> 8),
			(byte)extPort,
			(byte)(lifetime >> 24),
			(byte)(lifetime >> 16),
			(byte)(lifetime >> 8),
			(byte)lifetime
		};
		byte[] array2 = PmpRequest(gw, array);
		if (array2 == null || array2.Length < 16 || array2[1] != 130 || array2[2] != 0 || array2[3] != 0)
		{
			return 0;
		}
		if (lifetime != 0)
		{
			return (array2[10] << 8) | array2[11];
		}
		return 1;
	}

	private static bool TryNatPmp()
	{
		if (!DefaultGateway(out _gateway))
		{
			return false;
		}
		byte[] array = PmpRequest(_gateway, new byte[2]);
		if (array == null || array.Length < 12 || array[1] != 128 || array[2] != 0 || array[3] != 0)
		{
			return false;
		}
		_publicIp = (uint)((array[8] << 24) | (array[9] << 16) | (array[10] << 8) | array[11]);
		Log("NAT-PMP router at " + InviteCode.IpToString(_gateway));
		for (int i = 0; i < Proto.PortTries; i++)
		{
			int num = PmpMap(_gateway, _internalPort, Proto.DefaultPort + i, LeaseSeconds);
			if (num >= Proto.DefaultPort && num < Proto.DefaultPort + Proto.PortTries)
			{
				_externalPort = num;
				_lease = LeaseSeconds;
				_method = "NAT-PMP";
				return true;
			}
			if (num != 0)
			{
				PmpMap(_gateway, _internalPort, num, 0);
			}
		}
		_error = Error.Refused;
		return false;
	}

	// Catches double NAT / carrier-grade NAT the router can't see. Optional (plain HTTP).
	private static bool AskPublicIp(out uint ip)
	{
		ip = 0u;
		try
		{
			int status;
			string localIp;
			string text = Http("GET", "http://api.ipify.org/", null, null, out status, out localIp);
			if (status != 200 || text == null)
			{
				return false;
			}
			int num = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
			return num >= 0 && InviteCode.TryParseIpv4(text.Substring(num + 4).Trim(), out ip);
		}
		catch
		{
			return false;
		}
	}

	// ---- addresses friends can use without the router ----

	// Radmin VPN (26.x.x.x) / Hamachi (25.x.x.x): the last resort, like Moemon's
	public static bool FindVpnAddress(out string address, out string name)
	{
		address = null;
		name = null;
		foreach (string item in LocalIpv4s())
		{
			if (item.StartsWith("26."))
			{
				address = item;
				name = "Radmin VPN";
				return true;
			}
			if (item.StartsWith("25."))
			{
				address = item;
				name = "Hamachi";
				return true;
			}
		}
		return false;
	}

	public static string LanAddress()
	{
		foreach (string item in LocalIpv4s())
		{
			if (item.StartsWith("192.168.") || item.StartsWith("10.") || Regex.IsMatch(item, "^172\\.(1[6-9]|2\\d|3[01])\\."))
			{
				return item;
			}
		}
		return null;
	}

	public static List<string> LocalIpv4s()
	{
		List<string> list = new List<string>();
		try
		{
			NetworkInterface[] allNetworkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (NetworkInterface networkInterface in allNetworkInterfaces)
			{
				if (networkInterface.OperationalStatus != OperationalStatus.Up || networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
				{
					continue;
				}
				foreach (UnicastIPAddressInformation unicastAddress in networkInterface.GetIPProperties().UnicastAddresses)
				{
					if (unicastAddress.Address.AddressFamily == AddressFamily.InterNetwork)
					{
						list.Add(unicastAddress.Address.ToString());
					}
				}
			}
		}
		catch
		{
		}
		return list;
	}
}
