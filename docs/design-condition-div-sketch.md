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

### Rewritten with `Div`/`Condition`

```xml
<Div input="AxisLeftStick" style="auto-blur" x="539" y="309">
    <Render height="124" width="124" />

    <Condition any="AxisLeftStickUp AxisLeftStickLeft AxisLeftStickRight AxisLeftStickDown" match="label">
        <Div x="386" y="370">
            <Overlay src="Line_AxisLeftStick_Multi.png" />
        </Div>
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
        <Div x="307" y="339" style="show-if-label">
            <Overlay src="Line_AxisLeftStick.png" x="+79" y="+31" />
            <Render height="64" width="64" />
            <Label x="-19" y="+32" align="right" />
        </Div>
    </Condition>
</Div>
```

## What this actually reveals

**The gain is real but narrower than it first looks.** The per-direction `<Input>` elements
*stay* `<Input>` — they each resolve their own generic-input image/label, which `Div`'s ambient
`input=` context was never meant to provide (it only feeds `showIf`, not image resolution). `Div`
replaces `Group` and the second `<Input name="AxisLeftStick" style="show-if-label">`, but that
second one only becomes a `Div` because it's a pure position+style+decoration wrapper for content
(`Overlay`, `Render`, `Label`) that's already scoped by the outer `Div`'s ambient
`input="AxisLeftStick"` — it no longer needs to redeclare the name.

**The explicit `Condition` pair is more self-documenting than the `OneOf` fallthrough**, and it
would generalize better if a *third* state ever needed handling (e.g. "two directions labelled,
two blank" — today buried inside `WholeInputDeriver`/`CollapseWholeDirections` semantics, not
expressible in the layout at all). But it's also more verbose — two `Condition` blocks with
near-duplicate input lists versus one `OneOf`/`Group` pair. Whether that verbosity is worth it
probably depends on how often templates need a *third* branch, not just two.

**A real, previously-invisible consequence: `Overlay` `InputName` changes.** Today,
`Line_AxisLeftStick_Multi.png`'s `Overlay` is parented to a `<Group>`, so its `InputName` is
`null` per the existing rule — it isn't associated with any specific input for rendering or for
E2E `ShouldHaveImage` assertions. Under the rewrite, if that `Overlay` sits inside the outer `Div
input="AxisLeftStick"` (even transparently, through the `Condition`), it would newly resolve to
`InputName = "AxisLeftStick"`. That's arguably *more correct* — the line genuinely belongs to that
input — but it's a behavior change existing E2E fixtures assert against
(`tests/Core.IntegrationTests/EndToEnd/*Tests.cs` `ShouldHaveImages(new(Input: null, Src:
"Line_AxisLeftStick_Multi.png"), ...)` calls would need `Input: "AxisLeftStick"` instead). Worth
deciding deliberately rather than discovering it as an incidental side effect of a refactor.

## Open questions this sketch surfaces

1. Does `Condition`'s `any`/`none` list take space-separated input names (as sketched, matching
   how `<Input name="A B">` already encodes combinations), or a nested per-input structure?
   Space-separated is terser and consistent with existing combination syntax, but nested elements
   would let each input carry its own `match="label"` vs `match="mapping"` if that's ever needed
   per-input rather than once per `Condition`.
2. Should the `Overlay` `InputName` behavior change (null → resolved) ship as part of this, or
   should ambient-context propagation explicitly *stop* at `Overlay` to preserve today's
   behavior? This needs a decision, not a default.
3. Is a `Condition`-pair verbose enough, for the common two-branch case, that `OneOf` should stay
   as the ergonomic shorthand and `Condition` reserved for cases `OneOf`/`Group` genuinely can't
   express (three+ branches, cross-cutting AND/NOT)? I.e. do both coexist rather than `Condition`
   replacing `OneOf` outright?
