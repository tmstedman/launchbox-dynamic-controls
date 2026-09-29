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
| `<Overlay>` inside a `<Container>` | The Container's `x`/`y` |
| `<Input>` inside a `<Container>` | The Container's `x`/`y` plus `slotIndex × gap` |
| `<Input>` inside a nested `<Container>` inside a `<Container>` | The inner Container's own `x`/`y` plus its own `slotIndex × gap` (a nested Container is never transparent — it establishes its own origin) |
| `<Input>` inside a `<OneOf>` inside a `<Container>` | The OneOf's slot origin (each alternative shares one slot) |
| `<Input>` inside a `<Condition>` inside a `<Container>` | The outer Container's `x`/`y` plus `slotIndex × gap` (Condition is transparent) |

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
- An `<Input>` or `<Container>` — governs an Input's own image, and is inherited by whatever's reachable from it that doesn't set its own (see [Style cascade](#style-cascade))
- An `<Overlay>` — wins over the inherited value

### Style cascade

`style`, `showIf`, `minOpacity`, `inactiveBlurRadius`, and `fontSize` all resolve through the same cascade, and every element that carries visual attributes participates in it — `<Input>` and `<Container>` as origins whose result flows down to whatever they contain, `<Overlay>`/`<Label>` as the leaves at the end of it. Each attribute is resolved in priority order:

