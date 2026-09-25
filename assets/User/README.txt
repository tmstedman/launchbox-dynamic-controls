User overrides — your files are never overwritten by updates
=============================================================

Place files here to override the shipped defaults. The plugin checks User/ first
for every file it reads; if a match exists here it wins, otherwise the Defaults/
file is used.

To override a shipped file, copy it from Defaults/ to the same relative path here
and edit your copy. Two exceptions are merged rather than replaced, because each
holds many independent settings:

  GlobalConfig.xml                Merged field by field — include only the
                                  settings you want to change.
  Platforms/{Platform}/Labels.xml Merged entry by entry — your <Game> entries
                                  are laid over the shipped ones, so labelling
                                  one game doesn't discard the shipped labels
                                  for every other game.

An update refreshes these README.txt files, so don't keep notes of your own in
them. Nothing else under User/ is ever touched.

Files
-----
  GlobalConfig.xml   Override global plugin settings. Only include the settings
                     you want to change — omitted settings keep their defaults.

Subdirectories
--------------
  Platforms/         Per-platform Controllers.xml, Labels.xml and
                     ControllerOverrides/{Game}.xml — see Platforms/README.txt.
  Static/            Static overlay images that bypass the rendering pipeline entirely.
  Emulators/         Override MAME JOYCODE lookup or RetroArch core variant declarations.

Not here: controls.xml
----------------------
The BYOAC MAME controls database supplies button labels for arcade games. It is
not shipped and it is not layered — put it one level up, next to the Defaults/
and User/ folders:

  ...\LaunchBox\Data\Dynamic Controls\controls.xml

A copy inside User/ is ignored.
