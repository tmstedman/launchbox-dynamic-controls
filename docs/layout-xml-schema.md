# Layout.xml Schema

Reference for the `Layout.xml` file that drives each controller template. Every template lives under `Templates/{templateName}/` and contains:

- A `BaseImage.png` — the chassis artwork (PNG only; a `.jpg` is not probed)
- One `Layout.xml` (this document's schema) — slot definitions
- Per-input images (`ButtonA.png`, `Line_AxisLeftStick.png`, etc.)

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
| `<Label>` inside an `<Input>` | The Input's `x`/`y` (or the Input's enclosing slot if it has none) |
| `<Overlay>` inside an `<Input>` | Same |
| `<Overlay>` inside a `<Group>` | The Group's `x`/`y` |
| `<Input>` inside a `<Group>` | The Group's `x`/`y` plus `slotIndex × gap` |
| `<Input>` inside a nested `<Group>` inside a `<Group>` | The inner Group's own `x`/`y` plus its own `slotIndex × gap` (a nested Group is never transparent — it establishes its own origin) |
| `<Input>` inside a `<OneOf>` inside a `<Group>` | The OneOf's slot origin (each alternative shares one slot) |
| `<Input>` inside a `<Condition>` inside a `<Group>` | The outer Group's `x`/`y` plus `slotIndex × gap` (Condition is transparent) |

When you don't specify an `x` or `y`, the element defaults to `+0` (the enclosing origin unchanged). The resolved layout that the renderer sees is always in absolute canvas coordinates — relativity is a compile-time concept.

An `<Input>`'s own image has no relative-coordinate row of its own: it is always drawn exactly at the Input's own `x`/`y` (or its enclosing slot, if it has none) — there is no separate offset layer for it the way `<Label>`/`<Overlay>` have.

### `showIf` modes

Controls whether an Input's own image, or an `<Overlay>`, is at full opacity, or rendered in an inactive state instead. When a `showIf` condition is not met, the element is rendered at reduced opacity and optionally blurred rather than hidden entirely. `minOpacity` sets how faint it goes (`0` = invisible, `1` = full brightness); `inactiveBlurRadius` sets the blur radius (`0` = sharp). Both default to `0`, which hides inactive elements completely.

Allowed values:

- `label` — full opacity when this input has a label, inactive otherwise
- `mapping` — full opacity when a platform button drives this input, inactive otherwise
- `auto` — `label` mode when the game contributed its own labels, `mapping` mode otherwise (see [architecture.md](architecture.md))
- *(omitted)* — always full opacity

`showIf` can be set on:
- A named `<Style>` in `<Head>` — applied to any element that references the style
- An `<Input>` — governs its own image, and is inherited by its `<Overlay>` children that don't set their own
- An `<Overlay>` — wins over the inherited value

### Style cascade

Each visual attribute (`fontSize`, `minOpacity`, `inactiveBlurRadius`, `showIf`) is resolved in priority order:

1. **Explicit attribute on the element** — `<Input minOpacity="0.5">`
2. **Named style reference** — `<Input style="foo">` looks up the `<Style name="foo">` in `<Head>`
3. **Inherited from parent** — for an `<Overlay>` inside an `<Input>`, the Input's attribute is inherited
4. **Template default** — the unnamed `<Style>` in `<Head>`
5. **Built-in default** — `fontSize=28`, `minOpacity=0`, `inactiveBlurRadius=0`

Explicit attributes always win. Use named styles to share visual treatment across many inputs without repetition.

### Image resolution

When the renderer needs the actual file for an Input's own image, or for an `<Overlay>`, it resolves the filename to a **pair** of candidates rather than to a single winner:

```
styled:   Templates/{template}/{platform}/{controller}/{file}   ← controller-specific
          Templates/{template}/{platform}/{file}                ← platform-specific
          (or none, if neither exists)

generic:  Templates/{template}/{file}                           ← template-local
          Templates/{file}                                      ← shared root
```

