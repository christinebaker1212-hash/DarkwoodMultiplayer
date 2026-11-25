# Darkwood Co-op Online — 4 players

A BepInEx mod that lets **up to 4 players** play Darkwood together in one world.
It started from *Darkwood Co-op Online 0.3.93* (by Felipesfx, two players) and rebuilds the
networking the way **Moemon Emerald Kai Co-op** does it: the host's game runs a small lobby +
relay server, friends join with an **invite code**, and the whole thing is tuned to feel much less laggy.

## Install

1. Download `DarkwoodCoopOnline-4P-v0.4.0.zip` (in [`release/`](release/)).
2. Unzip it **into the Darkwood game folder** (the one with `Darkwood.exe`), so that `winhttp.dll`,
   `doorstop_config.ini` and the `BepInEx` folder sit next to `Darkwood.exe`.
   If you had the old 2-player mod, just let it overwrite.
3. Every player needs the same version (the game tells you if someone's is older or newer).

## Playing together

On the main menu there are two new entries:

| | |
|---|---|
| **HOST A GAME** | Type your name, a server name and (optionally) a password and a welcome note, then **START HOSTING** and pick the save to play. When the world is loaded the game opens your router by itself and shows an **invite code** like `K7QM-3XPA` — it's copied, just paste it to your friends. Press **F** to start once at least one friend is in; more friends can still join later. |
| **JOIN A GAME** | **REJOIN &lt;last game&gt;** (no typing), **ENTER INVITE CODE OR IP**, games on your home network (listed automatically) and Steam friends who are hosting. The host's world is downloaded straight from them, you pick a save slot for it the first time, and you're in. |

* Up to **4 players**. Friends can join or leave at any time; the host keeps playing.
* Everyone's name is shown above them (**N** hides/shows names). The top of the screen shows who's
  in, your ping, and (for the host) the invite code. Press **0** for the co-op window
  (players, addresses, copy code, leave). **F10** leaves the game.
* Downed instead of dead: when you drop, any other player can **hold F** next to you to revive you.
  If everyone is down — or someone bleeds out — it's a party wipe (everyone's day/clock stays together).
* Proximity voice chat works per player (turn it on in the config / voice settings).
* Steam: friends can also use **Join game** from the Steam friends list.

### When the invite code doesn't work

The game asks your router to forward the port (UPnP / NAT-PMP), like Moemon Kai Co-op does.

* **No invite code** (router has UPnP off, or your provider uses CGNAT): use **Radmin VPN** (free). Host and
  friends install it, the host creates a network, friends join it, then friends type the host's
  Radmin address (`26.x.x.x`, shown in the co-op window) in JOIN. Hamachi (`25.x.x.x`) works too.
  Steam friends can always join through Steam, no ports needed.
* **Windows Firewall** is the usual culprit: the first time you host, allow Darkwood on both
  *Private* and *Public* networks (Windows Security → Firewall → Allow an app through firewall).
* Port used: TCP **27115** (27115–27122 if busy); LAN discovery: UDP 27116.

## What changed compared to the 2-player mod

**4 players (Moemon-style networking)**
* Lobby + relay server inside the host's game (server-assigned slots 0–3), password-protected rooms
  (only a SHA-256 key of name + password is sent), invite codes, router port mapping, LAN discovery,
  REJOIN, Radmin/Hamachi fallback, Steam P2P on the same protocol.
* The host's save is sent over the game connection. The old version uploaded the host's save to a
  public file-sharing website for Steam joins and needed admin rights (HttpListener) for LAN joins; both are gone.
* Every player has their own puppet, name tag, voice channel and downed state; damage, revives,
  sounds, trap chains and messages go to/come from the right player. Pause is shown with who paused.
  Ownership of areas/enemies is per player (lowest slot wins a tie; a leaving player's areas are freed).

**Less lag**
* Reliable, ordered delivery (TCP with no Nagle delay, batched writes). The old LAN mode used raw UDP
  with **no** retransmission, so lost packets meant doors/items/enemies silently out of sync.
* Position-type updates that are already out of date are dropped instead of queuing behind newer ones,
  so a slow moment can't snowball into seconds of delay.
* Routing between clients happens on network threads, not at the host's frame rate.
* Remote players and enemies use snapshot interpolation (~100 ms buffer, short extrapolation, snap on
  teleport) instead of chasing the newest position, so movement is smooth even with jittery packets.
* The old version wrote a log line for every non-movement packet sent and received, and hundreds of
  diagnostic lines during play. Diagnostics are now off by default (`Debug.VerboseLog`), and the
  developer debug hotkeys (some start per-frame log streams) are off unless `Debug.DiagnosticHotkeys` is on.
* Enemy line-of-sight aggro checks run 5×/s instead of 20×/s per player; full-scene object searches on
  incoming interactions come from a cache.

## Settings

`BepInEx/config/com.felipe.darkwoodcooponline.cfg` — section `[Co-op]` (name, server name/password,
welcome note, open router, remembered last game), `[Voice]`, `[Debug]`.

## Known limitations

* Not every system could be play-tested with 4 players here (the code was built and its network layer
  tested automatically, but not inside the game). Please report anything odd with
  `Debug.VerboseLog = true` and the `BepInEx/LogOutput.log`.
* Enemies in an area belong to the player who first loaded that area. If that player leaves, those
  enemies disappear for the others until the area is loaded again.
* A player bleeding out (75 s without a revive) wipes the whole party, as in the 2-player version.

## Building from source

Needs the .NET SDK 8 (Linux, macOS or Windows), `curl` and `unzip`:

```bash
./build.sh
```

The game's own assemblies aren't redistributable, so `tools/StubGen` generates compile-only stubs
(from exactly the members the original mod used) and Unity/BepInEx reference assemblies are
downloaded from NuGet. `tools/RefCheck` compares the game/Unity/Steam API surface of a build against
the original DLL; `tools/NetTests` runs the network layer against real sockets (`dotnet run -c Release`).
`tools/package.sh <BepInEx-pack-dir>` builds the release zip.

## Credits

* Original Darkwood Co-op Online: Felipesfx.
* Networking design: Moemon Emerald Kai Co-op (lobby/relay server, invite codes, port mapping).
* Darkwood by Acid Wizard Studio. BepInEx / HarmonyX by their authors.