1. **Explicit attribute on the element itself** — `<Input minOpacity="0.5">`, `<Container style="foo">`
2. **Named style reference** — `style="foo"` looks up the `<Style name="foo">` in `<Head>`
3. **Whatever's already ambient** — the nearest enclosing `<Input>`/`<Container>`'s own resolved value, itself resolved the same way
4. **Template default** — the unnamed `<Style>` in `<Head>` (`fontSize`/`minOpacity`/`inactiveBlurRadius` only — the unnamed form doesn't carry `showIf`, see `<Style>` below)
5. **Built-in default** — `fontSize=28`, `minOpacity=0`, `inactiveBlurRadius=0`, `showIf` *(omitted)*

Explicit attributes always win. Use a named style, or a `<Container>`'s own attributes, to share visual treatment across many inputs without repeating it on each one.

**One exception**: a `<Container>` that sets nothing of its own for `showIf`/`minOpacity`/`inactiveBlurRadius` does *not* pass an ambient value through to its members (tier 3 is skipped for those three attributes specifically, though not for `fontSize`, which always keeps falling through). A `<Container>` exists to declare a shared value *for its own members*, not to relay whatever its enclosing `<Input>` happened to set for a completely different purpose — an analog stick's own `auto-blur` fade, say, isn't meant to reach four levels down into a `small-label-vacate` direction inside it and turn its vacate-to-zero into a fade instead. A `<Container>` that *does* set its own explicit value still passes it to members exactly as you'd expect; only an empty one stops being a pass-through for someone else's ambient value.

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

The container for everything the renderer cares about. Direct children are `<Input>`, `<Container>`, `<OneOf>`, and `<Condition>`, in document order. A bare `<Label>` parses here too, but always errors — there's no enclosing `<Input>` for it to attach to at the very top of the tree (see [Loose `<Label>`](#loose-label)). A bare `<Overlay>` also parses here, but does **not** error — with no enclosing `<Input>`/`<Container>` at all it simply renders unconditionally (see [Loose `<Overlay>`](#loose-overlay)).

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
| `showIf` | enum | no | Governs this Input's own image; falls through to whatever's reachable from it that sets its own (`<Overlay>`, a nested `<Input>`, ...) — see [Style cascade](#style-cascade) |
| `minOpacity` | double | no | Governs this Input's own image; falls through the same way |
| `inactiveBlurRadius` | double | no | Governs this Input's own image; falls through the same way |
| `fontSize` | double | no | Falls through to `<Label>` the same way |
| `x` | coordinate | no | Also the position of this Input's own image. Origin for nested elements with relative coords. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |
| `width` | double | no | This Input's own image render width. NaN = use the image's natural width |
| `height` | double | no | Same, height |
| `useImage` | string | no | Override image filename for this Input's own image (no extension; `.png` appended). Affects asset-borrowing semantics — see below |

**`useImage` and asset borrowing**: When `useImage` is set, the Input is "borrowing" another input's artwork. The image resolution chain still applies, so a borrowed image gets its platform-specific variant even when the *borrowing* input isn't mapped. This is how `AxisRightStickUp` shows the same up-arrow as `AxisLeftStickUp` without copying the asset.

**Children** (any combination, any order):
- `<Label>` — label text
- `<Overlay>` — additional image
- `<Input>`, `<Container>`, `<OneOf>`, `<Condition>` — nested layout

**Nested Input semantics**: A nested `<Input>` inside another Input establishes a parent-child relationship. The parent's `showIf`/`minOpacity`/`inactiveBlurRadius`/`fontSize` are what the child falls through to via the [style cascade](#style-cascade) when the child sets none of its own — same as a `<Label>`/`<Overlay>` reachable from the parent would. Nesting one `<Input>` inside another is unusual in practice, though — the shipped template no longer does it at all.

**This is *not* how a whole control's `showIf="auto"` fan-out works, even though it used to be.** `ButtonDpad`/`AxisLeftStick`/`AxisRightStick` light up when any of their four direction inputs has a label or mapping, but that fan-out is sourced from a fixed name-to-parts table (`WholeInputs.PartsOf` in the engine), not from `Children` nesting — so the direction inputs live as top-level *siblings* of their whole's own bare `<Input>`, not nested inside it:

```xml
<Input name="AxisLeftStick" style="auto-blur" x="539" y="309" height="124" width="124" />
<OneOf>
    <!-- direction alternatives, each its own top-level Input -->
</OneOf>
```

See [`<Container>`'s `for=`](#container--positioned-cluster) for how a loose `<Label>` inside still finds its whole with no enclosing Input to supply it ambiently.

**Strict-self render position**: A duplicate top-level `<Input>` with no nested children expresses "render the parent input's image at this position, independent of its descendants" — used by some templates to put an extra render in a different slot.

### Loose `<Label>`

A `<Label>` doesn't have to be a *direct* child of its own `<Input>` — it can also appear inside a `<Container>`, `<OneOf>`, or `<Condition>` that's itself somewhere inside that Input, most usefully nested inside a `<Condition>` to gate a label independently of its Input's other content:

```xml
<Input name="ButtonDpad" height="135" width="135">
    <Condition any="SomeOtherInput" match="label">
        <Label x="+0" y="+72" />
    </Condition>
</Input>
```

The loose `<Label>` attaches to whichever `<Input>` is ambient at that point in the tree — here, `ButtonDpad`, even though it's several levels of `<Condition>` away — exactly as if it had been written as a direct child. Entering a *nested* `<Input>` resets this ambient identity to the nested one; entering `<Container>`/`<OneOf>`/`<Condition>` does not, since none of those are a new Input's own boundary. Its coordinate origin, though, follows whatever `<Container>` it's actually inside: `<Condition>` passes the origin through completely unchanged, and `<OneOf>` shares one slot's origin across every alternative, but a `<Container>` always establishes its own frame — see below.

Whether it renders at all is decided once, structurally, the same way a `<OneOf>`'s alternatives are: reaching a `<Condition>` that fails drops everything inside it, the loose label included, before label-specific concerns (its own position, font size) ever come into play. A loose `<Label>` with **no** enclosing `<Input>` at all — not even an ambient one, e.g. one sitting directly under `<Body>` or inside a top-level `<Container>`/`<Condition>` with no `<Input>` anywhere above it — is a template-authoring error, logged once at load time; nothing is rendered — **unless** the nearest enclosing `<Container>` sets `for="SomeInput"`, which supplies the ambient identity explicitly for exactly this case: no enclosing `<Input>` to have supplied it the ordinary way. Deliberately not an attribute on `<OneOf>`/`<Condition>` — those are pure control-flow, unrelated to identity; `<Container>` already carries the rest of an Input's structural attributes. See [`<Container>`](#container--positioned-cluster).

**A loose `<Label>` centers itself against an enclosing `<Container>`.** Placed directly inside a `<Container>` (or reached through a `<OneOf>`/`<Condition>` nested in it), its `y` resolves to the visual center of that Container's slots — the midpoint between the first and last slot that's actually showing *for the current game* — rather than the shifted per-slot origin the Container's own members use:

```xml
<Input name="AxisLeftStick" style="auto-blur" x="539" y="309" height="124" width="124" />
<OneOf>
    <Condition any="AxisLeftStick" match="label">
        <Container for="AxisLeftStick" x="312" y="358.5" gap="45" collapse="true" vAlign="center">
            <Input name="AxisLeftStickUp" style="small-label-vacate" height="34" width="34" />
            <Input name="AxisLeftStickLeft" style="small-label-vacate" height="34" width="34" />
            <Input name="AxisLeftStickRight" style="small-label-vacate" height="34" width="34" />
            <Input name="AxisLeftStickDown" style="small-label-vacate" height="34" width="34" />
            <Label x="-24" align="right" />
        </Container>
    </Condition>
</OneOf>
```

The `<Container>`'s `for="AxisLeftStick"` is what makes this work with no enclosing `<Input>` at all — it supplies the ambient identity the loose `<Label>` attaches to, exactly as if it had been written as `AxisLeftStick`'s own direct child.

This lands the label "half-way up the container" — and it stays there regardless of how many of the four directions are actually present this game, *for every `vAlign` value*, not only `"center"`. Which slots survive `collapse="true"` is a per-game fact (this game's mapping/labels decide it), so the center is computed at render time rather than baked in once at template load — see [`<Container>`](#container--positioned-cluster) for the container's own static shape, and the engine's `LayoutFilter.ResolveLooseLabel` for the computation itself. Without `collapse="true"`, nothing ever varies by game (slots never vacate), so the center is simply the container's full, fixed slot count every time. A direct child `<Label>` on one of the Container's own *members* is unaffected by any of this — it keeps resolving against that member's own slot position, exactly as if the Container weren't there, since a nested `<Input>` resets the ambient anchor to its own.

**A *nested* `<Container>` is never transparent for this anchor**, even one with no `x`/`y`/`gap`/`vAlign` of its own: it always establishes its own frame, so a loose `<Label>` placed directly inside it centers on *that* Container's own slots — not the outer Container's. Only `<OneOf>` and `<Condition>` stay genuinely transparent for the anchor.

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
| `style` | string | no | Named style reference — see [Style cascade](#style-cascade) |
| `showIf` | enum | no | Falls through the cascade when unset — see [Style cascade](#style-cascade) |
| `minOpacity` | double | no | Same |
| `inactiveBlurRadius` | double | no | Same |

**Placement**: Overlays can be direct children of `<Input>` (visibility inherits from the Input) or `<Container>` (rendered once whenever the container itself is reached — see [`<Container>`](#container--positioned-cluster)), or loose — see [Loose `<Overlay>`](#loose-overlay).

### Loose `<Overlay>`

An `<Overlay>` doesn't have to be a *direct* child of its own `<Input>`/`<Container>` — like a loose `<Label>`, it can also appear inside a `<Container>`, `<OneOf>`, or `<Condition>` reached some other way, most usefully nested inside a `<Condition>` to gate an extra decoration independently of its owner's other content:

```xml
<Input name="ButtonA" showIf="mapping">
    <Condition any="ButtonA" match="mapping">
        <Overlay src="extra-glow.png" x="+5" y="+5" />
    </Condition>
</Input>
```

Ownership resolves in strict priority order, the same recursive walk that finds a loose `<Label>`'s owning `<Input>`:

1. **An ambient `<Input>`** — if one is reached on the way down (nested inside it directly, or via `<Container>`/`<OneOf>`/`<Condition>` between), the loose `<Overlay>` joins that Input's own `Overlays`, rendered with that Input's own visibility flags, exactly as if it had been a direct child. A `<Container for="SomeInput">` that successfully resolves counts as supplying this too — everything inside it sees `SomeInput` as ambient, the same as if it had an enclosing `<Input>` the ordinary way.
2. **Else an ambient `<Container>`** — if no `<Input>` is ambient (no enclosing one, and no `for=` that resolved), but a `<Container>` is, the loose `<Overlay>` joins that Container's own `Overlays`, folded into the same aggregate-flagged group-overlay emission its direct children already get.
3. **Else neither** — a bare `<Overlay>` sitting directly under `<Body>`, or inside a `<OneOf>`/`<Condition>` with no `<Input>`/`<Container>` ancestor at all, renders unconditionally. Unlike a loose `<Label>` in the same position, this is **not** a template-authoring error: an Overlay's position never depended on an owner to begin with (it resolves against whatever origin is ambient, same as always — see [Coordinates](#coordinates)), and there's simply no fold-in target for visibility, so it renders as its own template-level overlay. Its own `showIf` still governs its own opacity — with no flags to check, only `showIf="always"` (the default) ever actually shows something.

Whether it's reached at all is decided the same way as everything else: a `<Condition>` that fails drops it along with everything else inside, before any of the above ownership logic runs.

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
| `style` | string | no | Named style reference, for `fontSize` only — a Label has no `minOpacity`/`inactiveBlurRadius` of its own |
| `fontSize` | double | no | Falls through the cascade when unset — see [Style cascade](#style-cascade) |

A label with an unparseable coordinate logs the error and keeps the default (+0).

### `<Container>` — positioned cluster

A vertical list of inputs, each spaced `gap` pixels below the last. Which slot sits at `y` itself depends on `vAlign`: by default (`vAlign="top"`) the first child sits at the Container's `(x, y)`, the second at `(x, y + gap)`, the third at `(x, y + 2×gap)`, and so on.

**A `<Container>` never decides its own inclusion.** This used to be a `<Group>`, which — alongside the positioning below — also excluded itself entirely from `inputsToRender` when no descendant had a visible render. That implicit check is gone: a `<Container>` is *always* rendered once reached, and whether it exists at all this game is now exclusively an explicit wrapping `<Condition>`'s job (see [`<Condition>`](#condition--explicit-named-input-gate)). This matters for two different reasons depending on where the `<Container>` sits:

- **As a `<OneOf>` alternative**: a bare `<Container>` can never be selected at all — `AnyVisible` has no case for it, so it always reports "not visible" and the `<OneOf>` skips past it. Wrap it in a `<Condition>` naming whatever should make it eligible (see the fallback alternative in the directional-input example below).
- **As a top-level element** (or anywhere else reached unconditionally): with no wrapping `<Condition>`, the `<Container>` and its members always render — each member individually faded per its own `showIf` rather than the whole cluster disappearing. If you want the old "gone entirely when nothing's labelled" behaviour, wrap it in an explicit `<Condition>` naming its members (see the face-buttons example below).

Two purposes remain:

1. **Shared overlays**: an `<Overlay>` declared at the container level renders once whenever the container itself is reached, instead of being repeated on every member.
2. **Shared style**: `style`/`showIf`/`minOpacity`/`inactiveBlurRadius`/`fontSize` declared on the Container flow to its member Inputs and Overlay children the same way an Input's own attributes flow to its Labels and Overlays — see [Style cascade](#style-cascade), including the one exception that applies only here (an *empty* Container doesn't relay a wrapping Input's own ambient value through to members).

**These two don't always compose for free.** A direct-child `<Overlay>` cascades style from the Container exactly like a member Input does — so if the overlay sets no `showIf`/`minOpacity`/`inactiveBlurRadius` of its own (the common case for a decorative line/frame), it silently starts inheriting whatever style you hoist onto the Container for the *members'* sake. If that's not what you want, move the `<Overlay>` to sit *outside* the Container instead — as a loose `<Overlay>` sibling (see [Loose `<Overlay>`](#loose-overlay)), still inside the same wrapping `<Condition>` so its existence-gating is unchanged. Position is unaffected if it's already absolute; a relative one needs converting, since it'll resolve against a different ambient origin outside the Container.

```xml
<!-- Explicit Condition replaces the old <Group>'s implicit "any member visible" check -->
<Condition any="ButtonDpadUp ButtonDpadLeft ButtonDpadRight ButtonDpadDown" match="label">
    <Container x="312" y="291" gap="45" collapse="true">
        <Overlay src="Line_ButtonDpad_Multi.png" x="+72" y="-45" />
        <Input name="ButtonDpadUp">...</Input>
        <Input name="ButtonDpadLeft">...</Input>
        <Input name="ButtonDpadRight">...</Input>
        <Input name="ButtonDpadDown">...</Input>
    </Container>
</Condition>
```

| Attribute | Type | Required | Notes |
|---|---|---|---|
| `x` | coordinate | no | Container origin. Default `+0` |
| `y` | coordinate | no | Same. Default `+0` |
| `gap` | double | no | Vertical spacing between children. Default 0 |
| `vAlign` | `top` \| `bottom` \| `center` | no | Which slot `y` refers to. Default `top` — see below |
| `collapse` | bool (`true`/anything-else) | no | When `true`, hidden children vacate their slot and later children shift up to close the gap |
| `style` | string | no | Named style reference — see [Style cascade](#style-cascade) |
| `showIf` | enum | no | Default for member Inputs/Overlays that don't set their own — see [Style cascade](#style-cascade) |
| `minOpacity` | double | no | Same |
| `inactiveBlurRadius` | double | no | Same |
| `fontSize` | double | no | Same |
| `for` | string | no | Names the Input this Container builds on behalf of, when it has no enclosing `<Input>` of its own — e.g. a top-level Container inside a `<OneOf>` sibling of the whole it describes (see [Nested Input semantics](#input--a-generic-input)). Supplies the ambient identity a loose `<Label>` placed directly inside would otherwise have needed an enclosing `<Input>` for; unnecessary (and should be left unset) when the Container is already reached through one. Unrelated to `style`/`showIf`/etc. above — those cascade visual defaults, this carries identity |

**`vAlign`** shifts the container's whole origin *before* slots are laid out, so it changes where every child ends up, not just one of them:

- `top` (default) — `y` is the first slot; children fall below it, same as always.
- `bottom` — `y` is the *last* slot; children are laid out so the last one lands exactly on `y`, with earlier ones above it.
- `center` — `y` is the midpoint between the first and last slot.

The slot count used for this is the template's fixed slot count (the same one in the table below), computed once when the template loads and cached from then on — not how many children happen to be visible at render time. On its own that's exactly right, since without `collapse` every slot always renders (just possibly faded), so the fixed count and the actual count never differ.

Combined with `collapse`, they can differ — a vacated slot means fewer are actually left than `vAlign` was anchored against. Rather than drift toward `top` as slots vacate, the anchor is corrected at render time: the same `vAlign` shift is re-derived against however many slots collapse has actually left, and the difference from the fixed-count shift is folded uniformly into every remaining child's position, on top of collapse's own per-vacancy shift. The net effect: `bottom`/`center` stay pinned to the declared `y` no matter how many children are actually showing.

This is exactly what makes a loose `<Label>` placed directly in the Container useful for a single label shared across a collapsing cluster — see [Loose `<Label>`](#loose-label) — its position is the declared `(x, y)` itself (never shifted), so it inherits this same no-drift guarantee for free, with no per-game recalculation of its own.

A Container with only one slot renders identically under every `vAlign` value, since there's nothing to distribute around.

An unrecognized `vAlign` value logs an error and falls back to `top`.

**Children** can be `<Input>`, `<Container>`, `<OneOf>`, `<Condition>`, `<Overlay>` in any order, plus a loose `<Label>` (see [Loose `<Label>`](#loose-label)) or a loose `<Overlay>` reached through a nested `<OneOf>`/`<Condition>` (see [Loose `<Overlay>`](#loose-overlay)) — a direct-child `<Overlay>` still always lands on this Container's own `Overlays` list, never the loose path.

**How children occupy slots** — each child takes one position in the vertical list, except:

| Child kind | Slot behaviour |
|---|---|
| `<Input>` | Takes one slot |
| `<Container>` (nested) | Takes one slot as a block; the inner Container positions its own children independently — never transparent, even with no `x`/`y`/`gap` of its own |
| `<OneOf>` | Takes one slot; all its alternatives share that same position |
| `<Condition>` | Transparent — its children each take their own slot as if the Condition wasn't there |
| `<Overlay>` | Takes no slot — positioned at its own coordinates regardless |
| `<Label>` (loose) | Takes no slot, same as `<Overlay>` |

**Collapse** (`collapse="true"`) removes the gap left by hidden children. When a child's renders are all invisible, it vacates its slot and everything below shifts up by `gap`. Without collapse, slots are always fixed — a hidden child leaves a faded image or blank space.

### `<OneOf>` — mutually-exclusive alternatives

A container where only the first alternative whose visibility check passes is rendered; the rest are dropped entirely from `inputsToRender`. Used for "render the cluster of dpad labels, or render the single dpad icon, never both".

```xml
<OneOf>
    <Condition any="ButtonDpadUp ButtonDpadDown" match="label">
        <Container>
            <!-- a labelled cluster -->
            <Input name="ButtonDpadUp">...</Input>
            <Input name="ButtonDpadDown">...</Input>
        </Container>
    </Condition>
    <Input name="ButtonDpad" style="show-if-label">
        <!-- a single icon as fallback -->
    </Input>
</OneOf>
```

No attributes — a `<OneOf>` is pure control-flow (alternative selection), unrelated to identity or position. Children: `<Input>`, `<Container>`, `<OneOf>`, `<Condition>` in document order (the first-match-wins ordering is significant). A bare `<Label>` or `<Overlay>` parses here too (see [Loose `<Label>`](#loose-label)/[Loose `<Overlay>`](#loose-overlay)), but either is a degenerate alternative — neither ever counts as visible on its own (see below), so an alternative that's just a loose Label/Overlay is only useful for what it carries, never for "winning" the `<OneOf>`.

**Visibility check per alternative**:
- `<Input>` — "any-render-visible" (its own image passes its `showIf`, or recursively, one of its structural children does)
- `<Condition>` — its own `all`/`any`/`none` check against its named inputs (see below), ignoring what its children render
- `<Container>` — **never visible on its own** (see [`<Container>`](#container--positioned-cluster)) — a bare `<Container>` alternative can never be selected; wrap it in a `<Condition>` naming whatever should make it eligible
- `<Label>` (loose) — always false; it isn't a visibility-bearing alternative in its own right

If no alternative passes, the OneOf renders nothing — all alternatives are dropped.

### `<Condition>` — explicit named-input gate

A container whose children render only when an explicit `all`/`any`/`none` check passes against named generic inputs' label or mapping state. `<Condition>` names its own inputs and checks them directly, by dictionary lookup, regardless of what its children are — this is what lets it gate a subtree on an input that isn't (or isn't only) one of that subtree's own inputs. It's also the *only* inclusion-deciding mechanism in the schema now: a `<Container>` has no implicit visibility check of its own (see [`<Container>`](#container--positioned-cluster)), so anything that needs to exist conditionally — a `<OneOf>` alternative, a top-level cluster — wraps it in an explicit `<Condition>` rather than relying on any structural fold-in.

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
| `match` | `label` \| `mapping` \| `auto` | no | What "matches" means for each name — `label` checks `HasLabel`, `mapping` checks `IsMapped`, `auto` checks `HasLabel` when the game contributed its own labels and `IsMapped` otherwise (mirrors `showIf="auto"` — see [`showIf` modes](#showif-modes)). Default `label` |

Exactly one of `any`/`all`/`none` must be present; zero or more than one is logged and the whole `<Condition>` (and its children) is skipped.

No positional attributes — a `<Condition>` is transparent for coordinates and slot counting (unlike a nested `<Container>`, which never is — see the tables above). Children: `<Input>`, `<Container>`, `<OneOf>`, `<Condition>` in any order, plus a loose `<Label>` (see [Loose `<Label>`](#loose-label)) or a loose `<Overlay>` (see [Loose `<Overlay>`](#loose-overlay)) — the most common reason to nest either directly in a `<Condition>` rather than inside a wrapping `<Input>`/`<Container>`. Unlike `<Container>`, a `<Condition>` has **no** `Overlays` list of its own — but a bare `<Overlay>` placed directly inside one is not an error: it's parsed the same loose way a bare `<Label>` is, and joins whichever `<Input>`/`<Container>` is ambient once one is actually reached.

**Nesting for compound AND logic**: a `<Condition>` only expresses one any/all/none check, so an AND of two independent checks is one `<Condition>` nested inside another — the outer gates on one fact, the inner on another, and both must pass for the innermost children to render. The example above uses this to distinguish "the whole stick collapsed to one shared label" from "all four directions happen to be individually labelled but disagree" — both leave every direction with *some* label, so the inner check alone can't tell them apart; the outer check (whether the whole control's own label exists) is what disambiguates.

If no `<Condition>` (nor any other) alternative in an enclosing `<OneOf>` passes, and there's no unconditional fallback, nothing renders — same as any other `<OneOf>` with no matching alternative.

## File-level conventions

- Element names are case-sensitive. `<input>` is silently ignored.
- Attribute values are parsed culture-invariantly — use `.` as the decimal separator.
- Numbers (`width`, `height`, `gap`, `fontSize`, `minOpacity`, `inactiveBlurRadius`) are `double`. NaN signals "use the natural value" for `width`/`height`.
- Boolean attributes (currently just `collapse`) accept `"true"` case-insensitively. Any other value is treated as `false`.
- Coordinates (`x`, `y`) accept `+N`, `-N`, or plain `N`. A leading `+` makes it relative even when `N` is positive — without the `+`, it's absolute.
- The order of children matters in `<Container>` (slot index), `<OneOf>` (alternative priority), and `<Body>` (document order is preserved through the render output).

## Diagnostic logging

The parser emits errors to the configured `ILogger` for:

- Element with a missing required attribute (e.g. `<Input>` without `name`)
- Element with an unparseable coordinate
- Unknown element where one of `<Head>`, `<Body>`, `<Input>`, `<Overlay>`, `<Label>`, `<Container>`, `<OneOf>`, `<Condition>` was expected
- `style="X"` (on `<Input>`, `<Container>`, `<Overlay>`, or `<Label>`) where `X` isn't a `<Style name="X">` in `<Head>`
- `<Input showIf="X">` (or `<Container>`/`<Overlay>`) where `X` isn't a known mode
- `<Condition>` with zero, or more than one, of `any`/`all`/`none` set (the whole `<Condition>` is skipped)
- `<Condition match="X">` where `X` isn't `label`, `mapping`, or `auto` (falls back to `label`)
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

For a full reference, see the `Templates/Xbox Series X/Layout.xml` in this repository — it exercises every concept in this document (Head with named styles, Container with collapse and a shared overlay, `Condition`-wrapped Containers, OneOf with `Condition`-gated alternatives, nested `Condition` for compound AND logic, `useImage` for asset borrowing, all four `showIf` modes).

## Conventions for new templates

These aren't enforced by the parser, but following them keeps templates legible:

- Order `<Input>` elements roughly by visual position (top to bottom, left to right) when not constrained by slot ordering
- Group related inputs (face buttons, shoulder buttons) with a comment if you're not using `<Container>` itself
- Define named styles in `<Head>` for any combination of `showIf` + `minOpacity` you use more than twice
- Prefer relative coordinates inside `<Container>` so the cluster moves as a unit when you tweak its origin
- Keep `<Overlay>` lines (`Line_ButtonX.png` etc.) at the container level, not duplicated on every Input
- Wrap a `<Container>` in an explicit `<Condition>` whenever it should disappear entirely rather than fade — a `<Container>` never decides this on its own (see [`<Container>`](#container--positioned-cluster))
- Name every image after the **generic input** it belongs to, never after the markings on the chassis you are drawing. `Line_AxisTriggerLeft.png`, not `LineLT.png` — "LT" is an Xbox label, and a PlayStation template would then need its own `LineL2.png` for the same slot. One vocabulary keeps a template portable and lets images be shared from `Templates/` one level up.
  - Input images take the input name exactly: `ButtonA.png`, `AxisLeftStickUp.png`. These are resolved automatically from `<Input name="…">`.
  - Connector lines take `Line_{GenericInput}.png` and are referenced by `<Overlay src="…">` verbatim.
  - Add `_Multi` for the variant drawn when a cluster shows per-direction labels rather than one glyph: `Line_AxisLeftStick.png` for the single line, `Line_AxisLeftStick_Multi.png` for the fanned-out version.
  - An overlay attached to a `<Container>` serves a cluster and has no single input to name it after. Use the category from [Generic input names](templates.md#generic-input-names) instead — `Line_MetaButtons.png` for the Start/Back cluster.
