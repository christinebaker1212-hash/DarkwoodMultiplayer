using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using DarkwoodCoopOnline.Net;

namespace DarkwoodCoopOnline
{
	public class FakeLog { public void LogInfo(object o) => Console.WriteLine("  [log] " + o); public void LogWarning(object o) => Console.WriteLine("  [warn] " + o); public void LogError(object o) => Console.WriteLine("  [err] " + o); }
	public static class Plugin { public static FakeLog Log = new FakeLog(); }
}

static class T
{
	static int fails;
	static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }

	class Cl { public RelayClient C = new RelayClient(); public List<byte[]> Got = new List<byte[]>(); int _slot = -1; public int Slot { get { Drain(); return _slot; } }
		public void Drain() { while (C.Inbox.TryDequeue(out var f)) { Got.Add(f); if (f[0] == Proto.S_JOINED) _slot = f[1]; } }
		public byte[] Last(byte type) { Drain(); return Got.LastOrDefault(f => f[0] == type); }
		public List<byte[]> All(byte type) { Drain(); return Got.Where(f => f[0] == type).ToList(); } }

	static Cl Connect(int port)
	{
		var cl = new Cl(); var done = new ManualResetEventSlim(); string err = null;
		TcpLink.ConnectAsync("127.0.0.1", port, 3000, (l, f) => cl.C.OnFrame(f), l => cl.C.CloseReason = "closed", (l, e) => { if (l != null) cl.C.Attach(l); err = e; done.Set(); });
		done.Wait(5000); if (err != null) throw new Exception(err); return cl;
	}
	static void Wait(int ms = 150) => Thread.Sleep(ms);

	static int Main()
	{
		// invite codes: round trip, typo tolerance, checksum
		foreach (var (ip, cport) in new[] { (0x5DB8D822u, 27115), (0x01020304u, 27122), (0xC0A80001u, 27118) })
		{
			var code = InviteCode.Make(ip, cport);
			Check(InviteCode.TryParse(code, out var ip2, out var p2) && ip2 == ip && p2 == cport, $"invite code {code} round trip ({InviteCode.IpToString(ip)}:{cport})");
			Check(InviteCode.TryParse(code.ToLower().Replace('-', ' ').Replace('1', 'l').Replace('0', 'o'), out ip2, out _) && ip2 == ip, $"invite code {code} typed loosely");
			var bad = code.Substring(0, 8) + (code[8] == 'Z' ? 'Y' : 'Z');
			Check(!InviteCode.TryParse(bad, out _, out _), $"invite code {bad} rejected (checksum)");
		}
		Check(InviteCode.TryResolve("26.1.2.3", out var h, out var pp) && h == "26.1.2.3" && pp == Proto.DefaultPort, "resolve plain IP");
		Check(InviteCode.TryResolve("myhost.example:30000", out h, out pp) && h == "myhost.example" && pp == 30000, "resolve host:port");

		var srv = new RelayServer();
		Check(srv.Start("Night Crew", "secret", "Host", "local:host", out var e), "server starts " + e);
		int port = srv.Port;

		// wrong password, then right one
		var a = Connect(port); Wait();
		var info = a.Last(Proto.S_ROOM_INFO);
		Check(info != null, "room info on connect");
		var ir = new MsgReader(info); ir.U16(); var rname = ir.Str(); var locked = ir.U8();
		Check(rname == "Night Crew" && locked == 1, "room info: name + locked");
		a.C.Join(Proto.RoomKey("Night Crew", "nope"), "Ann", "local:ann"); Wait();
		var rej = a.Last(Proto.S_REJECT);
		Check(rej != null && rej[1] == (byte)Proto.Reject.BadPassword, "wrong password rejected");
		a.C.Join(Proto.RoomKey("night crew ", "secret"), "Ann", "local:ann"); Wait();
		Check(a.Slot == 1, "right password (case-insensitive name) joins as slot 1");
		Check(a.All(Proto.S_PLAYER).Any(f => f[1] == 0), "joiner hears about the host");

		// save transfer while at the menu
		a.C.RequestSave(); Wait();
		RelayServer.Ev ev; int saveSlot = -1;
		while (srv.Events.TryDequeue(out ev)) if (ev.Kind == RelayServer.EvKind.SaveRequest) saveSlot = ev.Slot;
		Check(saveSlot == 1, "host gets the save request");
		var blob = new byte[200_000]; new Random(5).NextBytes(blob);
		srv.SendSave(1, blob); Wait(300);
		var got = new byte[blob.Length]; int have = 0;
		foreach (var f in a.All(Proto.S_SAVE)) { var r = new MsgReader(f); var total = r.U32(); var off = r.U32(); var d = r.Rest(); Buffer.BlockCopy(d, 0, got, (int)off, d.Length); have += d.Length; }
		Check(have == blob.Length && got.SequenceEqual(blob), "save arrives intact in chunks");

		// not in game yet: relays/state are not forwarded
		srv.SendGame(-1, new byte[] { 2, 9, 9 }, false); Wait();
		Check(a.All(Proto.S_RELAY).Count == 0, "no game traffic before HELLO");
		srv.SendState(new byte[] { 2, 1, 1, 1 });
		a.C.Hello(); Wait();
		Check(a.All(Proto.S_STATE).Any(f => f[1] == 0 && f[2] == 2 && f[3] == 1), "HELLO delivers the host's cached state");

		// two more players; a fifth is refused
		var b = Connect(port); Wait(); b.C.Join(Proto.RoomKey("Night Crew", "secret"), "Bo", "local:bo"); Wait();
		var c = Connect(port); Wait(); c.C.Join(Proto.RoomKey("Night Crew", "secret"), "Cy", "local:cy"); Wait();
		Check(b.Slot == 2 && c.Slot == 3, "players 3 and 4 get slots 2 and 3");
		var d5 = Connect(port); Wait(); d5.C.Join(Proto.RoomKey("Night Crew", "secret"), "Di", "local:di"); Wait(300);
		var rej5 = d5.Last(Proto.S_REJECT);
		Check(rej5 != null && rej5[1] == (byte)Proto.Reject.Full, "5th player refused: full");
		Check(srv.PlayerCount == 4, "4 players in the game");

		a.C.SendState(new byte[] { 2, 7, 7 }); Wait();
		b.C.Hello(); Wait();
		Check(b.All(Proto.S_STATE).Any(f => f[1] == 1 && f[3] == 7), "late joiner sees an earlier player's cached state");
		c.C.Hello(); Wait();

		// relay targeting
		b.C.SendGame(-1, new byte[] { 40, 1 }, false); Wait();
		Check(a.All(Proto.S_RELAY).Any(f => f[1] == 2 && f[2] == 40) && c.All(Proto.S_RELAY).Any(f => f[1] == 2 && f[2] == 40), "broadcast reaches every other player, tagged with the sender");
		bool hostGot = false; while (srv.Events.TryDequeue(out ev)) if (ev.Kind == RelayServer.EvKind.Game && ev.Slot == 2 && ev.Payload[0] == 40) hostGot = true;
		Check(hostGot, "broadcast reaches the host");
		Check(!b.All(Proto.S_RELAY).Any(f => f[2] == 40), "sender doesn't get its own broadcast");
		c.C.SendGame(1, new byte[] { 41 }, false); Wait();
		Check(a.All(Proto.S_RELAY).Any(f => f[1] == 3 && f[2] == 41) && !b.All(Proto.S_RELAY).Any(f => f[2] == 41), "targeted relay reaches only its target");
		srv.SendGame(2, new byte[] { 42 }, false); Wait();
		Check(b.All(Proto.S_RELAY).Any(f => f[1] == 0 && f[2] == 42) && !a.All(Proto.S_RELAY).Any(f => f[2] == 42), "host -> one player");

		// coalescing: a burst of droppable positions doesn't queue up stale ones
		for (int i = 0; i < 2000; i++) a.C.SendState(new byte[] { 2, (byte)(i & 255), (byte)(i >> 8), 0 });
		Wait(400);
		var states = b.All(Proto.S_STATE).Where(f => f[1] == 1).ToList();
		var lastS = states.Last();
		Check(lastS[3] + (lastS[4] << 8) == 1999, $"newest position arrives last (got {states.Count} of 2000 bursts forwarded)");

		// leave
		c.C.Leave(); Wait(700);
		Check(a.All(Proto.S_LEAVE).Any(f => f[1] == 3), "others are told when a player leaves");
		bool left = false; while (srv.Events.TryDequeue(out ev)) if (ev.Kind == RelayServer.EvKind.Left && ev.Slot == 3) left = true;
		Check(left && srv.PlayerCount == 3, "host sees the leave; slot freed");
		var c2 = Connect(port); Wait(); c2.C.Join(Proto.RoomKey("Night Crew", "secret"), "Cy", "local:cy"); Wait();
		Check(c2.Slot == 3, "rejoin takes the free slot");

		// ping
		a.C.Tick(2f); Wait(300);
		Check(a.C.RttMs >= 0, $"ping answered ({a.C.RttMs} ms)");

		// LAN discovery
		LanSearch.Start(); Wait(1200);
		var games = LanSearch.Games; LanSearch.Stop();
		Check(games.Any(g => g.Name == "Night Crew" && g.Port == port && g.Locked && g.Players == 4), $"LAN search finds the game ({games.Count} found)");

		srv.Stop(); Wait(800);
		var bye = a.Last(Proto.S_REJECT);
		Check(bye != null && bye[1] == (byte)Proto.Reject.HostLeft, "host closing tells players");
		Console.WriteLine(fails == 0 ? "ALL PASSED" : $"{fails} FAILED");
		return fails == 0 ? 0 : 1;
	}
}
