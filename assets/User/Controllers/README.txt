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

      <!-- A <Controller> defines a named variant (e.g. a 3-button vs 6-button
           pad). The variant with default="true" is used unless a game's input
           mapping selects a different one.
           analogToDigital="left" mirrors Dpad platform buttons onto the left
           stick generic inputs so both work for directional labels. -->

      <Controller name="3-Button" analogToDigital="left" default="true">
          <Mapping name="A" input="ButtonX" />
          <Mapping name="B" input="ButtonA" />
          <Mapping name="C" input="ButtonB" />
      </Controller>

      <!-- inheritFrom prepends another variant's mappings before this one's, so
           a variant that only adds buttons doesn't restate the shared ones. A
           mapping here overrides an inherited one with the same name. -->

      <Controller name="6-Button" analogToDigital="left" inheritFrom="3-Button">
          <Mapping name="X" input="ButtonLeftShoulder" />
          <Mapping name="Y" input="ButtonY" />
          <Mapping name="Z" input="ButtonRightShoulder" />
      </Controller>

  </Controllers>

Every mapping a variant uses must be reachable from that variant — either listed
inside it or inherited via inheritFrom. There is no file-wide baseline: <Mapping>
elements placed directly under <Controllers> are ignored.

Sharing one file across platforms
---------------------------------
When several platforms in a hardware family use the same controller, put the full
definition in the family's root platform file and point the others at it with a
root-level inheritFrom:

  <!-- User/Controllers/Nintendo Famicom.xml -->
  <Controllers inheritFrom="Nintendo Entertainment System" />

The value is the other file's platform name, without the .xml. A file can also
add to what it inherits — list its own <Controller> elements alongside the
attribute; a variant whose name matches a root one replaces it, and a new name is
appended. Both kinds of inheritFrom are transitive, and a cycle or a missing base
is logged to Logs/debug.log rather than failing the load.

Attributes
----------
  Mapping/@name     Platform button name (matches element names in label files).
  Mapping/@input    Generic input name (ButtonA, ButtonX, AxisLeftStick, …).
  Controller/@name  Variant name — referenced by input mapping files.
  Controller/@default="true"  Marks the variant used when no game-specific
                    selection is made.
  Controller/@inheritFrom     Name of another <Controller> whose mappings are
                    prepended before this variant's own.
  Controller/@analogToDigital="left"|"right"  Mirrors Dpad platform buttons
                    onto the named stick's generic inputs. Not inherited — read
                    from each variant's own attribute.
  Controllers/@inheritFrom    Platform name of another Controllers file whose
                    variants are pulled in and merged under this file's own.

Example:
  User/Controllers/Sega Genesis.xml