The file name comes from `<Input useImage>` if specified, else the Input's own `name` (with `.png` appended). For `<Overlay>`, it's always the `src` attribute verbatim.

Which of the two is drawn depends on the input's mapping state, so styled art doesn't appear on buttons the current controller doesn't have:

| Input state | Image drawn |
|---|---|
| A platform button drives it, as that button's natural target | `{platformButton}.png` from the styled tiers, else the generic |
| A platform button drives it, but the button naturally targets another input | `{platformButton}.png` from the styled tiers, so the player sees the button they're physically pressing; else the generic |
| No platform button drives it | The generic — **unless** the Input sets `useImage`, which is borrowing another input's asset and so honours that asset's styled variant |

This lets templates supply platform-aware artwork (e.g. Genesis `A`/`B`/`C` art on an Xbox chassis) without forking the template, and without a platform's art leaking onto buttons that platform doesn't have.

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

The container for everything the renderer cares about. Direct children are `<Input>`, `<Group>`, `<OneOf>`, and `<Condition>`, in document order. A bare `<Label>` parses here too, but always errors — there's no enclosing `<Input>` for it to attach to at the very top of the tree (see [Loose `<Label>`](#loose-label)).

### `<Input>` — a generic input

The unit of the layout. An Input has a `name` matching a generic input identifier (`ButtonA`, `AxisLeftStickUp`, etc.), draws exactly one image described by its own attributes, and contains the labels and overlays that further visualise it. Generic input names are a system-wide vocabulary shared across Controllers.xml, the RetroArch and MAME integrations, and the template — each layer speaks in these names so they all connect without knowing about each other.

```xml
<Input name="ButtonA" style="auto-blur" x="970" y="401" height="64" width="64">
    <Label x="+0" y="+72" />
</Input>
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `name` | string | **yes** | Generic input name. Input with no name is skipped + logged |
| `style` | string | no | Named style reference (`<Style name="...">` in `<Head>`) |
| `showIf` | enum | no | Governs this Input's own image; inherited by `<Overlay>` children that don't set their own |
| `minOpacity` | double | no | Governs this Input's own image; inherited by `<Overlay>` |
| `inactiveBlurRadius` | double | no | Governs this Input's own image; inherited by `<Overlay>` |
| `fontSize` | double | no | Inherited by `<Label>` |
| `x` | coordinate | no | Also the position of this Input's own image. Origin for nested elements with relative coords. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |
| `width` | double | no | This Input's own image render width. NaN = use the image's natural width |
| `height` | double | no | Same, height |
| `useImage` | string | no | Override image filename for this Input's own image (no extension; `.png` appended). Affects asset-borrowing semantics — see below |

**`useImage` and asset borrowing**: When `useImage` is set, the Input is "borrowing" another input's artwork. The image resolution chain still applies, so a borrowed image gets its platform-specific variant even when the *borrowing* input isn't mapped. This is how `AxisRightStickUp` shows the same up-arrow as `AxisLeftStickUp` without copying the asset.

**Children** (any combination, any order):
- `<Label>` — label text
- `<Overlay>` — additional image
- `<Input>`, `<Group>`, `<OneOf>`, `<Condition>` — nested layout

**Nested Input semantics**: A nested `<Input>` inside another Input establishes a parent-child relationship. The parent's own image fans out to the child's image for fallback (a child input that can't find its own image uses the parent's). A common pattern is the four-direction nested inputs under an `AxisLeftStick` — `AxisLeftStickUp`, `AxisLeftStickDown`, etc.

**Strict-self render position**: A duplicate top-level `<Input>` with no nested children expresses "render the parent input's image at this position, independent of its descendants" — used by some templates to put an extra render in a different slot.

### Loose `<Label>`

A `<Label>` doesn't have to be a *direct* child of its own `<Input>` — it can also appear inside a `<Group>`, `<OneOf>`, or `<Condition>` that's itself somewhere inside that Input, most usefully nested inside a `<Condition>` to gate a label independently of its Input's other content:

```xml
<Input name="ButtonDpad" height="135" width="135">
    <Condition any="SomeOtherInput" match="label">
        <Label x="+0" y="+72" />
    </Condition>
