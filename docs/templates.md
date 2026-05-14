# Templates

A template provides the controller chassis artwork and defines where each button image is placed on the overlay. The chassis (`BaseImage.png`) and the button images are rendered independently — buttons are overlaid on top of the chassis at positions defined in `Layout.xml`. This means button images can be swapped out per-platform without changing the chassis, so a single template can display platform-accurate button art for every platform it supports. The active template is set by `DefaultTemplate` in `GlobalConfig.xml`.

## Directory layout

```
Templates/
  {TemplateName}/
    BaseImage.png               — controller chassis artwork; sets canvas dimensions
    Layout.xml                  — slot definitions and rendering rules
    {GenericInput}.png          — generic fallback images (ButtonA.png, ButtonB.png, …)
    Line{Slot}.png              — connector line graphics (LineA.png, LineLB.png, …)
    {Platform}/
      {PlatformButton}.png      — platform-specific button image
      {ControllerVariant}/
        {PlatformButton}.png    — controller variant-level override (highest priority)
```

`{Platform}` must match the LaunchBox platform name exactly (e.g. `Sega Genesis`, `Super Nintendo Entertainment System`). `{ControllerVariant}` must match a `<Controller name="...">` value in `Defaults\Controllers\{Platform}.xml`.

## Image resolution

When rendering a button the plugin picks the most-specific image that exists on disk:

1. `{Platform}/{ControllerVariant}/{PlatformButton}.png` — controller variant art (only when a controller variant is active)
2. `{Platform}/{PlatformButton}.png` — platform art
3. `{TemplateName}/{GenericInput}.png` — generic template fallback

**Example:** Rendering the Sega Genesis B button, which the Controllers file maps to the `ButtonA` generic slot:

- With the 6-Button controller variant active: `Sega Genesis/6-Button/B.png`
- Without a controller variant (or if the controller variant folder has no file): `Sega Genesis/B.png`
- If neither exists: `ButtonA.png`

Image files must be `.png`. All paths above are relative to the template folder.

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

## Adding images for a new platform

> **Recommended:** Before modifying a shipped template, copy it to a new folder under `Templates\` and update `DefaultTemplate` in `User\GlobalConfig.xml` to point to the copy. Changes to the shipped `Xbox Series X` folder are overwritten when you update the plugin.

If a platform isn't already in the template, add it in three steps:

### 1. Define the button mapping

Create `User\Controllers\{Platform}.xml` (or `Defaults\Controllers\{Platform}.xml` to share it with others). This maps each platform button name to a generic input slot:

```xml
<!-- User\Controllers\Atari 7800.xml -->
<Controllers>
    <Controller name="Standard">
        <Mapping name="One" input="ButtonA" />
        <Mapping name="Two" input="ButtonB" />
    </Controller>
</Controllers>
```

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
