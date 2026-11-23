Dynamic Controls — Plugin Data Layout
=====================================

The "Dynamic Controls" folder holds everything the plugin reads and writes,
grouped by purpose:

  Defaults/      Shipped data — replaced wholesale on every update.
  User/          Your overrides — never touched by updates.
  Templates/     Controller overlay templates (PNG button images + Layout.xml).
  Logs/          Plugin diagnostic output.

For each file the plugin reads, it checks User/ first. If a matching file exists
there it wins; otherwise the shipped Defaults/ file is used. GlobalConfig.xml and
each platform's Labels.xml and Controllers.xml are the exceptions: your copy is
merged over the shipped one instead (see User/README.txt).

Defaults/  (shipped — replaced on every update)
----------------------------------------------------------
Editing these is encouraged, with one proviso: contribute the change back. A
wrong button mapping, a missing platform, a game with no labels — fix it here,
then open a pull request at

  https://github.com/tmstedman/launchbox-dynamic-controls

Once merged it ships in the next release, so the update that would have wiped
your edit delivers it instead. An edit that stays on your machine is one you
lose the next time you update. For changes specific to your own setup, use
User/ instead — see the section below.

  GlobalConfig.xml                          Global plugin settings.
  Platforms/{Platform}/Controllers.xml      Controller variants per platform, and per-game
                                            input mappings (<Game> entries; none ship: one
                                            would replace every user's emulator
                                            configuration for that game).
  Platforms/{Platform}/Labels.xml           Per-platform labels (game entries + defaults block).
  controls.xml                              BYOAC MAME controls database.
  Emulators/MAME/JoycodeMapping.xml            JOYCODE -> generic-input lookup for MAME cfg translation.
  Emulators/RetroArch/{CoreDisplayName}.xml Per-core controller variant declarations.

User/  (your overrides — never overwritten)
--------------------------------------------
Place files here with the same relative path as their Defaults/ counterpart to
shadow them. For example:

  GlobalConfig.xml                          Override global settings.
  Platforms/{Platform}/Controllers.xml      Override or add a platform's controllers, or a
                                            game's input mapping.
  Platforms/{Platform}/Labels.xml           Override or add game labels for a platform.
  controls.xml                              Override the MAME controls database.
  Emulators/MAME/JoycodeMapping.xml            Override the JOYCODE mapping.
  Emulators/RetroArch/{CoreDisplayName}.xml Override a core's variant declarations.
  Static/{Platform}/{RomName}.png/.jpg      Static overlay image (skips the rendering pipeline).

Logs/
-----
  debug.log                                 Diagnostic log. Cleared every time Launchbox starts.
