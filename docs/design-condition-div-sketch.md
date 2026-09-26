# Design sketch: `<Condition>` and `<Div>` in Layout.xml

**Status: design discussion only. Nothing here is implemented — no resolver changes, no schema doc
updates. This file exists to pressure-test the proposal against a real template section before
committing to `docs/layout-xml-schema.md`.**

## The proposal so far

- **`<Condition>`** — renders a subtree based on explicit `all`/`any`/`none` boolean logic over
  named inputs' mapped/has-label state. Generalizes `<Group AlwaysInclude=false>`'s implicit
  "any descendant visible" rule into something that can express AND/NOT across inputs.
- **`<Div>`** — a positioned (`x`/`y`) + styled container with no input-mapping semantics of its
  own. Optionally carries an `input="X"` attribute that sets the ambient input context for
  descendant `showIf` evaluation — generalizing the existing special case where `<Overlay>`
  inherits `InputName` from an `<Input>` parent (today `null` under `<Group>`/`<Stack>`).
  - Decided: the ambient input propagates *transparently* through `<Group>`/`<Stack>`/`<OneOf>`,
    not just to direct children.
  - Decided: nested `input=` attributes shadow normally — innermost wins.
  - Open: whether a bare `<Condition>` (no explicit input list) defaults to the ambient input.

## The test case: AxisLeftStick's dual-rendering OneOf

This section (`Templates/Xbox Series X/Layout.xml`) is a good stress test because it already does
the thing `Condition` is meant to make explicit — an implicit either/or between "all four
directions have their own label" and "only the whole stick has one" — and it already duplicates
an `Input name="AxisLeftStick"` declaration to get a second, alternate render.

### As it stands today

```xml
<Input name="AxisLeftStick" style="auto-blur" x="539" y="309">
    <Render height="124" width="124" />
    <OneOf>
        <Group>
            <Overlay src="Line_AxisLeftStick_Multi.png" x="386" y="370" />
            <Stack x="312" y="358.5" gap="45" collapse="true" vAlign="center">
                <Input name="AxisLeftStickUp" style="small-label-blur">
                    <Render height="34" width="34" />
                    <Label x="-24" y="+17" align="right" />
                </Input>
                <Input name="AxisLeftStickLeft" style="small-label-blur">
                    <Render height="34" width="34" />
                    <Label x="-24" y="+17" align="right" />
                </Input>
                <Input name="AxisLeftStickRight" style="small-label-blur">
                    <Render height="34" width="34" />
                    <Label x="-24" y="+17" align="right" />
                </Input>
                <Input name="AxisLeftStickDown" style="small-label-blur">
                    <Render height="34" width="34" />
                    <Label x="-24" y="+17" align="right" />
                </Input>
            </Stack>
        </Group>
        <Input name="AxisLeftStick" style="show-if-label" x="307" y="339">
            <Overlay src="Line_AxisLeftStick.png" x="+79" y="+31" />
            <Render height="64" width="64" />
            <Label x="-19" y="+32" align="right" />
        </Input>
    </OneOf>
</Input>
```

To understand *why* the second branch only renders when the first doesn't, a reader has to know
`OneOf`'s rule from prose documentation ("picks first alternative where `AnyVisible` is true") —
it's not visible in the markup itself.

### Rewritten with `Condition` (first attempt wrongly reached for `Div` — see below)

```xml
<Input name="AxisLeftStick" style="auto-blur" x="539" y="309">
    <Render height="124" width="124" />

    <Condition any="AxisLeftStickUp AxisLeftStickLeft AxisLeftStickRight AxisLeftStickDown" match="label">
        <Overlay src="Line_AxisLeftStick_Multi.png" x="386" y="370" />
        <Stack x="312" y="358.5" gap="45" collapse="true" vAlign="center">
            <Input name="AxisLeftStickUp" style="small-label-blur">
                <Render height="34" width="34" />
                <Label x="-24" y="+17" align="right" />
            </Input>
            <Input name="AxisLeftStickLeft" style="small-label-blur">
                <Render height="34" width="34" />
                <Label x="-24" y="+17" align="right" />
            </Input>
            <Input name="AxisLeftStickRight" style="small-label-blur">
                <Render height="34" width="34" />
                <Label x="-24" y="+17" align="right" />
            </Input>
            <Input name="AxisLeftStickDown" style="small-label-blur">
                <Render height="34" width="34" />
                <Label x="-24" y="+17" align="right" />
            </Input>
        </Stack>
    </Condition>

    <Condition none="AxisLeftStickUp AxisLeftStickLeft AxisLeftStickRight AxisLeftStickDown" any="AxisLeftStick" match="label">
        <Input name="AxisLeftStick" style="show-if-label" x="307" y="339">
            <Overlay src="Line_AxisLeftStick.png" x="+79" y="+31" />
            <Render height="64" width="64" />
            <Label x="-19" y="+32" align="right" />
        </Input>
    </Condition>
</Input>
```

