# Layout.xml Schema

Reference for the `Layout.xml` file that drives each controller template. Every template lives under `Templates/{templateName}/` and contains:

- A `BaseImage.png` (or `.jpg`) — the chassis artwork
- One `Layout.xml` (this document's schema) — slot definitions
- Per-input images (`ButtonA.png`, `LineL.png`, etc.)

The parser is forgiving: unknown attributes are silently ignored; invalid numeric values are logged and replaced with defaults. The intent is that templates degrade gracefully when authors mistype something. All errors and warnings are written to `Logs\debug.log` — enable `<Debug>true</Debug>` in `GlobalConfig.xml` to see them.

## Concepts to read first

These show up everywhere; understanding them up front makes the per-element reference shorter.

### Coordinates

Every `x` / `y` attribute is one of two forms:

- **Absolute**: a plain number. `x="100"` means "100px from the canvas origin (0,0) at the top-left of the base image".
- **Relative**: a number with a leading `+` or `-`. `x="+5"` means "5px right of the enclosing container's current origin".

What "enclosing container's origin" means depends on context:

| In a... | Relative coords resolve against... |
|---|---|
| `<Render>` inside an `<Input>` | The Input's `x`/`y` (or the Input's enclosing slot if it has none) |
| `<Label>` inside an `<Input>` | Same |
| `<Overlay>` inside an `<Input>` | Same |
| `<Overlay>` inside a `<Stack>` | The Stack's `x`/`y` |
| `<Input>` inside a `<Stack>` | The Stack's `x`/`y` plus `slotIndex × gap` |
| `<Input>` inside a `<Group>` inside a `<Stack>` | The Stack's `x`/`y` plus `slotIndex × gap` (Group is transparent) |
| `<Input>` inside a `<OneOf>` inside a `<Stack>` | The OneOf's slot origin (each alternative shares one slot) |

When you don't specify an `x` or `y`, the element defaults to `+0` (the enclosing origin unchanged). The resolved layout that the renderer sees is always in absolute canvas coordinates — relativity is a compile-time concept.

### `showIf` modes

Controls whether a `<Render>` or `<Overlay>` is at full opacity, or rendered in an inactive state instead. When a `showIf` condition is not met, the element is rendered at reduced opacity and optionally blurred rather than hidden entirely. `minOpacity` sets how faint it goes (`0` = invisible, `1` = full brightness); `inactiveBlurRadius` sets the blur radius (`0` = sharp). Both default to `0`, which hides inactive elements completely.

Allowed values:

- `label` — full opacity when this input has a label, inactive otherwise
- `mapping` — full opacity when a platform button drives this input, inactive otherwise
- `auto` — `label` mode when the game contributed its own labels, `mapping` mode otherwise (see [architecture.md](architecture.md))
- *(omitted)* — always full opacity

`showIf` can be set on:
- A named `<Style>` in `<Head>` — applied to any element that references the style
- An `<Input>` — inherited by its `<Render>` and `<Overlay>` children that don't set their own
- A `<Render>` or `<Overlay>` — wins over inherited values

### Style cascade

Each visual attribute (`fontSize`, `minOpacity`, `inactiveBlurRadius`, `showIf`) is resolved in priority order:

1. **Explicit attribute on the element** — `<Render minOpacity="0.5" />`
2. **Named style reference** — `<Input style="foo">` looks up the `<Style name="foo">` in `<Head>`
3. **Inherited from parent** — for elements inside an `<Input>`, the Input's attribute is inherited
4. **Template default** — the unnamed `<Style>` in `<Head>`
5. **Built-in default** — `fontSize=28`, `minOpacity=0`, `inactiveBlurRadius=0`

Explicit attributes always win. Use named styles to share visual treatment across many inputs without repetition.

### Image resolution

When the renderer needs the actual file for a `<Render>` or `<Overlay>`, it walks a four-tier path chain (highest priority first):

```
Templates/{template}/{platform}/{controller}/{file}    ← controller-specific
Templates/{template}/{platform}/{file}                 ← platform-specific
Templates/{template}/{file}                            ← template-local
Templates/{file}                                       ← shared root
```

The file name comes from `<Render useImage>` if specified, else the Input's `name` (with `.png` appended). For `<Overlay>`, it's always the `src` attribute verbatim.

