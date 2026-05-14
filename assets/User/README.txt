User overrides — never overwritten by updates
==============================================

Place files here to override the shipped defaults. The plugin checks User/ first
for every file it reads; if a match exists here it wins, otherwise the Defaults/
file is used.

To override a shipped file, copy it from Defaults/ to the same relative path here
and edit your copy. Exception: GlobalConfig.xml is merged field-by-field, so you
only need to include the settings you want to change.

Files
-----
  GlobalConfig.xml   Override global plugin settings. Only include the settings
                     you want to change — omitted settings keep their defaults.
  controls.xml       Override the BYOAC MAME controls database used to supply
                     button labels for arcade games. Copy from Defaults/ and edit,
                     or replace with a newer version of the database.

Subdirectories
--------------
  Controllers/       Override a platform's controller button definitions.
  InputMappings/     Override or add per-game input mappings.
  Labels/            Add or override game and platform-default labels.
  Static/            Static overlay images that bypass the rendering pipeline entirely.
  Emulators/         Override MAME JOYCODE lookup or RetroArch core variant declarations.