## What this actually reveals

**First draft of this sketch put `<Render>` directly inside `<Div>`, on both branches — a bug,
caught in review, not a stylistic choice.** `<Render>`/`<Label>` need real image/label resolution
against a specific input's identity; an element carrying them *is* that input's render slot by
definition, which is precisely what `<Div>`'s "no input-mapping semantics of its own" was supposed
to rule out. Once both render-bearing elements correctly stay `<Input>` — because they both
carry `<Render>` — **`<Div>` doesn't appear anywhere in the corrected rewrite at all.**
`<Condition>` alone replaces `<OneOf>`'s implicit fallthrough; it doesn't need `<Div>` as
scaffolding, because `<Group>` already had no position or input semantics of its own to begin
with.

**This means the AxisLeftStick section is the wrong place to look for `<Div>`'s payoff.** `<Div>`
only earns its place where something needs *position and style but never render/label content* —
a decorative background panel behind a HUD section, or a wrapper that exists purely to position
several already-self-contained children together. Nothing in this section fits: `Group`'s only
job here was visibility (which `Condition` now states explicitly), and every leaf either renders
something (must stay `Input`) or already carries its own `x`/`y` (`Overlay`, `Stack`). The
Xbox Series X template may simply not have a `<Div>`-shaped gap in it yet — worth checking a
different template, or a hypothetical one, before assuming `<Div>` is broadly useful rather than
narrowly.

**The explicit `Condition` pair is more self-documenting than the `OneOf` fallthrough**, and it
would generalize better if a *third* state ever needed handling (e.g. "two directions labelled,
two blank" — today buried inside `WholeInputDeriver`/`CollapseWholeDirections` semantics, not
expressible in the layout at all). But it's also more verbose — two `Condition` blocks with
near-duplicate input lists versus one `OneOf`/`Group` pair. Whether that verbosity is worth it
probably depends on how often templates need a *third* branch, not just two.

**The `Overlay` `InputName` question is now moot for this example.** With no `Div` in the
rewrite, `Line_AxisLeftStick_Multi.png`'s `Overlay` is parented directly to the `Condition`
(or whatever `Condition` desugars to) exactly as it was to `Group` before — still not parented to
an `Input`, so `InputName` stays `null`, unchanged from today. The ambient-input-propagation
question (transparent through `Group`/`Stack`/`OneOf`, decided earlier) only bites once `Div`
*is* actually in the tree somewhere — it wasn't exercised by this example at all.

## Open questions this sketch surfaces

1. Does `Condition`'s `any`/`none` list take space-separated input names (as sketched, matching
   how `<Input name="A B">` already encodes combinations), or a nested per-input structure?
   Space-separated is terser and consistent with existing combination syntax, but nested elements
   would let each input carry its own `match="label"` vs `match="mapping"` if that's ever needed
   per-input rather than once per `Condition`.
2. Is a `Condition`-pair verbose enough, for the common two-branch case, that `OneOf` should stay
   as the ergonomic shorthand and `Condition` reserved for cases `OneOf`/`Group` genuinely can't
   express (three+ branches, cross-cutting AND/NOT)? I.e. do both coexist rather than `Condition`
   replacing `OneOf` outright?
3. `<Div>` found no use case in this particular section. Before finalizing the schema, find (or
   construct) a template scenario that actually needs a positioned, style-bearing, render-free
   wrapper — otherwise `<Div>` risks shipping as speculative surface area with no real consumer.
