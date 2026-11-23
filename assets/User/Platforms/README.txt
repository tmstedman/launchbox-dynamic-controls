Platforms — user overrides
===========================

Each subfolder is a platform (the name must match LaunchBox's platform name
exactly) and can hold any combination of:

  Controllers.xml                    Override a platform's controllers, or add a
                                     per-game input mapping.
  Labels.xml                         Add or override game and platform-default labels.

Example layout:

  User/Platforms/Sega Genesis/Controllers.xml
  User/Platforms/Sega Genesis/Labels.xml

Both files are merged with the shipped ones in Defaults/Platforms/{Platform}/
entry by entry (see below), because each holds many independent settings:
write only the entries you want to change.

Controllers.xml
----------------
When to use: if your emulator's default button assignments for a platform
differ from the shipped mapping, copy the controller you use from the
platform's Defaults/Platforms/{Platform}/Controllers.xml into a
Controllers.xml here and edit the <Mapping> entries to match your emulator's
configuration.

Your file is merged over the shipped one: a <Controller> here replaces the
shipped one with the same name, a new name adds a controller, and the shipped
controllers you leave out are kept. The default and inheritFrom attributes on
the root <Controllers> element are the shipped file's unless yours states
them, so copying the default controller here keeps it the default.

  <Controllers default="3-Button">

      <!-- A <Controller> defines a named variant (e.g. a 3-button vs 6-button
           pad). The variant the root names default is used unless a game's
           input mapping selects a different one.
           analogToDigital="left" mirrors Dpad direction buttons onto the left
           stick's directions so both work for directional labels. A button
           declared with <Control> reaches those directions, so the whole stick
           follows from them. -->

      <Controller name="3-Button" analogToDigital="left">
          <Mapping name="A" input="ButtonX" />
          <Mapping name="B" input="ButtonA" />
          <Mapping name="C" input="ButtonB" />
      </Controller>

      <!-- inheritFrom prepends another variant's mappings before this one's, so
           a variant that only adds buttons doesn't restate the shared ones. A
           mapping here overrides an inherited one with the same name, and
           <Unmap name="..."/> drops one. analogToDigital is inherited too;
           analogToDigital="none" switches an inherited mirror off. -->

      <Controller name="6-Button" inheritFrom="3-Button">
          <Mapping name="X" input="ButtonLeftShoulder" />
          <Mapping name="Y" input="ButtonY" />
          <Mapping name="Z" input="ButtonRightShoulder" />
      </Controller>

  </Controllers>

A control that works as a unit — a stick, a D-pad, a C-button cluster — gets an
<Control> naming the buttons it is made of. The control reaches whatever those
buttons reach, so a label written against it describes the whole control:

  <Controller name="Pad">

      <Control name="C-Any" mappings="C-Up C-Down C-Left C-Right" />
      <Mapping name="C-Up"    input="AxisRightStickUp" />
      <Mapping name="C-Down"  input="AxisRightStickDown" />
      <Mapping name="C-Left"  input="AxisRightStickLeft" />
      <Mapping name="C-Right" input="AxisRightStickRight" />

  </Controller>

A control is resolved last — after your emulator's configuration and any
per-game override are read — because an emulator binds directions one at a
time and has no way to name a control as a unit. Pointing the control straight
at a generic input instead (input="AxisRightStick") would fix it at the moment
you wrote the file, and the label would be lost on any game whose emulator
config had moved the directions underneath it.

A control never displaces a direct mapping: if some button drives an input
outright, that button keeps the slot and decides which image is shown. An
control whose buttons are all unmapped reaches nothing and is skipped. Controls
are inherited by inheritFrom alongside mappings, overridden by name.

Every mapping a variant uses must be reachable from that variant — either
listed inside it or inherited via inheritFrom. There is no file-wide baseline:
<Mapping> elements placed directly under <Controllers> are logged and skipped.

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
  Controller/@name  Variant name — referenced by a <Game>'s inheritFrom.
  Controllers/@default  Name of the variant used when no game-specific
                    selection is made. A name that matches no variant is
                    logged and ignored; with none, the first variant is used.
  Controller/@inheritFrom     Name of another <Controller> whose mappings are
                    prepended before this variant's own.
  Control/@name       Platform button name being declared — usable in Labels.xml
                    like any other.
  Control/@mappings   Space-separated platform button names it stands for.
  Controller/@analogToDigital="left"|"right"|"none"  Mirrors Dpad direction
                    buttons onto the named stick's directions. Inherited from
                    the variant's inheritFrom chain when absent; "none"
                    switches an inherited mirror off.
  Unmap/@name       Inherited platform button this variant doesn't have.
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

A label too long for one line can be split across several by writing &#10;
where each new line starts: <Input name="A">Punch&#10;Block</Input>. The
lines are centred on where a one-line label would sit.

