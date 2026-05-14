InputMappings — user overrides
================================

Files here override per-game input mappings for any emulator not handled
automatically (RetroArch and MAME are read automatically when enabled).

When to use: if a specific game remaps buttons or uses a non-default controller
variant and your emulator is not RetroArch or MAME.

File naming: {Platform}/{Game}.xml
  Platform and game names must match LaunchBox's values exactly.

XML structure
-------------
A per-game file selects a controller variant and overlays button overrides on top
of that variant's baseline. Buttons not mentioned in the file are preserved
unchanged from the baseline.

  <GameMapping controller="3-Button">

      <!-- Override a specific button's generic input -->
      <Mapping name="A" input="ButtonRightShoulder" />

      <!-- Remove a button from the mapping entirely -->
      <Unmap name="C" />

  </GameMapping>

In the example above, <Mapping name="A" ...> replaces whatever generic input A
had in the baseline — the original assignment is unassigned. <Unmap name="C" />
removes C from the mapping entirely. Multiple <Mapping> entries with the same
name are all applied, mapping that button to each listed generic input
simultaneously (useful to drive two template slots from one platform button).

Attributes
----------
  GameMapping/@controller   Controller variant to use as the base (optional;
                            defaults to the platform's default variant). Must
                            match a <Controller name="..."> in
                            Controllers\{Platform}.xml.
  Mapping/@name             Platform button name to override.
  Mapping/@input            Generic input to assign to that button.
  Unmap/@name               Platform button name to remove from the mapping.

Example:
  User/InputMappings/Sega Genesis/Aladdin (USA).xml
