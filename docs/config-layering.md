# Config layering

How the plugin resolves a data file when both a shipped copy and a user copy exist. Read this before changing where a file is loaded from, or changing how two copies of one file combine. For what each file *contains*, see [templates.md](templates.md) and [layout-xml-schema.md](layout-xml-schema.md).

## The two layers

Plugin data lives under `…\LaunchBox\Data\Dynamic Controls\`, split into two layers:

```
Dynamic Controls\
  Defaults\                              ← shipped; replaced wholesale on every update
    GlobalConfig.xml
    Platforms\{Platform}\Controllers.xml
    Platforms\{Platform}\ControllerOverrides\{Rom}.xml
    Platforms\{Platform}\Labels.xml
    Emulators\MAME\JoycodeMapping.xml
    Emulators\RetroArch\{CoreDisplayName}.xml
  User\                                  ← user-authored; never overwritten
    (same relative paths, all optional)
    Static\{Platform}\{Rom}.png|.jpg     ← user-only; no shipped counterpart
  Templates\{Name}\                      ← shipped; not layered
  Logs\                                  ← output; not layered
  controls.xml                           ← BYOAC MAME database; not layered
```

`Defaults\` holds only shipped data — never anything a user authored. `User\` holds only user-authored files. An update writes `Defaults\` and `Templates\` and nothing else, which is what makes re-extracting a release zip safe.

## Resolution

**A layered lookup returns `User\{path}` when that file exists, otherwise `Defaults\{path}`, otherwise null.**

`LayeredFileSystem.Resolve(...segments)` performs it, and returns a root-relative path that the caller reads back through `FileExists`/`OpenRead`. Loaders call it without knowing which layer won.

Two loaders need a specific layer instead and address it directly through `LayeredFileSystem.Defaults` / `.User`:

- `InputLabelsLoader` reads both and merges them (see [Labels](#platformsplatformlabelsxml--entry-level-merge)).
- `StaticImageResolver` reads `User\` only, because `Static\` has no shipped counterpart.

## What isn't layered

| Path | Resolves from | Why |
|---|---|---|
| `Templates\` | root | Shipped and fixed — a user cannot override a template, so it needs no layer |
| `Static\` | `User\` only | Purely user-supplied overlay images; there is no shipped default to override |
| `Logs\` | root | Plugin output, not configuration |
| `controls.xml` | root | Third-party database the user supplies; not shipped, so nothing to shadow |
| RetroArch's own `.cfg`/`.rmp` files | the emulator installation | They belong to RetroArch, not to the plugin's data folder |

## Merge strategy per file

| File | User override | Strategy | Granularity |
|---|---|---|---|
| `GlobalConfig.xml` | yes | **Merged** | per setting |
| `Platforms\{Platform}\Labels.xml` | yes | **Merged** | per entry — per game, and per default button |
| `Platforms\{Platform}\Controllers.xml` | yes | **Replaced** | whole file, per platform |
| `Platforms\{Platform}\ControllerOverrides\{Rom}.xml` | yes | **Replaced** | whole file, per game |
| `Emulators\**` | yes | **Replaced** | whole file |
| `Templates\` | no | not layered | — |
| `Static\` | user-only | not layered | per image |

**Two files merge; everything else is replaced wholesale.**

A file is replaced when it describes exactly one thing — one platform's controllers, one game's mapping — because replacing it is precisely the customization the user meant, and it needs no cross-file merge logic. A file is merged when it holds many independent settings, because replacing it would discard far more than the user intended to change.

## `GlobalConfig.xml` — field-level merge

**Load `Defaults\GlobalConfig.xml`, then overwrite only the settings that are *present* in `User\GlobalConfig.xml`.**

A user changing one setting writes only that setting, and every other default — including ones added in later releases — still applies:

```xml
<Config>
  <DefaultTemplate>My Controller</DefaultTemplate>
</Config>
```

**The overlay must detect presence by reading the user file's child element names, not by deserializing it into a `GlobalConfig` and copying fields.** Deserialization fills absent elements with type defaults, so an omitted `<EnableRetroArch>` becomes `false` and silently overrides a shipped `true`. `ConfigLoader` makes an `XmlDocument` pass over the element names for this reason.

## `Platforms\{Platform}\Labels.xml` — entry-level merge

**Read both copies and overlay the user's entries onto the shipped ones.**

One file holds every game on a platform, so replacing it wholesale would mean that labelling a single game discards the shipped labels for every other game on that platform.

- `<Game>` entries match by `launchBoxId` first, then `romName`. A user entry matching a shipped one replaces it; an unmatched user entry is added.
- `<Defaults>` entries overlay by their `name` attribute. A user `<Input name="Start">` replaces the shipped one; shipped buttons the user didn't name survive.

**The merge is per entry, not per label.** A user `<Game>` entry replaces the shipped entry for that game outright rather than combining button-by-button — so "show *these* labels for this game" stays expressible, instead of leaving the user unable to remove a shipped label.

## What ships in a release zip

- The **assets** and **ALL** zips contain `Defaults\` and `Templates\`, and no user-authored file.
- They also carry a `User\` skeleton: the subfolders, each with a `README.txt` describing what belongs there. Those README files are the only thing an update overwrites under `User\`. Keep them documentation-only — never write configuration to that path.
- A missing `User\` folder is handled: layered lookups fall through to `Defaults\`, and a missing `User\Static\` image renders through the normal pipeline.
