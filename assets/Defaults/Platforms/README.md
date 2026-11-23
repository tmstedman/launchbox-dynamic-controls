Shipped per-platform configuration.

Each subfolder is a platform and holds `Controllers.xml` (controller
variants) and `Labels.xml` (game and default button labels).

Editing these is encouraged, with one proviso: contribute the change back — a
wrong button mapping, a missing platform, a game with no labels — fix it here,
then open a pull request at
https://github.com/tmstedman/launchbox-dynamic-controls. Once merged it ships
in the next release, so the update that would have wiped your edit delivers
it instead. For changes specific to your own setup, use `User\Platforms\`
instead.

No `<Game>` entries ship in `Controllers.xml`, and none should be contributed
here. A game with an entry is read from that entry alone: RetroArch's and
MAME's configuration is not consulted for it, so a shipped entry would switch
off every user's own emulator setup for that game, controller choice
included. A game whose controller variant RetroArch can't tell the plugin is
better served by the platform's default in `Controllers.xml`.

To add your own per-game mappings, put `<Game>` entries in
`User\Platforms\{Platform}\Controllers.xml` instead.