This lets templates supply platform-aware artwork (e.g. PlayStation symbols for `Sega Genesis` controllers driving a PlayStation chassis) without forking the template.

## Element reference

### `<ControllerTemplate>` — root

The document root. Contains `<Head>` and `<Body>` in either order.

```xml
<ControllerTemplate>
    <Head>...</Head>
    <Body>...</Body>
</ControllerTemplate>
```

No attributes.

### `<Head>` — metadata

Template-wide metadata. Currently the only child element type is `<Style>`. Future non-display configuration would go here.

```xml
<Head>
    <Style fontSize="24" inactiveBlurRadius="8" />
    <Style name="auto-blur" showIf="auto" minOpacity="0.3" />
</Head>
```

### `<Style>` — visual defaults

Two distinct uses, distinguished by the presence of `name`:

- **Without `name`** — template-wide defaults. Sets `fontSize`, `minOpacity`, `inactiveBlurRadius` for the whole template. Multiple unnamed styles aren't useful; the last one wins.
- **With `name`** — a referenceable bundle. Inputs reference it via `<Input style="...">`. The named style's attributes are applied to the input unless the input overrides them.

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `name` | string | no | Names the style for reference |
| `showIf` | enum (see above) | no | Only meaningful on named styles |
| `fontSize` | double | no | |
| `minOpacity` | double | no | Opacity when `showIf` condition is not met; `0` = hidden, `1` = always full opacity |
| `inactiveBlurRadius` | double | no | Blur radius when `showIf` condition is not met; `0` = no blur |

### `<Body>` — display layout

The container for everything the renderer cares about. Direct children are `<Input>`, `<Group>`, `<Stack>`, and `<OneOf>`, in document order.

### `<Input>` — a generic input

The unit of the layout. An Input has a `name` matching a generic input identifier (`ButtonA`, `AxisLeftStickUp`, etc.) and contains the renders, labels, and overlays that visualise it. Generic input names are a system-wide vocabulary shared across Controllers.xml, the RetroArch and MAME integrations, and the template — each layer speaks in these names so they all connect without knowing about each other.

```xml
<Input name="ButtonA" style="auto-blur" x="970" y="401">
    <Render height="64" width="64" />
    <Label x="+0" y="+72" />
</Input>
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `name` | string | **yes** | Generic input name. Input with no name is skipped + logged |
| `style` | string | no | Named style reference (`<Style name="...">` in `<Head>`) |
| `showIf` | enum | no | Inherited by nested `<Render>` / `<Overlay>` |
| `minOpacity` | double | no | Inherited |
| `inactiveBlurRadius` | double | no | Inherited |
| `fontSize` | double | no | Inherited by `<Label>` |
| `x` | coordinate | no | Origin for nested elements with relative coords. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |

**Children** (any combination, any order):
- `<Render>` — image render
- `<Label>` — label text
- `<Overlay>` — additional image
- `<Input>`, `<Group>`, `<Stack>`, `<OneOf>` — nested layout

**Nested Input semantics**: A nested `<Input>` inside another Input establishes a parent-child relationship. The parent's renders fan out to the child's renders for image fallback (a child input that can't find its own image uses the parent's). A common pattern is the four-direction nested inputs under an `AxisLeftStick` — `AxisLeftStickUp`, `AxisLeftStickDown`, etc.

**Strict-self render position**: A duplicate top-level `<Input>` with no nested children expresses "render the parent input's image at this position, independent of its descendants" — used by some templates to put an extra render in a different slot.

### `<Render>` — image render position

Where to draw an input image. Multiple `<Render>` children produce multiple visual copies of the input.

```xml
<Render x="+0" y="+0" height="64" width="64" />
<Render useImage="ButtonDpadUp" x="+0" y="+45" height="34" width="34" />
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `x` | coordinate | no | Relative to Input's origin. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |
| `width` | double | no | NaN = use image's natural width |
| `height` | double | no | NaN = use image's natural height |
| `useImage` | string | no | Override image filename (no extension; `.png` appended). Affects asset-borrowing semantics — see below |
| `showIf` | enum | no | Defaults to inherited from Input |
| `minOpacity` | double | no | Defaults to inherited |
| `inactiveBlurRadius` | double | no | Defaults to inherited |

