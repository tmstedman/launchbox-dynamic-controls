Shipped per-platform configuration.

Each subfolder is a platform and can hold `Controllers.xml` (controller
variants), `Labels.xml` (game and default button labels), and
`ControllerOverrides/{Game}.xml` (per-game input mapping overrides).

Editing these is encouraged, with one proviso: contribute the change back — a
wrong button mapping, a missing platform, a game with no labels — fix it here,
then open a pull request at
https://github.com/tmstedman/launchbox-dynamic-controls. Once merged it ships
in the next release, so the update that would have wiped your edit delivers
it instead. For changes specific to your own setup, use `User\Platforms\`
instead.

`ControllerOverrides/{Game}.xml` follows the same structure as
`User\Platforms\{Platform}\ControllerOverrides\`. Unlike Labels, most input
mapping configuration is emulator-specific and belongs in
`User\Platforms\{Platform}\ControllerOverrides\` rather than here. Controller
variant selection (the `controller="..."` attribute) is the exception — it is
emulator-independent and can be contributed here.

Arcade platforms such as Sega NAOMI are a likely candidate for shipped
per-game overrides: each game has its own unique button configuration, and
specifying the correct controller variant per game is emulator-independent.
MAME users additionally benefit from automatic JOYCODE translation, but the
variant selection still needs to be correct, and other emulators (Flycast,
Demul) rely on it entirely.

To add your own per-game overrides, use `User\Platforms\{Platform}\ControllerOverrides\` instead.