</Input>
```

The loose `<Label>` attaches to whichever `<Input>` is ambient at that point in the tree — here, `ButtonDpad`, even though it's several levels of `<Condition>` away — exactly as if it had been written as a direct child. Entering a *nested* `<Input>` resets this ambient identity to the nested one; entering `<Group>`/`<OneOf>`/`<Condition>` does not, since none of those are a new Input's own boundary. Its coordinate origin, though, follows whatever `<Group>` it's actually inside: `<Condition>` passes the origin through completely unchanged, and `<OneOf>` shares one slot's origin across every alternative, but a `<Group>` always establishes its own frame — see below.

Whether it renders at all is decided once, structurally, the same way a `<Group>`'s members or a `<OneOf>`'s alternatives are: reaching a `<Condition>` that fails drops everything inside it, the loose label included, before label-specific concerns (its own position, font size) ever come into play. A loose `<Label>` with **no** enclosing `<Input>` at all — not even an ambient one, e.g. one sitting directly under `<Body>` or inside a top-level `<Group>`/`<Condition>` with no `<Input>` anywhere above it — is a template-authoring error, logged once at load time; nothing is rendered.

**A loose `<Label>` centers itself against an enclosing `<Group>`.** Placed directly inside a `<Group>` (or reached through a `<OneOf>`/`<Condition>` nested in it), its `y` resolves to the visual center of that Group's slots — the midpoint between the first and last slot that's actually showing *for the current game* — rather than the shifted per-slot origin the Group's own members use:

```xml
<Input name="AxisLeftStick">
    <Condition any="AxisLeftStick" match="label">
        <Group x="312" y="358.5" gap="45" collapse="true" vAlign="center">
            <Input name="AxisLeftStickUp" style="small-label-vacate" height="34" width="34" />
            <Input name="AxisLeftStickLeft" style="small-label-vacate" height="34" width="34" />
            <Input name="AxisLeftStickRight" style="small-label-vacate" height="34" width="34" />
            <Input name="AxisLeftStickDown" style="small-label-vacate" height="34" width="34" />
            <Label x="-24" align="right" />
        </Group>
    </Condition>
</Input>
```

This lands the label "half-way up the group" — and it stays there regardless of how many of the four directions are actually present this game, *for every `vAlign` value*, not only `"center"`. Which slots survive `collapse="true"` is a per-game fact (this game's mapping/labels decide it), so the center is computed at render time rather than baked in once at template load — see [`<Group>`](#group--positioned-conditional-cluster) for the group's own static shape, and the engine's `LayoutFilter.ResolveLooseLabel` for the computation itself. Without `collapse="true"`, nothing ever varies by game (slots never vacate), so the center is simply the group's full, fixed slot count every time. A direct child `<Label>` on one of the Group's own *members* is unaffected by any of this — it keeps resolving against that member's own slot position, exactly as if the Group weren't there, since a nested `<Input>` resets the ambient anchor to its own.

**A *nested* `<Group>` is never transparent for this anchor**, even one with no `x`/`y`/`gap`/`vAlign` of its own: it always establishes its own frame, so a loose `<Label>` placed directly inside it centers on *that* Group's own slots — not the outer Group's. Only `<OneOf>` and `<Condition>` stay genuinely transparent for the anchor.

### `<Overlay>` — additional image

An arbitrary image rendered at a position, with no implicit relationship to the input's `name`. Used for connector lines (`Line_AxisLeftStick.png`, `Line_ButtonDpad_Multi.png`), background frames, decorative artwork.

```xml
<Overlay src="Line_AxisLeftStick.png" x="+79" y="+31" />
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

**Placement**: Overlays can be children of `<Input>` (visibility inherits from the Input) or `<Group>` (visible once when the group is included), or anywhere a render-context exists.

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

