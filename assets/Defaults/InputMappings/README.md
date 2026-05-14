Per-game input mapping defaults shipped with the plugin.

Files here follow the same structure as `User\InputMappings\` and are used as
defaults when no user override exists for a given game. Unlike Labels, most
input mapping configuration is emulator-specific and belongs in `User\InputMappings\`
rather than here. Controller variant selection (the `controller="..."` attribute)
is the exception — it is emulator-independent and can be contributed here.

Arcade platforms such as Sega NAOMI are a likely candidate for shipped defaults:
each game has its own unique button configuration, and specifying the correct
controller variant per game is emulator-independent. MAME users additionally
benefit from automatic JOYCODE translation, but the variant selection still
needs to be correct, and other emulators (Flycast, Demul) rely on it entirely.

To add your own per-game mappings, use `User\InputMappings\` instead.