Control sets: a game whose controls change with the vehicle, view or mode
can hold a <ControlSet name="..."> for each, labelling every button as it
works there:

      <Game launchBoxId="2418" romName="Pilotwings">
          <Input name="Dpad-Left">Turn Left</Input>
          <ControlSet name="Rocket Belt">
              <Input name="Dpad-Left">Turn Left</Input>
              <Input name="A">Fast Jets Throttle</Input>
          </ControlSet>
      </Game>

The <Input>s directly under <Game> are the game's default, a summary of
every set when it has them, which the pause screen shows only for a game
without sets; with sets it starts on the first one each launch and keeps the
page between pauses, and Left and Right (or Page Up and Page Down, or an
Xbox controller's D-pad Left and Right or bumpers) step through them. A set
takes nothing from the default, so it repeats what it shares; the <Defaults>
block still applies beneath every set. Each set needs a name, unique within
the game.

Merging with the shipped Labels.xml: your file is merged entry-by-entry with
Defaults/Platforms/{Platform}/Labels.xml — a User <Game> entry overrides the
matching Defaults entry entirely (matched first by id, then by name); User
<Defaults> buttons override Defaults entries naming the same buttons (a
combination in any order), and unmentioned Defaults buttons are kept. A game
with control sets is replaced whole too: copy every set you want to keep into
your entry, since one you leave out is removed.

Contributing: if you create labels for a game, please consider submitting
them so they ship as defaults for everyone. Open a pull request or issue at:
https://github.com/tmstedman/launchbox-dynamic-controls

Per-game input mappings: <Game> in Controllers.xml
--------------------------------------------------
A <Game> entry in Controllers.xml overrides one game's input mapping, for any
emulator not handled automatically (RetroArch and MAME are read automatically
when enabled).

When to use: if a specific game remaps buttons or uses a non-default
controller variant and your emulator is not RetroArch or MAME.

Finding the game: an entry is matched by launchBoxId first (the LaunchBox
Games Database ID), then by romName — LaunchBox's ROM name, compared ignoring
case but otherwise exactly, so each region's dump needs its own entry. Either
attribute alone is enough, and your entries are searched before the shipped
ones. A clone doesn't use its parent's entry (unlike its
labels), so a clone's own RetroArch or MAME configuration is still read when
only its parent has one; give the clone an entry of its own to override it
too. The clone does inherit its parent's labels, so a label for a button only
the parent's entry supplies may not be shown for the clone until it has its
own entry.

An entry is written like a <Controller>: inheritFrom names the controller
variant it starts from, and its <Mapping>s are laid over that variant's
baseline. Buttons the entry doesn't mention are preserved unchanged. A button
the entry remaps is drawn like one an emulator has swapped, with the picture
of the physical button. An entry can declare <Control>s too, which replace
its variant's controls of the same name and add new ones. A <Game> here
replaces any shipped entry for the same game whole.

A game with an entry is read from the entry alone: RetroArch's and MAME's
configuration is not consulted for it (with Debug on, Logs\debug.log says so).
An entry that names no controller is read against the platform's default
controller, not the one RetroArch selected.

On Arcade, use bare MAME port names (P1_BUTTON1, COIN1) in the entry and in
that game's labels. The longer TYPE:tag:mask form of a port name comes from
the game's MAME cfg, which is not read once the game has an entry, so a label
written that way stops matching. The cfg is also what adds the ports Arcade's
Controllers.xml doesn't declare (START2, COIN2, SERVICE, P1_BUTTON9 and up,
another player's ports): for a game with an entry, those exist only if the
entry maps them itself.

  <Controllers>

      <Game launchBoxId="1234" romName="Aladdin (USA)" inheritFrom="3-Button">

          <!-- Override a specific button's generic input -->
          <Mapping name="A" input="ButtonRightShoulder" />

          <!-- Remove a button from the mapping entirely -->
          <Unmap name="C" />

      </Game>

  </Controllers>

In the example above, <Mapping name="A" ...> replaces whatever generic input
A had in the baseline — the original assignment is unassigned. <Unmap
name="C" /> removes C from the mapping entirely. Multiple <Mapping> entries
with the same name are all applied, mapping that button to each listed
generic input simultaneously (useful to drive two template slots from one
platform button).

Attributes:
  Game/@launchBoxId         LaunchBox Games Database ID, matched first.
  Game/@romName             ROM name without extension, matched when no ID
                            does.
  Game/@inheritFrom         Controller variant to use as the base (optional;
                            defaults to the platform's default variant). Must
                            match a <Controller name="..."> in that platform's
                            Controllers.xml.
  Game/@analogToDigital     As on <Controller>: inherited from the variant
                            when absent, "none" for no mirroring.
  Mapping/@name             Platform button name to override.
  Mapping/@input            Generic input to assign to that button.
  Unmap/@name               Platform button name to remove from the mapping.
