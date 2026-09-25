Platforms — user overrides
===========================

Each subfolder is a platform (the name must match LaunchBox's platform name
exactly) and can hold any combination of:

  Controllers.xml                    Override a platform's controller button definitions.
  Labels.xml                         Add or override game and platform-default labels.
  ControllerOverrides/{Game}.xml     Override or add a per-game input mapping.

Example layout:

  User/Platforms/Sega Genesis/Controllers.xml
  User/Platforms/Sega Genesis/Labels.xml
  User/Platforms/Sega Genesis/ControllerOverrides/Aladdin (USA).xml

To override a shipped file, copy it from the matching
Defaults/Platforms/{Platform}/ path here and edit your copy. Controllers.xml
and ControllerOverrides/{Game}.xml are shadowed wholesale — a User copy
replaces the Defaults file entirely. Labels.xml is merged entry-by-entry
instead (see below), because it holds many independent settings.

Controllers.xml
----------------
When to use: if your emulator's default button assignments for a platform
differ from the shipped mapping, copy the platform's
Defaults/Platforms/{Platform}/Controllers.xml here and edit the <Mapping>
entries to match your emulator's configuration.

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

Every mapping a variant uses must be reachable from that variant — either
listed inside it or inherited via inheritFrom. There is no file-wide baseline:
<Mapping> elements placed directly under <Controllers> are ignored.

Sharing one file across platforms: when several platforms in a hardware
family use the same controller, put the full definition in the family's root
platform folder and point the others at it with a root-level inheritFrom:

  <!-- User/Platforms/Nintendo Famicom/Controllers.xml -->
  <Controllers inheritFrom="Nintendo Entertainment System" />

The value is the other platform's name. A file can also add to what it
inherits — list its own <Controller> elements alongside the attribute; a
variant whose name matches a root one replaces it, and a new name is
appended. Both kinds of inheritFrom are transitive, and a cycle or a missing
base is logged to Logs/debug.log rather than failing the load.

Attributes:
  Mapping/@name     Platform button name (matches element names in Labels.xml).
  Mapping/@input    Generic input name (ButtonA, ButtonX, AxisLeftStick, …).
  Controller/@name  Variant name — referenced by ControllerOverrides files.
  Controller/@default="true"  Marks the variant used when no game-specific
                    selection is made.
  Controller/@inheritFrom     Name of another <Controller> whose mappings are
                    prepended before this variant's own.
  Controller/@analogToDigital="left"|"right"  Mirrors Dpad platform buttons
                    onto the named stick's generic inputs. Not inherited —
                    read from each variant's own attribute.
  Controllers/@inheritFrom    Platform name of another platform folder whose
                    variants are pulled in and merged under this file's own.

Labels.xml
----------
Files here add or override button labels shown on the overlay.

  <Labels>
      <Defaults>
          <Input name="Start">Pause</Input>
      </Defaults>

      <Game launchBoxId="1234" romName="Sonic the Hedgehog (USA)">
          <Input name="A">Jump</Input>
          <Input name="B">Spin Dash</Input>
      </Game>
  </Labels>

The <Defaults> block sets labels applied to every game on that platform. If a
game entry defines the same button, the game's value wins.

The launchBoxId attribute is the LaunchBox Games Database ID and is the
primary lookup key — the plugin finds the entry regardless of your ROM's
filename. The romName attribute is a fallback for games without a database ID.

A space-separated name describes an action performed by pressing several
buttons at once, such as <Input name="BUTTON1 BUTTON2">Power Move</Input>. It
labels whichever control your configuration binds to all of those buttons
together, in preference to their individual labels. If nothing fires them
together, it does not appear.

The name attribute is the button as printed on the original hardware (A, B, C
for Sega Genesis; A, B, X, Y, L, R for Super Nintendo; etc.), not the names of
buttons on your Xbox or PlayStation controller. They must match the
Mapping/@name values defined in that platform's Controllers.xml — unrecognised
names are silently ignored.

Merging with the shipped Labels.xml: your file is merged entry-by-entry with
Defaults/Platforms/{Platform}/Labels.xml — a User <Game> entry overrides the
matching Defaults entry entirely (matched first by id, then by name); User
<Defaults> buttons override matching Defaults buttons, and unmentioned
Defaults buttons are kept.

Contributing: if you create labels for a game, please consider submitting
them so they ship as defaults for everyone. Open a pull request or issue at:
https://github.com/tmstedman/launchbox-dynamic-controls

ControllerOverrides/{Game}.xml
-------------------------------
Files here override per-game input mappings for any emulator not handled
automatically (RetroArch and MAME are read automatically when enabled).

When to use: if a specific game remaps buttons or uses a non-default
controller variant and your emulator is not RetroArch or MAME.

File naming: ControllerOverrides/{Game}.xml — the game name must match
LaunchBox's ROM name exactly.

A per-game file selects a controller variant and overlays button overrides on
top of that variant's baseline. Buttons not mentioned in the file are
preserved unchanged from the baseline.

  <GameMapping controller="3-Button">

      <!-- Override a specific button's generic input -->
      <Mapping name="A" input="ButtonRightShoulder" />

      <!-- Remove a button from the mapping entirely -->
      <Unmap name="C" />

  </GameMapping>

In the example above, <Mapping name="A" ...> replaces whatever generic input
A had in the baseline — the original assignment is unassigned. <Unmap
name="C" /> removes C from the mapping entirely. Multiple <Mapping> entries
with the same name are all applied, mapping that button to each listed
generic input simultaneously (useful to drive two template slots from one
platform button).

Attributes:
  GameMapping/@controller   Controller variant to use as the base (optional;
                            defaults to the platform's default variant). Must
                            match a <Controller name="..."> in that platform's
                            Controllers.xml.
  Mapping/@name             Platform button name to override.
  Mapping/@input            Generic input to assign to that button.
  Unmap/@name               Platform button name to remove from the mapping.

Example:
  User/Platforms/Sega Genesis/ControllerOverrides/Aladdin (USA).xml