### `<Group>` — positioned, conditional cluster

A vertical list of inputs, each spaced `gap` pixels below the last. Which slot sits at `y` itself depends on `vAlign`: by default (`vAlign="top"`) the first child sits at the Group's `(x, y)`, the second at `(x, y + gap)`, the third at `(x, y + 2×gap)`, and so on. Two purposes, always in force together:

1. **Conditional inclusion**: when no descendant has a visible render, the *entire group* is excluded from `inputsToRender` — its labels aren't rendered, its overlays aren't drawn. This is "semantic exclusion", not just fading. Each child still decides its own individual visibility independently when the group *is* included.
2. **Shared overlays**: an `<Overlay>` declared at the group level renders once when the group is included, instead of being repeated on every member.

```xml
<Group x="312" y="291" gap="45" collapse="true">
    <Overlay src="Line_ButtonDpad_Multi.png" x="+72" y="-45" />
    <Input name="ButtonDpadUp">...</Input>
    <Input name="ButtonDpadLeft">...</Input>
    <Input name="ButtonDpadRight">...</Input>
    <Input name="ButtonDpadDown">...</Input>
</Group>
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `x` | coordinate | no | Group origin. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |
| `gap` | double | no | Vertical spacing between children. Default 0 |
| `vAlign` | `top` \| `bottom` \| `center` | no | Which slot `y` refers to. Default `top` — see below |
| `collapse` | bool (`true`/anything-else) | no | When `true`, hidden children vacate their slot and later children shift up to close the gap |

**`vAlign`** shifts the group's whole origin *before* slots are laid out, so it changes where every child ends up, not just one of them:

- `top` (default) — `y` is the first slot; children fall below it, same as always.
- `bottom` — `y` is the *last* slot; children are laid out so the last one lands exactly on `y`, with earlier ones above it.
- `center` — `y` is the midpoint between the first and last slot.

The slot count used for this is the template's fixed slot count (the same one in the table below), computed once when the template loads and cached from then on — not how many children happen to be visible at render time. On its own that's exactly right, since without `collapse` every slot always renders (just possibly faded), so the fixed count and the actual count never differ.

Combined with `collapse`, they can differ — a vacated slot means fewer are actually left than `vAlign` was anchored against. Rather than drift toward `top` as slots vacate, the anchor is corrected at render time: the same `vAlign` shift is re-derived against however many slots collapse has actually left, and the difference from the fixed-count shift is folded uniformly into every remaining child's position, on top of collapse's own per-vacancy shift. The net effect: `bottom`/`center` stay pinned to the declared `y` no matter how many children are actually showing.

This is exactly what makes a loose `<Label>` placed directly in the Group useful for a single label shared across a collapsing cluster — see [Loose `<Label>`](#loose-label) — its position is the declared `(x, y)` itself (never shifted), so it inherits this same no-drift guarantee for free, with no per-game recalculation of its own.

A Group with only one slot renders identically under every `vAlign` value, since there's nothing to distribute around.

An unrecognized `vAlign` value logs an error and falls back to `top`.

**Children** can be `<Input>`, `<Group>`, `<OneOf>`, `<Condition>`, `<Overlay>` in any order, plus a loose `<Label>` (see [Loose `<Label>`](#loose-label)).

**How children occupy slots** — each child takes one position in the vertical list, except:

| Child kind | Slot behaviour |
|---|---|
| `<Input>` | Takes one slot |
| `<Group>` (nested) | Takes one slot as a block; the inner Group positions its own children independently — never transparent, even with no `x`/`y`/`gap` of its own |
| `<OneOf>` | Takes one slot; all its alternatives share that same position |
| `<Condition>` | Transparent — its children each take their own slot as if the Condition wasn't there |
| `<Overlay>` | Takes no slot — positioned at its own coordinates regardless |
| `<Label>` (loose) | Takes no slot, same as `<Overlay>` |

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

No attributes. Children: `<Input>`, `<Group>`, `<OneOf>`, `<Condition>` in document order (the first-match-wins ordering is significant). A bare `<Label>` parses here too (see [Loose `<Label>`](#loose-label)), but is a degenerate alternative — it never counts as visible on its own (see below), so it's only useful for the loose label it carries, never for "winning" the `<OneOf>`.

**Visibility check per alternative**:
- `<Input>` — "any-render-visible" (its own image passes its `showIf`)
- `<Group>` — "any-member-visible" (recursively, the same check on at least one descendant)
- `<Condition>` — its own `all`/`any`/`none` check against its named inputs (see below), ignoring what its children render
- `<Label>` (loose) — always false; it isn't a visibility-bearing alternative in its own right

If no alternative passes, the OneOf renders nothing — all alternatives are dropped.

### `<Condition>` — explicit named-input gate

A container whose children render only when an explicit `all`/`any`/`none` check passes against named generic inputs' label or mapping state. Where `<Group>`'s conditional inclusion is implicit — "visible when any *descendant* has a visible render" — `<Condition>` names its own inputs and checks them directly, by dictionary lookup, regardless of what its children are. This is what lets it gate a subtree on an input that isn't (or isn't only) one of that subtree's own inputs — something `<Group>`'s descendant fold-in can't express.

```xml
<Condition any="AxisLeftStick" match="label">
    <Condition all="AxisLeftStickUp AxisLeftStickLeft AxisLeftStickRight AxisLeftStickDown" match="label">
        <Input name="AxisLeftStick" style="show-if-label" x="307" y="339">
            <!-- single glyph, shown only when all four directions individually agree -->
        </Input>
    </Condition>
