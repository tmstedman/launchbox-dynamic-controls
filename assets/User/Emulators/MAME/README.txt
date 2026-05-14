MAME — user overrides
======================

Files here override the shipped Defaults/Emulators/MAME/ data.

JoycodeMapping.xml
  Maps JOYCODE values (as written in MAME .cfg files) to generic input names.
  Override this if the overlay is not showing the right buttons for your
  controller — for example, when using DirectInput your JOYCODE assignments
  may differ from the shipped mapping.

  To find the JOYCODE values your setup is using, look in MAME's cfg/ folder
  (next to mame.exe). Open cfg/default.cfg or the per-game cfg/{rom}.cfg and
  look for the JOYCODE_... strings assigned to each port — these are the values
  you need to map.
