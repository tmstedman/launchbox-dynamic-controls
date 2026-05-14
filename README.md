# Dynamic Controls

[![CI](https://github.com/tmstedman/launchbox-dynamic-controls/actions/workflows/ci.yml/badge.svg?branch=master)](https://github.com/tmstedman/launchbox-dynamic-controls/actions/workflows/ci.yml)
[![Unit coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/tmstedman/50c7b51e44254e976e548f232d259cfc/raw/unit-coverage.json)](https://github.com/tmstedman/launchbox-dynamic-controls/actions/workflows/ci.yml)
[![Integration coverage](https://img.shields.io/endpoint?url=https://gist.githubusercontent.com/tmstedman/50c7b51e44254e976e548f232d259cfc/raw/integration-coverage.json)](https://github.com/tmstedman/launchbox-dynamic-controls/actions/workflows/ci.yml)

<table>
  <tr>
    <td width="33%"><img src="docs/images/Screenshot%20SFII%20CE.png" alt="Dynamic Controls overlay on Street Fighter 2: Champion Edition" width="100%"></td>
    <td width="33%"><img src="docs/images/Screenshot%20Jak%20II.png" alt="Dynamic Controls overlay on Jak II" width="100%"></td>
    <td width="33%"><img src="docs/images/Screenshot%20Super%20Mario%2064.png" alt="Dynamic Controls overlay on Super Mario 64" width="100%"></td>
  </tr>
</table>

A controller button overlay plugin for LaunchBox and Big Box. Instead of a static image per game, it generates the pause-screen overlay dynamically from configuration - picking the right button art and labels automatically per platform, controller variant, and game.

## Requirements

- **LaunchBox / Big Box 13.3 or newer** on **Windows**, with the pause-screen feature enabled.

## Installation

1. Download `launchbox-dynamic-controls-ALL-<version>.zip` from the [Releases page](https://github.com/tmstedman/launchbox-dynamic-controls/releases).
2. Extract it into your LaunchBox folder (wherever `LaunchBox.exe` lives).
3. In LaunchBox / Big Box's **pause-screen settings**, set the pause theme to **Dynamic Controls**.
4. Restart LaunchBox / Big Box.

> **Updating?** Re-extract over your existing install. Your customizations live under `Data\Dynamic Controls\User\`, which no release zip ever touches - only the shipped `Defaults\` and `Templates\` folders are overwritten.

Individual component zips (plugin, assets, pause theme) are also on the Releases page if you need to update one piece at a time.

## Configuration

All data lives under `…\LaunchBox\Data\Dynamic Controls\`, split into two layers:

- **`Defaults\`** - shipped files, overwritten on every update. Don't edit these.
- **`User\`** - your files, never touched by updates.

To override any shipped file, place a copy at the same relative path under `User\` - it takes precedence automatically. The one exception is `GlobalConfig.xml`: rather than copying the whole file, you only need to include the settings you want to change, and the rest keep their defaults.

### `GlobalConfig.xml`

Create `User\GlobalConfig.xml` to change global settings:

```xml
<Config>
    <DefaultTemplate>Xbox Series X</DefaultTemplate>
    <Debug>false</Debug>
    <EnableMame>true</EnableMame>
    <EnableRetroArch>true</EnableRetroArch>
</Config>
```

| Setting | Default | Meaning |
|---|---|---|
| `DefaultTemplate` | `Xbox Series X` | Controller artwork to draw - a folder name under `Templates\`. |
| `Debug` | `false` | `true` writes a verbose `Logs\debug.log` on every launch. |
| `EnableMame` | `true` | `true` reads your MAME `.cfg` files to pick up game-specific JOYCODE button assignments. |
| `EnableRetroArch` | `true` | `true` reads RetroArch config and remap files so the overlay reflects your game-specific button remaps and active controller type. |

## Input mappings

The plugin works in terms of **platform button names** - the names printed on the original hardware (`A`, `B`, `C` for Sega Genesis; `A`, `B`, `X`, `Y`, `L`, `R` for Super Nintendo; and so on). These are *not* the names of buttons on your Xbox or PlayStation controller.

`Defaults\Controllers\{Platform}.xml` maps each platform button name to a generic input that the controller template knows about. For example, for Sega Genesis:

```xml
<!-- Defaults\Controllers\Sega Genesis.xml (excerpt) -->
<Mapping name="A" input="ButtonX" />
<Mapping name="B" input="ButtonA" />
<Mapping name="C" input="ButtonB" />
```

Generic input names (`ButtonA`, `ButtonB`, `ButtonX`, `ButtonY`, `ButtonLeftShoulder`, `ButtonDpad`, `AxisLeftStick`, …) are the shared vocabulary that connects every part of the plugin. The full list is in [`docs/templates.md`](docs/templates.md#generic-input-names). Controllers.xml maps platform buttons *to* them; RetroArch and MAME integration resolves *to* them; and the controller template defines a slot *for* each one, with a corresponding button image and position. So the Genesis `B` button maps to `ButtonA`, which the template renders at the A-button position — but using the platform-specific `B.png` artwork from the `Sega Genesis` subfolder, so the player sees the original hardware button label rather than the Xbox one. If no platform-specific image exists, it falls back to the generic `ButtonA.png`. This file ships for ~50 platforms.

The overlay can only be accurate if this mapping matches what your emulator is actually doing. The shipped files assume the emulator's default generic button assignments for each platform - if your emulator is configured differently, copy the relevant `Controllers\{Platform}.xml` to `User\Controllers\` and edit it to match your configuration.

### Game-specific overrides

Some games remap buttons or use a different controller variant. The plugin resolves this from the following sources:

1. **XML** (highest priority) - create `User\InputMappings\{Platform}\{Game}.xml` for any emulator not covered below, or to explicitly override automatic detection:

```xml
<!-- User\InputMappings\Sega Genesis\Aladdin (USA).xml -->
<GameMapping controller="3-Button">
    <Mapping name="A" input="ButtonRightShoulder" />
</GameMapping>
```

2. **RetroArch** (`EnableRetroArch=true`) - reads your RetroArch `.cfg` and remap files automatically to detect the active controller type and any per-game button swaps.
3. **MAME** (`EnableMame=true`) - reads your MAME `.cfg` files to pick up per-game JOYCODE button assignments.

## Labels

Labels tell the plugin what each button does in a specific game. Create `User\Labels\{Platform}\{Game}.xml`, where `{Platform}` and `{Game}` match the values in LaunchBox exactly. Use the same platform button names as in Controllers and InputMappings:

```xml
<!-- User\Labels\Sega Genesis\Sonic the Hedgehog (USA).xml -->
<InputLabels>
    <A>Jump</A>
    <B>Spin Dash</B>
    <Start>Pause</Start>
</InputLabels>
```

To set labels that apply across every game on a platform, use `_DefaultLabels.xml`. All entries are merged into game-specific label files, with game-specific entries taking precedence for any button defined in both:

```xml
<!-- User\Labels\Sega Genesis\_DefaultLabels.xml -->
<InputLabels>
    <Start>Pause</Start>
</InputLabels>
```

### MAME controls.xml support

The plugin automatically reads a `controls.xml` database to display what each button does in each game. `controls.xml` is not distributed with the plugin: download the BYOAC MAME controls database and place it at `Data\Dynamic Controls\controls.xml`.

## Platform button images

Templates support platform-specific hardware button art: when a platform subfolder exists inside the template folder, the overlay substitutes those images for the generic ones automatically. The shipped `Xbox Series X` template covers over 40 platforms. See [`docs/templates.md`](docs/templates.md) for the image resolution rules and how to add images for additional platforms or controller variants.

## Troubleshooting

- **No overlay appears.** Confirm `Data\Dynamic Controls\` exists at the right path (not under `Plugins\`), that `DefaultTemplate` names a real folder under `Templates\`, and that the pause screen is enabled in LaunchBox.
- **Plugin doesn't load.** Both `DynamicControls.LaunchBox.dll` and `DynamicControls.Core.dll` must be present in `Plugins\DynamicControls.LaunchBox\`.
- **Windows or antivirus flags the download.** Right-click the zip → **Properties** → tick **Unblock** → **OK**, then re-extract.
- **Dig deeper.** Create `User\GlobalConfig.xml` with `<Config><Debug>true</Debug></Config>` and check `Logs\debug.log` after the next launch.

## Known limitations

- **Game-specific files match on exact game filename.** Labels (`Labels\{Platform}\{Game}.xml`) and per-game input mappings (`InputMappings\{Platform}\{Game}.xml`) are matched against the ROM filename without its extension. Regional variants require their own file. This is a high-priority item to address.
- **DirectInput users in RetroArch do not get button swap detection.** XInput controllers get full game-level swap detection; DirectInput controllers get controller variant and remap file support but no swap detection through cfg files.
- **DirectInput users in MAME do get button swap detection.** However, it is not reliable since DirectInput devices do not adhere to a standard layout.
- **RetroArch button swap detection covers game-level remaps only.** Swaps configured in global, core, or core-remap files are not applied — only game-level remap files are checked. If you configure button swaps at those levels the overlay may not reflect them.
- **RetroArch controller variant detection requires a core definition file.** The plugin can only detect the active controller variant for RetroArch cores that have a shipped `Emulators/RetroArch/{CoreDisplayName}.xml`. Currently only Genesis Plus GX ships, so controller variant detection is a no-op for all other cores unless you add one.

## Contributing

The data files that ship with the plugin - button mappings, labels, templates, and emulator definitions - are the most impactful area for contributions. No C# knowledge required for any of these.

- **Game labels** (`Defaults\Labels\{Platform}\{Game}.xml`) - what each button does in a specific game, keyed by platform button name (the `name` attributes in `Controllers\{Platform}.xml`). The most common contribution: add a labels file for any game that doesn't have one yet. The file name and platform folder must match the title and platform in LaunchBox exactly.
- **Default input mappings** (`Defaults\Controllers\{Platform}.xml`) - how platform buttons map to generic controller slots. Covers ~50 platforms; corrections and new platforms welcome. When adding a new platform, follow the conventions used in the existing files.
- **Platform button images** - PNGs under `Templates\Xbox Series X\{Platform}\`. Styled images for any platform not yet covered in the template, or additional controller variants for existing ones. Images must be styled consistently with the existing platform images.
- **RetroArch device-type IDs** (`Defaults\Emulators\RetroArch\{CoreDisplayName}.xml`) - maps RetroArch's `input_libretro_device` IDs to controller variant names, so the plugin can detect which variant is active. Only one core ships today; every additional core helps.

Open a pull request or issue at [github.com/tmstedman/launchbox-dynamic-controls](https://github.com/tmstedman/launchbox-dynamic-controls).

## Development

Built in two layers: `src/Core` (`net6.0`, platform-neutral logic) and `src/LaunchBox` (`net6.0-windows`, the LaunchBox/WPF host). Tests run with `dotnet test`. See [`docs/architecture.md`](docs/architecture.md) for architecture, [`docs/conventions.md`](docs/conventions.md) for conventions, and [`.github/workflows/ci.yml`](.github/workflows/ci.yml) for the CI build.

## Acknowledgements

The Dynamic Controls pause theme is based on [Pause Shift](https://forums.launchbox-app.com/files/file/3229-pauseshift/) by Faeran.

## License

Licensed under the [MIT License](LICENSE).