**`useImage` and asset borrowing**: When `useImage` is set, the render is "borrowing" another input's artwork. The image resolution chain still applies, so a borrowed image gets its platform-specific variant even when the *borrowing* input isn't mapped. This is how `AxisRightStickUp` shows the same up-arrow as `AxisLeftStickUp` without copying the asset.

A render with an unparseable `x` or `y` value logs an error and keeps the default (+0).

### `<Overlay>` — additional image

An arbitrary image rendered at a position, with no implicit relationship to the input's `name`. Used for connector lines (`LineL.png`, `LineDpad.png`), background frames, decorative artwork.

```xml
<Overlay src="LineL.png" x="+79" y="+31" />
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `src` | string | **yes** | Image filename. Overlay with no `src` is skipped + logged |
| `x` | coordinate | no | Relative to container's origin |
| `y` | coordinate | no | Same |
| `width` | double | no | NaN = natural |
| `height` | double | no | NaN = natural |
| `showIf` | enum | no | Inherited from Input when nested in one |
| `minOpacity` | double | no | Inherited |
| `inactiveBlurRadius` | double | no | Inherited |

**Placement**: Overlays can be children of `<Input>` (visibility inherits from the Input), `<Group>` (visible once when the group is included), `<Stack>` (visible whenever the stack is rendered), or anywhere a render-context exists.

### `<Label>` — label text position

Where to draw the label text for this input. The text content itself comes from the resolved labels — `<Label>` only declares position, alignment, and font size.

```xml
<Label x="+60" y="+22" align="left" fontSize="20" />
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `x` | coordinate | no | Relative to Input's origin |
| `y` | coordinate | no | Same |
| `align` | enum | no | `left`, `center`, `right`. Default `left`. Lower-cased on read |
| `fontSize` | double | no | Defaults to inherited from Input, then template default |

A label with an unparseable coordinate logs the error and keeps the default (+0).

### `<Group>` — conditional cluster

A wrapper around a cluster of related inputs. Two purposes:

1. **Conditional inclusion**: when no descendant has a visible render, the *entire group* is excluded from `inputsToRender` — its labels aren't rendered, its overlays aren't drawn. This is "semantic exclusion", not just fading.
2. **Shared overlays**: an `<Overlay>` declared at the group level renders once when the group is included, instead of being repeated on every member.

```xml
<Group>
    <Overlay src="LineDpad.png" x="+72" y="-45" />
    <Input name="ButtonDpadUp">...</Input>
    <Input name="ButtonDpadDown">...</Input>
</Group>
```

No attributes. Children: `<Input>`, `<Group>`, `<Stack>`, `<OneOf>`, `<Overlay>` in any order.

A Group inside a `<Stack>` is *transparent* to slot counting — each Input in the Group consumes its own stack slot.

### `<Stack>` — positioned cluster

A vertical list of inputs, each spaced `gap` pixels below the last. The first child sits at the Stack's `(x, y)`, the second at `(x, y + gap)`, the third at `(x, y + 2×gap)`, and so on.

Unlike `<Group>`, a Stack is always included in the layout — it never hides itself based on whether its children are visible. Each child decides its own visibility independently.

```xml
<Stack x="312" y="291" gap="45" collapse="true">
    <Input name="ButtonDpadUp">...</Input>
    <Input name="ButtonDpadLeft">...</Input>
    <Input name="ButtonDpadRight">...</Input>
    <Input name="ButtonDpadDown">...</Input>
</Stack>
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `x` | coordinate | no | Stack origin. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |
| `gap` | double | no | Vertical spacing between children. Default 0 |
| `collapse` | bool (`true`/anything-else) | no | When `true`, hidden children vacate their slot and later children shift up to close the gap |

**Children** can be `<Input>`, `<Group>`, `<Stack>`, `<OneOf>`, `<Overlay>` in any order.

**How children occupy slots** — each child takes one position in the vertical list, except:

| Child kind | Slot behaviour |
|---|---|
| `<Input>` | Takes one slot |
| `<Group>` | Transparent — its children each take their own slot as if the Group wasn't there |
| `<Stack>` (nested) | Takes one slot as a block; the inner Stack positions its own children independently |
| `<OneOf>` | Takes one slot; all its alternatives share that same position |
| `<Overlay>` | Takes no slot — positioned at its own coordinates regardless |

**Collapse** (`collapse="true"`) removes the gap left by hidden children. When a child's renders are all invisible, it vacates its slot and everything below shifts up by `gap`. Without collapse, slots are always fixed — a hidden child leaves a faded image or blank space.

### `<OneOf>` — mutually-exclusive alternatives

A container where only the first alternative whose visibility check passes is rendered; the rest are dropped entirely from `inputsToRender`. Used for "render the cluster of dpad labels, or render the single dpad icon, never both".

```xml
<OneOf>
    <Group>
        <!-- a labelled cluster -->
        <Input name="ButtonDpadUp">...</Input>
        <Input name="ButtonDpadDown">...</Input>
    </Group>
    <Input name="ButtonDpad" style="show-if-label">
        <!-- a single icon as fallback -->
    </Input>
