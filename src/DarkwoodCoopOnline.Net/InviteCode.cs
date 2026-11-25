using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace DarkwoodCoopOnline.Net;

// Invite codes: 7 Crockford base-32 characters and a check character, "K7QM-3XPA" (Moemon Emerald Kai
// Co-op's scheme). 35 bits: 3 bits of "which" + 32 bits of "where".
//   which 0-6: the host's public IPv4 address and game port Proto.DefaultPort + which (router opened)
//   which 7:   the host's Steam account: the game connects through Steam (Valve's relay), so it works
//              behind any router, with nothing to set up - used when the router can't be opened
public static class InviteCode
{
	private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

	public const int SteamMarker = 7;

	private const ulong SteamIdBase = 76561197960265728uL;

	public static string Make(uint ip, int port)
	{
		return Encode(((ulong)((uint)(port - Proto.DefaultPort) & 7u) << 32) | ip);
	}

	public static string MakeSteam(ulong steamId64)
	{
		return Encode(((ulong)SteamMarker << 32) | (uint)(steamId64 & 0xFFFFFFFFu));
	}

	public static bool TryParseSteam(string code, out ulong steamId64)
	{
		steamId64 = 0uL;
		if (!TryDecode(code, out var v) || (int)((v >> 32) & 7) != SteamMarker || (uint)v == 0)
		{
			return false;
		}
		steamId64 = SteamIdBase + (uint)v;
		return true;
	}

	private static string Encode(ulong num)
	{
		char[] array = new char[8];
		for (int num2 = 6; num2 >= 0; num2--)
		{
			array[num2] = Alphabet[(int)(num & 0x1F)];
			num >>= 5;
		}
		int num3 = 0;
		for (int i = 0; i < 7; i++)
		{
			num3 += Alphabet.IndexOf(array[i]) * (i + 3);
		}
		array[7] = Alphabet[num3 % 31];
		return new string(array, 0, 4) + "-" + new string(array, 4, 4);
	}

	private static int Value(char ch)
	{
		ch = char.ToUpperInvariant(ch);
		switch (ch)
		{
		case 'O':
			ch = '0';
			break;
		case 'I':
		case 'L':
			ch = '1';
			break;
		case 'U':
			ch = 'V';
			break;
		}
		return Alphabet.IndexOf(ch);
	}

	public static bool TryParse(string code, out uint ip, out int port)
	{
		ip = 0u;
		port = 0;
		if (!TryDecode(code, out var v) || (int)((v >> 32) & 7) == SteamMarker)
		{
			return false;
		}
		ip = (uint)v;
		port = Proto.DefaultPort + (int)((v >> 32) & 7);
		return ip != 0;
	}

	private static bool TryDecode(string code, out ulong v)
	{
		v = 0uL;
		if (string.IsNullOrEmpty(code))
		{
			return false;
		}
		List<int> list = new List<int>();
		foreach (char c in code)
		{
			if (c == '-' || c == ' ')
			{
				continue;
			}
			int num = Value(c);
			if (num < 0 || list.Count >= 8)
			{
				return false;
			}
			list.Add(num);
		}
		if (list.Count != 8)
		{
			return false;
		}
		ulong num2 = 0uL;
		int num3 = 0;
		for (int j = 0; j < 7; j++)
		{
			num2 = (num2 << 5) | (uint)list[j];
			num3 += list[j] * (j + 3);
		}
		if (list[7] != num3 % 31)
		{
			return false;
		}
		v = num2;
		return true;
	}

	public static string IpToString(uint ip)
	{
		return $"{ip >> 24}.{(ip >> 16) & 0xFF}.{(ip >> 8) & 0xFF}.{ip & 0xFF}";
	}

	public static bool TryParseIpv4(string s, out uint ip)
	{
		ip = 0u;
		if (!IPAddress.TryParse((s ?? "").Trim(), out IPAddress address) || address.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}
		byte[] addressBytes = address.GetAddressBytes();
		ip = (uint)((addressBytes[0] << 24) | (addressBytes[1] << 16) | (addressBytes[2] << 8) | addressBytes[3]);
		return true;
	}

	// What a player typed in JOIN: an invite code, "ip", "ip:port" or "host:port"
	public static bool TryResolve(string input, out string host, out int port)
	{
		host = null;
		port = Proto.DefaultPort;
		input = (input ?? "").Trim().Replace(';', '.').Replace(',', '.');
		if (input.Length == 0 || TryParseSteam(input, out var _))
		{
			return false;
		}
		if (TryParse(input, out var ip, out var port2))
		{
			host = IpToString(ip);
			port = port2;
			return true;
		}
		int num = input.LastIndexOf(':');
		if (num > 0 && input.IndexOf(':') == num && int.TryParse(input.Substring(num + 1), out var result) && result > 0 && result < 65536)
		{
			host = input.Substring(0, num);
			port = result;
			return true;
		}
		if (input.IndexOf(' ') >= 0)
		{
			return false;
		}
		host = input;
		return true;
	}
}
