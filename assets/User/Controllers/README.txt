Controllers — user overrides
=============================

Files here override the shipped Defaults/Controllers/{Platform}.xml definitions.

When to use: if your emulator's default button assignments for a platform differ
from the shipped mapping, copy the relevant platform file from Defaults/Controllers/
here and edit the <Mapping> entries to match your emulator's configuration.

File naming: {Platform}.xml
  The platform name must match LaunchBox's platform name exactly.

XML structure
-------------
  <Controllers>

      <!-- Root-level <Mapping> entries form a shared baseline inherited by
           every <Controller> variant in this file. -->

      <Mapping name="A" input="ButtonX" />
      <Mapping name="B" input="ButtonA" />

      <!-- A <Controller> defines a named variant (e.g. a 3-button vs 6-button
           pad). The variant with default="true" is used unless a game's input
           mapping selects a different one.
           analogToDigital="left" mirrors Dpad platform buttons onto the left
           stick generic inputs so both work for directional labels. -->

      <Controller name="6-Button" analogToDigital="left" default="true">
          <!-- Mappings here extend or override the root-level baseline. -->
          <Mapping name="X" input="ButtonLeftShoulder" />
      </Controller>

      <!-- A variant with no nested mappings just inherits the root baseline. -->
      <Controller name="3-Button" analogToDigital="left" />

  </Controllers>

Attributes
----------
  Mapping/@name     Platform button name (matches element names in label files).
  Mapping/@input    Generic input name (ButtonA, ButtonX, AxisLeftStick, …).
  Controller/@name  Variant name — referenced by input mapping files.
  Controller/@default="true"  Marks the variant used when no game-specific
                    selection is made.
  Controller/@analogToDigital="left"|"right"  Mirrors Dpad platform buttons
                    onto the named stick's generic inputs.

Example:
  User/Controllers/Sega Genesis.xml
