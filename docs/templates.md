# Templates

A template provides the controller chassis artwork and defines where each button image is placed on the overlay. The chassis (`BaseImage.png`) and the button images are rendered independently — buttons are overlaid on top of the chassis at positions defined in `Layout.xml`. This means button images can be swapped out per-platform without changing the chassis, so a single template can display platform-accurate button art for every platform it supports. The active template is set by `DefaultTemplate` in `GlobalConfig.xml`.

## Directory layout

```
Templates/
  {TemplateName}/
    BaseImage.png               — controller chassis artwork; sets canvas dimensions
    Layout.xml                  — slot definitions and rendering rules
    {GenericInput}.png          — generic fallback images (ButtonA.png, ButtonB.png, …)
    Line_{GenericInput}.png     — connector line for that input (Line_ButtonA.png, Line_AxisTriggerLeft.png, …)
    {Platform}/
      {PlatformButton}.png      — platform-specific button image
      {ControllerVariant}/
        {PlatformButton}.png    — controller variant-level override (highest priority)
```

`{Platform}` must match the LaunchBox platform name exactly (e.g. `Sega Genesis`, `Super Nintendo Entertainment System`). `{ControllerVariant}` must match a `<Controller name="...">` value in `Defaults\Platforms\{Platform}\Controllers.xml`.

## Image resolution

When rendering a button the plugin picks the most-specific image that exists on disk:

1. `{Platform}/{ControllerVariant}/{PlatformButton}.png` — controller variant art (only when a controller variant is active)
2. `{Platform}/{PlatformButton}.png` — platform art
3. `{TemplateName}/{GenericInput}.png` — generic template fallback
4. `Templates/{GenericInput}.png` — shared fallback, one level above the template folder; lets several templates share a common image set

**Example:** Rendering the Sega Genesis B button, which the Controllers file maps to the `ButtonA` generic slot:

- With the 6-Button controller variant active: `Sega Genesis/6-Button/B.png`
- Without a controller variant (or if the controller variant folder has no file): `Sega Genesis/B.png`
- If neither exists: `ButtonA.png`

One exception to "most-specific wins": when the current platform has *no* button mapped to that generic input, the generic image is used even if platform art exists — showing a Genesis `C` button on a controller slot the game can't reach would mislead. An `<Input useImage="…">` is exempt, because it is explicitly borrowing another input's artwork.

A `<Whole>` that several platform buttons drive together also looks for the platform's picture of those buttons, before any of the above: `{Platform}/{buttons, sorted by name and space-joined}.png`. Nintendo 64's `AxisRightStickX` (parts `AxisRightStickLeft AxisRightStickRight`, driven by `C-Left` and `C-Right`) finds `Nintendo 64/C-Left C-Right.png`, and its `AxisRightStickY` finds `Nintendo 64/C-Down C-Up.png`, where otherwise `C-Any` — which reaches all four C directions, so covers each pair too — would lend its four-circle picture. The name is the sorted set of buttons driving the parts in this game, so swapping two of them, in a per-game mapping or a RetroArch remap, gives the same name and the same picture; a file that isn't there changes nothing, and a whole driven by one button, or bound to one directly, doesn't look. Button names go through the same file-name clean-up as everything else here, so a name containing a character the operating system forbids in file names has it replaced by `_` — on Windows a MAME port such as `OTHER:STROBE1:2` is looked up as `OTHER_STROBE1_2`, where macOS and Linux keep the colon.