</OneOf>
```

No attributes. Children: `<Input>`, `<Group>`, `<Stack>`, `<OneOf>` in document order (the first-match-wins ordering is significant).

**Visibility check per alternative**:
- `<Input>` — "any-render-visible" (at least one of the input's renders passes its `showIf`)
- `<Group>` — "any-member-visible" (recursively, the same check on at least one descendant)

If no alternative passes, the OneOf renders nothing — all alternatives are dropped.

## File-level conventions

- Element names are case-sensitive. `<input>` is silently ignored.
- Attribute values are parsed culture-invariantly — use `.` as the decimal separator.
- Numbers (`width`, `height`, `gap`, `fontSize`, `minOpacity`, `inactiveBlurRadius`) are `double`. NaN signals "use the natural value" for `width`/`height`.
- Boolean attributes (currently just `collapse`) accept `"true"` case-insensitively. Any other value is treated as `false`.
- Coordinates (`x`, `y`) accept `+N`, `-N`, or plain `N`. A leading `+` makes it relative even when `N` is positive — without the `+`, it's absolute.
- The order of children matters in `<Stack>` (slot index), `<OneOf>` (alternative priority), and `<Body>` (document order is preserved through the render output).

## Diagnostic logging

The parser emits errors to the configured `ILogger` for:

- Element with a missing required attribute (e.g. `<Input>` without `name`)
- Element with an unparseable coordinate
- Unknown element where one of `<Head>`, `<Body>`, `<Input>`, `<Render>`, `<Overlay>`, `<Label>`, `<Group>`, `<Stack>`, `<OneOf>` was expected
- `<Input style="X">` where `X` isn't a `<Style name="X">` in `<Head>`
- `<Render showIf="X">` where `X` isn't a known mode

Errors don't abort the load — the bad element is skipped (or, for coordinate problems, replaced with `+0`), the rest of the template parses normally. Check the log file after a problem template to see what was dropped.

## Complete example

A minimal template with a single button:

```xml
<ControllerTemplate>
    <Head>
        <Style fontSize="20" inactiveBlurRadius="6" />
    </Head>
    <Body>
        <Input name="ButtonA" x="100" y="100">
            <Render width="64" height="64" />
            <Label x="+0" y="+72" />
        </Input>
    </Body>
</ControllerTemplate>
```

For a full reference, see the `Templates/Xbox Series X/Layout.xml` in this repository — it exercises every concept in this document (Head with named styles, Stack with collapse, Group with shared overlay, OneOf with `Group`-or-`Input` alternatives, `useImage` for asset borrowing, all four `showIf` modes).

## Conventions for new templates

These aren't enforced by the parser, but following them keeps templates legible:

- Order `<Input>` elements roughly by visual position (top to bottom, left to right) when not constrained by slot ordering
- Group related inputs (face buttons, shoulder buttons) with a comment if you're not using `<Group>` itself
- Define named styles in `<Head>` for any combination of `showIf` + `minOpacity` you use more than twice
- Prefer relative coordinates inside `<Stack>` and `<Group>` so the cluster moves as a unit when you tweak its origin
- Keep `<Overlay>` lines (`LineX.png` etc.) at the group level, not duplicated on every Input
- Name files consistently: `Button*.png` for button icons, `Axis*.png` for stick directions, `Line*.png` for connector lines
