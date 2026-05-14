Dynamic Controls — Plugin Data Layout
=====================================

The "Dynamic Controls" folder holds everything the plugin reads and writes,
grouped by purpose:

  Defaults/      Shipped data — overwritten on every update. Do not edit.
  User/          Your overrides — never touched by updates.
  Templates/     Controller overlay templates (PNG button images + Layout.xml).
  Logs/          Plugin diagnostic output.

For each file the plugin reads, it checks User/ first. If a matching file exists
there it wins; otherwise the shipped Defaults/ file is used.

Defaults/  (shipped, overwritten on update — do not edit)
----------------------------------------------------------
  GlobalConfig.xml                          Global plugin settings.
  Controllers/{Platform}.xml                Controller variants per platform.
  Labels/{Platform}/_DefaultLabels.xml      Platform-default labels.
  Labels/{Platform}/{RomName}.xml           Per-game labels.
  controls.xml                              BYOAC MAME controls database.
  Emulators/MAME/JoycodeMapping.xml            JOYCODE -> generic-input lookup for MAME cfg translation.
  Emulators/RetroArch/{CoreDisplayName}.xml Per-core controller variant declarations.

User/  (your overrides — never overwritten)
--------------------------------------------
Place files here with the same relative path as their Defaults/ counterpart to
shadow them. For example:

  GlobalConfig.xml                          Override global settings.
  Controllers/{Platform}.xml                Override a platform's controller definitions.
  InputMappings/{Platform}/{Game}.xml       Override a game's input mapping.
  Labels/{Platform}/{RomName}.xml           Override per-game labels.
  controls.xml                              Override the MAME controls database.
  Emulators/MAME/JoycodeMapping.xml            Override the JOYCODE mapping.
  Emulators/RetroArch/{CoreDisplayName}.xml Override a core's variant declarations.
  Static/{Platform}/{RomName}.png/.jpg      Static overlay image (skips the rendering pipeline).

Logs/
-----
  debug.log                                 Diagnostic log. Cleared every time Launchbox starts.