Image files must be `.png`. Paths 1–3 are relative to the template folder; path 4 is relative to `Templates\`.

## Generic input names

The generic inputs used by the shipped Xbox Series X template:

| Category | Names |
|---|---|
| Face buttons | `ButtonA`, `ButtonB`, `ButtonX`, `ButtonY` |
| Shoulders | `ButtonLeftShoulder`, `ButtonRightShoulder` |
| Triggers | `AxisTriggerLeft`, `AxisTriggerRight` |
| Stick clicks | `ButtonLeftStick`, `ButtonRightStick` |
| Left stick | `AxisLeftStick`, `AxisLeftStickUp`, `AxisLeftStickDown`, `AxisLeftStickLeft`, `AxisLeftStickRight` |
| Right stick | `AxisRightStick`, `AxisRightStickUp`, `AxisRightStickDown`, `AxisRightStickLeft`, `AxisRightStickRight` |
| D-pad | `ButtonDpad`, `ButtonDpadUp`, `ButtonDpadDown`, `ButtonDpadLeft`, `ButtonDpadRight` |
| Meta | `ButtonStart`, `ButtonBack`, `ButtonGuide` |

A new template only needs to provide images for the inputs it uses — any generic input without a corresponding file simply won't render.

The composite names in that table (`AxisLeftStick`, `AxisRightStick`, `ButtonDpad`) have no special
meaning to the engine. A template says which of its inputs are composites, and what they are made
of, with `<Whole>` in `<Head>` — see
[layout-xml-schema.md](layout-xml-schema.md#whole--a-composite-control). A template that declares
none has no composites.

## Adding images for a new platform

> **Recommended:** Before modifying a shipped template, copy it to a new folder under `Templates\` and update `DefaultTemplate` in `User\GlobalConfig.xml` to point to the copy. Changes to the shipped `Xbox Series X` folder are overwritten when you update the plugin.

If a platform isn't already in the template, add it in three steps:

### 1. Define the button mapping

Create `User\Platforms\{Platform}\Controllers.xml` (or `Defaults\Platforms\{Platform}\Controllers.xml` to share it with others). This maps each platform button name to a generic input slot:

```xml
<!-- User\Platforms\Atari 7800\Controllers.xml -->
<Controllers>
    <Controller name="Standard">
        <Mapping name="One" input="ButtonA" />
        <Mapping name="Two" input="ButtonB" />
    </Controller>
</Controllers>
```

When a platform has several controllers that share a common button set, a variant can declare
`inheritFrom="OtherController"` to prepend that controller's mappings before its own instead of
repeating them. Inheritance is transitive — the base may itself `inheritFrom` a third controller —
so you can build up a chain (e.g. NEC PC Engine's `6-Button` inherits from `3-Button`, which
inherits from `2-Button`). A variant's own mappings override any inherited entry with the same
`name`. Only mappings are inherited; `analogToDigital` is read from each controller's own attribute.

```xml
<!-- User\Platforms\NEC PC Engine\Controllers.xml -->
<Controllers>
    <Controller name="2-Button">
        <Mapping name="I" input="ButtonB" />
        <Mapping name="II" input="ButtonA" />
    </Controller>
    <Controller name="3-Button" inheritFrom="2-Button">
        <Mapping name="III" input="ButtonX" />
    </Controller>
    <Controller name="6-Button" inheritFrom="3-Button">
        <Mapping name="IV" input="ButtonY" />
    </Controller>
</Controllers>
```

#### `<Control>` — one button standing for several

A control that works as a unit — a stick, a D-pad, a C-button cluster — gets an `<Control>` naming
the buttons it is made of. The control then reaches whatever those buttons reach:

```xml
<!-- Defaults\Platforms\Nintendo 64\Controllers.xml -->
<Controller name="Pad" default="true">
    <Control name="C-Any" mappings="C-Up C-Down C-Left C-Right" />
    <Mapping name="C-Up"    input="AxisRightStickUp" />
    <Mapping name="C-Down"  input="AxisRightStickDown" />
    <Mapping name="C-Left"  input="AxisRightStickLeft" />
    <Mapping name="C-Right" input="AxisRightStickRight" />