</Condition>
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `any` | string (space-separated generic input names) | one of `any`/`all`/`none` | True when *at least one* named input matches |
| `all` | string (space-separated generic input names) | one of `any`/`all`/`none` | True when *every* named input matches. An empty name list is always false, never vacuously true |
| `none` | string (space-separated generic input names) | one of `any`/`all`/`none` | True when *no* named input matches |
| `match` | `label` \| `mapping` | no | What "matches" means for each name — `label` checks `HasLabel`, `mapping` checks `IsMapped`. Default `label` |

Exactly one of `any`/`all`/`none` must be present; zero or more than one is logged and the whole `<Condition>` (and its children) is skipped.

No positional attributes — a `<Condition>` is transparent for coordinates and slot counting (unlike a nested `<Group>`, which never is — see the tables above). Children: `<Input>`, `<Group>`, `<OneOf>`, `<Condition>` in any order, plus a loose `<Label>` (see [Loose `<Label>`](#loose-label)) — the most common reason to nest one directly in a `<Condition>` rather than inside a wrapping `<Input>`. Unlike `<Group>`, a `<Condition>` has **no** `Overlays` list of its own — it has no dedicated parsing branch for `<Overlay>` the way `<Group>` does, so a bare `<Overlay>` placed directly inside one is logged as an invalid element and dropped. To attach a shared overlay to content a `<Condition>` gates, nest a `<Group>` inside the `<Condition>` and put the `<Overlay>` there instead — the pattern every shipped template already uses.

**Nesting for compound AND logic**: a `<Condition>` only expresses one any/all/none check, so an AND of two independent checks is one `<Condition>` nested inside another — the outer gates on one fact, the inner on another, and both must pass for the innermost children to render. The example above uses this to distinguish "the whole stick collapsed to one shared label" from "all four directions happen to be individually labelled but disagree" — both leave every direction with *some* label, so the inner check alone can't tell them apart; the outer check (whether the whole control's own label exists) is what disambiguates.

If no `<Condition>` (nor any other) alternative in an enclosing `<OneOf>` passes, and there's no unconditional fallback, nothing renders — same as any other `<OneOf>` with no matching alternative.

## File-level conventions

- Element names are case-sensitive. `<input>` is silently ignored.
- Attribute values are parsed culture-invariantly — use `.` as the decimal separator.
- Numbers (`width`, `height`, `gap`, `fontSize`, `minOpacity`, `inactiveBlurRadius`) are `double`. NaN signals "use the natural value" for `width`/`height`.
- Boolean attributes (currently just `collapse`) accept `"true"` case-insensitively. Any other value is treated as `false`.
- Coordinates (`x`, `y`) accept `+N`, `-N`, or plain `N`. A leading `+` makes it relative even when `N` is positive — without the `+`, it's absolute.
- The order of children matters in `<Group>` (slot index), `<OneOf>` (alternative priority), and `<Body>` (document order is preserved through the render output).

## Diagnostic logging

The parser emits errors to the configured `ILogger` for:

- Element with a missing required attribute (e.g. `<Input>` without `name`)
- Element with an unparseable coordinate
- Unknown element where one of `<Head>`, `<Body>`, `<Input>`, `<Overlay>`, `<Label>`, `<Group>`, `<OneOf>`, `<Condition>` was expected
- `<Input style="X">` where `X` isn't a `<Style name="X">` in `<Head>`
- `<Input showIf="X">` where `X` isn't a known mode
- `<Condition>` with zero, or more than one, of `any`/`all`/`none` set (the whole `<Condition>` is skipped)
- `<Condition match="X">` where `X` isn't `label` or `mapping` (falls back to `label`)
- A loose `<Label>` (see [Loose `<Label>`](#loose-label)) with no enclosing `<Input>` at all, ambient or otherwise

Errors don't abort the load — the bad element is skipped (or, for coordinate problems, replaced with `+0`), the rest of the template parses normally. Check the log file after a problem template to see what was dropped.

## Complete example

A minimal template with a single button:

```xml
<ControllerTemplate>
    <Head>
        <Style fontSize="20" inactiveBlurRadius="6" />
    </Head>
    <Body>
        <Input name="ButtonA" x="100" y="100" width="64" height="64">
            <Label x="+0" y="+72" />
        </Input>
    </Body>
</ControllerTemplate>
```

For a full reference, see the `Templates/Xbox Series X/Layout.xml` in this repository — it exercises every concept in this document (Head with named styles, Group with collapse and a shared overlay, OneOf with `Condition`-gated alternatives, nested `Condition` for compound AND logic, `useImage` for asset borrowing, all four `showIf` modes).

## Conventions for new templates

These aren't enforced by the parser, but following them keeps templates legible:

- Order `<Input>` elements roughly by visual position (top to bottom, left to right) when not constrained by slot ordering
- Group related inputs (face buttons, shoulder buttons) with a comment if you're not using `<Group>` itself
- Define named styles in `<Head>` for any combination of `showIf` + `minOpacity` you use more than twice
- Prefer relative coordinates inside `<Group>` so the cluster moves as a unit when you tweak its origin
- Keep `<Overlay>` lines (`Line_ButtonX.png` etc.) at the group level, not duplicated on every Input
- Name every image after the **generic input** it belongs to, never after the markings on the chassis you are drawing. `Line_AxisTriggerLeft.png`, not `LineLT.png` — "LT" is an Xbox label, and a PlayStation template would then need its own `LineL2.png` for the same slot. One vocabulary keeps a template portable and lets images be shared from `Templates/` one level up.
  - Input images take the input name exactly: `ButtonA.png`, `AxisLeftStickUp.png`. These are resolved automatically from `<Input name="…">`.
  - Connector lines take `Line_{GenericInput}.png` and are referenced by `<Overlay src="…">` verbatim.
  - Add `_Multi` for the variant drawn when a cluster shows per-direction labels rather than one glyph: `Line_AxisLeftStick.png` for the single line, `Line_AxisLeftStick_Multi.png` for the fanned-out version.
  - An overlay attached to a `<Group>` serves a cluster and has no single input to name it after. Use the category from [Generic input names](templates.md#generic-input-names) instead — `Line_MetaButtons.png` for the Start/Back cluster.
