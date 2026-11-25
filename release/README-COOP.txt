DARKWOOD CO-OP ONLINE - 4 PLAYERS  (v0.4.0)
===========================================

Up to 4 players in one Darkwood world. Networking works like Moemon Emerald Kai
Co-op: the host's game is the server, friends join with an invite code.

INSTALL
-------
Unzip everything into the Darkwood folder (next to Darkwood.exe), so that
winhttp.dll, doorstop_config.ini and the BepInEx folder are beside it.
Everyone must have the same version.

HOST A GAME (main menu)
-----------------------
Type your name, a server name, and if you like a password and a welcome note.
Press START HOSTING and pick the save to play. When the world is loaded the game
opens your router by itself and shows an INVITE CODE like K7QM-3XPA. It's copied
already - paste it to your friends. Press F to start once someone is in; others
can still join after you start.

JOIN A GAME (main menu)
-----------------------
  REJOIN <name>          - the last game you joined, nothing to type
  ENTER INVITE CODE OR IP - the code your friend sent (or their IP, e.g. a
                           Radmin VPN 26.x.x.x address), then the password
  Games on your network  - friends on the same Wi-Fi show up by themselves
  Steam friends hosting  - click a name (or use "Join game" in Steam)
The host's world is downloaded from them directly. The first time, you choose
which save slot it goes into.

IN THE GAME
-----------
  F       start the game (host, in the lobby) / hold next to a downed friend to revive
  N       show / hide names above players
  0       co-op window: players, ping, invite code, addresses, leave
  F1      teleport to the nearest friend (if you're stuck)
  F10     leave the game (host: stop hosting)
  ~       debug panel (stuck cutscene / dream fixes)

NO INVITE CODE? (router UPnP off, or CGNAT)
-------------------------------------------
 * Use Radmin VPN (free): host and friends install it, the host makes a network,
   friends join it, then friends type the host's 26.x.x.x address in JOIN.
 * Steam friends can always join through Steam.
 * Windows Firewall: allow Darkwood on Private AND Public networks.
   Ports: TCP 27115 (to 27122), UDP 27116 for the network list.

SETTINGS
--------
BepInEx\config\com.felipe.darkwoodcooponline.cfg
  [Co-op]  PlayerName, ServerName, ServerPassword, WelcomeNote, OpenRouter
  [Debug]  VerboseLog = true only when reporting a bug (send BepInEx\LogOutput.log)

Original 2-player mod by Felipesfx. Darkwood by Acid Wizard Studio.