</Controller>
```

| Attribute | Required | Notes |
|---|---|---|
| `name` | yes | The platform button being declared — the one a `Labels.xml` entry can name |
| `mappings` | yes | Space-separated platform button names it stands for |

Two things make this worth the indirection rather than just writing `input="AxisRightStick"`:

- **It resolves last**, after any emulator config and any per-game override. A cfg binds directions
  one at a time and has no way to name a control as a unit, so a button that *does* name one has to
  be re-read against wherever its members actually ended up. Naming the generic input directly
  would fix it at the moment the file was written, and the label would be lost on every game whose
  cfg had moved the directions beneath it.
- **It says something about the physical controller**, which is why it lives next to the mappings
  it describes rather than in the template. Whether a platform has a C-cluster is a fact about its
  pad.

A control is appended, never displacing: a button that drives an input directly outranks one that
merely stands for buttons reaching it, so a direct binding keeps the reverse lookup and decides
which image the player is shown. A control whose members are all unbound reaches nothing and is
skipped.

Controls are inherited by `inheritFrom` alongside mappings, under the same override-by-`name` rule.

### Sharing one definition across platforms

When the platforms in one hardware *family* share a controller (e.g. the NES, its Japanese
counterpart the Famicom, and the Famicom Disk System add-on all use the same D-pad + 2-button pad),
put the definition in the family's root platform file and point each descendant at it with a
root-level `inheritFrom`:

```xml
<!-- Defaults\Controllers\Nintendo Entertainment System.xml  (root platform, full definition) -->
<Controllers>
    <Controller name="Pad" analogToDigital="left" default="true">
        <Mapping name="B" input="ButtonA" />
        <Mapping name="A" input="ButtonB" />
        <!-- … -->
    </Controller>
</Controllers>
```

```xml
<!-- Defaults\Controllers\Nintendo Famicom.xml  (one-line pointer) -->
<Controllers inheritFrom="Nintendo Entertainment System" />
```

Group by hardware family, not by whichever mappings happen to coincide today: a pointer should
target the platform that genuinely *defines* the controller family, so a later fix propagates to the
right platforms and no further. The Game Boy line, for instance, is a *separate* family rooted at
`Nintendo Game Boy` — even though its pad currently matches the NES pad — so that adding, say, the
Game Boy Advance's L/R shoulders never leaks onto the NES.

A platform file can also add or override controllers on top of the root — list its own `<Controller>`
elements alongside the root `inheritFrom`; a controller whose `name` matches a root entry replaces
it, and a new name is appended. Root-level inheritance is transitive and controller-level
`inheritFrom` resolves across the file boundary, so a platform can add a `6-Button` that inherits a
`3-Button` defined in the root.


### 2. Add platform button images

Create `Templates\{TemplateName}\{Platform}\` and add one `.png` per platform button. File names must match the `name` attributes in the Controllers file (e.g. `One.png`, `Two.png`).

If the platform buttons happen to map cleanly onto the generic images you can skip this step and rely on the generic fallbacks — the overlay will still show the correct button positions, just with generic artwork.

### 3. Add controller variant images (optional)

If the platform has controller variants, create a subdirectory per controller variant for any images that differ from the platform-level defaults. Subdirectory names must match the `name` attributes on `<Controller>` elements in the Controllers file.

```
Templates\Xbox Series X\Sega Genesis\
    A.png                   — shared by 3-Button and 6-Button
    B.png
    C.png
    6-Button\
        A.png               — 6-Button only (overrides platform-level A.png)
        X.png
        Y.png
        Z.png
```

## Adding a new template

Create `Templates\{TemplateName}\` under your LaunchBox installation:

1. **`BaseImage.png`** — controller chassis artwork. Its pixel dimensions set the canvas size; all coordinates in `Layout.xml` are relative to its top-left corner.
2. **Generic images** — one `.png` per generic input name listed above. These render when no platform-specific image is found.
3. **Connector line images** — the `Line*.png` files referenced by `<Overlay src="…">` in `Layout.xml`. Missing files silently render nothing, so you can omit lines you don't use.
4. **`Layout.xml`** — defines the position, size, and rendering rules for each input slot. See [layout-xml-schema.md](layout-xml-schema.md) for the full element and attribute reference.
5. Set `DefaultTemplate` in `User\GlobalConfig.xml` to the new folder name.

Platform-specific images are optional at creation time — the generic fallbacks work for every platform until you add styled art.
