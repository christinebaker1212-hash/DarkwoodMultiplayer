DARKWOOD CO-OP ONLINE - 4 PLAYERS  (v0.4.9)
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
shows an INVITE CODE like K7QM-3XPA. It's copied already - paste it to your
friends. No router setup and no Radmin needed. Press F to start once someone is in; others
can still join after you start.

JOIN A GAME (main menu)
-----------------------
  REJOIN <name>          - the last game you joined, nothing to type
  ENTER INVITE CODE OR IP - the code your friend sent, then the password
  Games on your network  - friends on the same Wi-Fi show up by themselves
  Steam friends hosting  - click a name (or use "Join game" in Steam)
The host's world is downloaded from them directly. The first time, you choose
which save slot it goes into.

IN THE GAME
-----------
  F       start the game (host, in the lobby)
  hold F  next to a downed friend to revive them (3.5 s). On a controller: hold
          A (Cross on PlayStation) or the game's interact button. When someone
          is down the top of the screen says so, and a marker shows where they
          are. Change the controller button: ReviveControllerButton in the config.
  N       player names on / off (also in the CO-OP box of the pause menu)
  Tab     hold to see the co-op info (players, ping, invite code); it also
          shows in the pause menu
  0       co-op window: players, ping, invite code, addresses, leave
  F1      teleport to the nearest friend (if you're stuck)
  F10     leave the game (host: stop hosting)
  ~       debug panel (stuck cutscene / dream fixes)
  F5      export the character's sprite sheets for making custom skins (see below)
  F6      switch to the next skin in CoopSkins (and back to the normal look)

HOW THE CODE WORKS
------------------
The game asks your router to open its port (UPnP), like Moemon Kai Co-op. If the
router won't, the code is made through Steam instead and friends connect through
Steam's relay - it works behind any router. Same kind of code, typed the same way.
 * Windows may ask if Darkwood can use the network when you first host: Allow.
 * The co-op window (0) has a spare Steam code if the main one doesn't connect.
 * Non-Steam copies (GOG): router code, the network list, or a VPN address.

CUSTOM CHARACTER SKINS - STEP 1: EXPORT
---------------------------------------
Only one person needs to do this. Load into a world, stand somewhere safe and
press F5. After a few seconds a folder opens: CoopSkins\export in the Darkwood
folder. It holds every sprite sheet the player character uses (every weapon and
item) plus frames.json, the map of which frame is where. Zip that whole export
folder and send it to whoever is making the skins.
Change or turn off the key: [Skins] ExportKey in the config.

CUSTOM CHARACTER SKINS - STEP 2: WEAR ONE
-----------------------------------------
A skin is a folder in CoopSkins (for example CoopSkins\bunny) with repainted
copies of the exported sheets, same file names. Everyone puts the same skin
folders in their CoopSkins. In game, F6 switches your own skin; everyone else
sees it on you. Friends without that folder just see you normally.

SETTINGS
--------
BepInEx\config\com.felipe.darkwoodcooponline.cfg
  [Co-op]  PlayerName, ServerName, ServerPassword, WelcomeNote, OpenRouter
  [Skins]  ExportKey (F5), CycleKey (F6), MySkin (your skin's folder name)
  [Debug]  VerboseLog = true only when reporting a bug (send BepInEx\LogOutput.log)

Original 2-player mod by Felipesfx. Darkwood by Acid Wizard Studio.
