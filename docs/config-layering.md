# Config layering

How the plugin resolves a data file when both a shipped copy and a user copy exist. Read this before changing where a file is loaded from, or changing how two copies of one file combine. For what each file *contains*, see [templates.md](templates.md) and [layout-xml-schema.md](layout-xml-schema.md).

## The two layers

Plugin data lives under `…\LaunchBox\Data\Dynamic Controls\`, split into two layers:

```
Dynamic Controls\
  Defaults\                              ← shipped; replaced wholesale on every update
    GlobalConfig.xml
    Platforms\{Platform}\Controllers.xml
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

Three loaders need a specific layer instead and address it directly through `LayeredFileSystem.Defaults` / `.User`:

- `InputLabelsLoader` reads both and merges them (see [Labels](#platformsplatformlabelsxml--entry-level-merge)).
- `InputMappingLoader` reads both copies of `Controllers.xml` and merges them (see [Controllers](#platformsplatformcontrollersxml--entry-level-merge)).
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
| `Platforms\{Platform}\Controllers.xml` | yes | **Merged** | per entry — per controller, and per game |
| `Emulators\**` | yes | **Replaced** | whole file |
| `Templates\` | no | not layered | — |
| `Static\` | user-only | not layered | per image |

**Three files merge; everything else is replaced wholesale.**

A file is replaced when it describes exactly one thing — one emulator core's controller ids, the MAME joycode table — because replacing it is precisely the customization the user meant, and it needs no cross-file merge logic. A file is merged when it holds many independent settings, because replacing it would discard far more than the user intended to change.

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
- `<Defaults>` entries overlay by the buttons they name. A user `<Input name="Start">` replaces the shipped one; shipped buttons the user didn't name survive. A combination is the same buttons in any order, and a MAME port is the same port under its bare name or its `TYPE:tag:mask`, so `B A` replaces a shipped `A B`.

**The merge is per entry, not per label.** A user `<Game>` entry replaces the shipped entry for that game outright rather than combining button-by-button — so "show *these* labels for this game" stays expressible, instead of leaving the user unable to remove a shipped label.

**A game with control sets is replaced whole as well.** A user entry's `<ControlSet>`s and its own `<Input>`s are the whole entry, so a set the shipped entry has and the user's leaves out is gone, and an empty user entry clears the game's labels. Correcting one context means copying the entry; in exchange, removing a set needs no syntax of its own.

## `Platforms\{Platform}\Controllers.xml` — entry-level merge

**Read both copies and overlay the user's entries onto the shipped ones.**

A platform's file holds several controllers and every game's remap of them, so replacing it wholesale would mean that changing one controller discards the others, and that a user's file has to restate every controller just to remap one game.

- A user `<Controller>` whose `name` matches a shipped one replaces it in place; a new name is added after the shipped ones.
- A user `<Game>` replaces every shipped one it shares a `launchBoxId` or a `romName` with; an unmatched one is added. Matching on either key, rather than looking the keys up separately as `Labels.xml` does, is what lets a user entry giving only `romName` replace a shipped one that also has an ID. A shipped entry sharing no key with the user's survives the merge, so the lookup tries the user's entries first: one naming the game by either key wins over a shipped one naming it by the other.
- The root `default` is the user file's when it states one, otherwise the shipped file's. It names a controller rather than being marked on one, so a user copy of the default controller stays the default. A name that matches no controller is logged and the next one down applies.
- The root `inheritFrom` is the user file's when it states one, otherwise the shipped file's, so a user file for a platform that points at its family's root keeps that pointer without restating it. The platform it points at is itself loaded merged, so a user change to the root platform reaches every platform inheriting from it.
- A root `inheritFrom` brings in the base platform's controllers but not its games: a game's ID and ROM name belong to its own platform.

**The merge is per entry, not per button.** A user controller or game replaces the shipped one outright, the same rule as a `<Game>` in `Labels.xml`, so what an entry's buttons do is stated in one place. To change one button and keep the rest, give the user controller another name and `inheritFrom` the shipped one.

## What ships in a release zip

- The **assets** and **ALL** zips contain `Defaults\` and `Templates\`, and no user-authored file.
- They also carry a `User\` skeleton: the subfolders, each with a `README.txt` describing what belongs there. Those README files are the only thing an update overwrites under `User\`. Keep them documentation-only — never write configuration to that path.
- A missing `User\` folder is handled: layered lookups fall through to `Defaults\`, and a missing `User\Static\` image renders through the normal pipeline.
