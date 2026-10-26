# Label sources

Inputs the shipped label files are built from. Nothing here is packaged — the release zip
carries `assets/` only.

A source is either **transcribed** into a file here, when it is bulk data a rule change would
make us re-read (`arcade-artwork.xml`), or **recorded** in a provenance note, when it is a pile
of scans nobody will re-derive mechanically (`genesis-manuals.md`). Both answer the same
question — what does this label rest on — for sources too different to share one shape.

## `arcade-artwork.xml`

What the controller artwork says, transcribed and nothing more. One `<Game>` per image, one
`<Label>` per control the artwork draws a line to, holding the text exactly as printed.

**This file records what is drawn, not what should ship.** Labels are not collapsed, shortened,
spell-corrected, reconciled against the MAME controls database, or mapped to emulator ports.
Every one of those is a judgement that can change, and keeping them downstream means a rule
change never costs another read of 3,571 images.

The exception is a card that is itself wrong — a misspelling, a control it leaves unlabelled, a
label on a control the cabinet does not have or on the wrong one. Those are corrected here once a second look at the
image has settled that the card, not our reading, is at fault, and the diff against
`arcade-artwork-original.xml` (below) is then the list to send back to the artwork's author.

Controls are named with the plugin's own generic inputs — the vocabulary in `Layout.xml`.
A control the artwork leaves blank is simply absent. A directional cluster carries either one
label for the whole control or one per direction; transcribe whichever is drawn.

### `<Note>`: when a `<Label>` doesn't fit

Some cards print text a `<Label>` can't hold: a footnote below the whole diagram instead of a
line to one icon, or a control with no clear match among the generic inputs. Record what's on
the card and where it sits, in a `<Note>` on the game, and move on — never fold it into a guessed
`<Label>`. A judgement about what it means belongs downstream, in `docs/label-import.md`'s rules,
where the MAME cfg and controls database are also in view. Guessing at transcription time throws
that context away before it's even consulted.

Ace Attacker is the case that forced this: its D-pad carries no line to any icon, only a footnote
below the diagram reading *"(UP&DOWN) Hit Ball - (LEFT&RIGHT) Save Ball"*. Recorded as a `<Note>`,
not split into four guessed `<Label>`s.

```xml
<Game romName="bradley" title="Bradley Trainer">
    <Label control="ButtonRightShoulder">7.62mm Machine Gun</Label>
    <Label control="AxisLeftStick">Move</Label>
    <Label control="ButtonDpadUp">High Rate Armor Peircing</Label>
    <Label control="ButtonDpadLeft">Low Rate Armor Peircing</Label>
    <Label control="ButtonDpadDown">Single Armor Peircing</Label>
</Game>
```

Bradley Trainer shows both variations at once: its D-pad carries a label per direction rather
than one for the cluster, and its artwork misspells *Piercing*. Both are transcribed as drawn —
the spelling is corrected when the shipped file is built, not here.

### Transcribing

The loop, one batch at a time:

```sh
python3 ~/.work/dynamic-controls/scripts/next_batch.py 25   # what to read, and where to write it
#   ... read those images in ONE message, write the batch file ...
python3 ~/.work/dynamic-controls/scripts/merge_artwork.py   # fold it in, delete the batch
python3 ~/.work/dynamic-controls/scripts/check_artwork.py   # verify
```

Batches are written to `batches/` and merged separately so a bad one can be deleted and redone
without touching anything already transcribed. A game that is already present is never
overwritten — a duplicate means the same image was read twice and only a person can say which
reading is right, so the merge leaves both in place and says so.

Reading is mechanical, so the instruction is short:

> For each image, output the ROM name, the title, and every label the artwork prints, each
> against the control it points to. Use the control vocabulary above. Copy the text exactly,
> including spelling and punctuation. Omit any control with no label. Do not collapse, shorten,
> reword or interpret anything. If something printed on the card doesn't fit that — a footnote,
> a control with no clear vocabulary match — record it in a `<Note>` instead of guessing.

Batch the images into single messages and discard context between batches. One image per turn
in a growing conversation re-sends every earlier image and makes the job quadratic.

Check the result with:

```sh
python3 ~/.work/dynamic-controls/scripts/check_artwork.py
```

It verifies control names, duplicates, missing images and clusters labelled both ways. It cannot
tell you a label is wrong — only that the file is a usable transcription.

## `arcade-artwork-original.xml`

The transcription exactly as first completed, before any correction. Not an input to anything —
it exists to be diffed against `arcade-artwork.xml`, which answers "what have we changed since
reading the cards".

**The point of the diff is to separate our mistakes from the author's.** A change is either a
misreading of a card that was right — After Burner's d-pad carries a movement label neither
cabinet has — or a real error on the card itself. Only the second is worth telling whoever made
the images; a list of our own misreadings is no use to them. The diff alone cannot tell them
apart, so each change is settled by looking at the image again.

```sh
python3 ~/.work/dynamic-controls/scripts/artwork_corrections.py
```

Groups every change by rom, which a bare `diff` does not — the changes are mostly identical
label lines, and without the rom name there is no image to look up.

## `arcade-waivers.tsv`

The differences between the artwork and the shipped labels that are meant to stay. One row per
control: `rom`, `control`, `artwork_text`, the `expected_pipeline_text` that replaces it, a
`reason` and a one-line `note`. Most rows are a single port the card splits across two controls
— the shipped label then names both roles, gameplay first ("Shoot / Start") — or a control the
card has no way to draw.

A waiver pins the pipeline's text rather than ignoring the control, so a waived game only scores
as matching while the pipeline still says exactly what the row expects. Edit a label that has a
row and the row has to move with it; `~/.work/dynamic-controls/scripts/ab.py` applies the rows
and lists any whose artwork text or expected text no longer appears.

A row records a decision, so nothing is waived before someone has made it. A port the card splits
across two controls is the common trap: its shipped label has to be written by hand to name both
roles, and until it is, the row waits in `arcade-split-ports-to-review.tsv` (same columns) and
the game counts as a mismatch. Move a row across once its combined text is in `Labels.xml`.

## `genesis-manuals.md`

Which printed manual each Sega Genesis label was read off, by PDF page, and whether the page was
OCR'd or read as an image. A provenance note rather than a transcription: the manuals are 379
image-only PDF scans, not redistributable and not reducible to a table that a later rule change
could be replayed against, so what is recorded is the route back to the page.

It also carries the judgements that are specific to this platform rather than general —
chiefly that `6-Button` is the shipped default, so a manual printing both columns is read from
the six-button one, and the two columns disagree about what A and B mean.

**Finding the page and reading it are separate steps, and only the first is mechanical.**
`locate_controls.py` scores every page of a manual on the vocabulary of a controls page and
renders the best few; the page itself is then read as an image. OCR is reliable for *which page*
(a question about words) and unreliable for *which button* (a question about layout) — a
transcription derived from the OCR dump misattributed 48% of its sentences and was deleted rather
than shipped. The note records the scorer's measured recall and what its predecessor's keyword
filter silently threw away.

## Generated files live elsewhere

Everything here is hand-edited and tracked. What `tools/LabelDump` produces goes to
`tools/LabelDump/output/` and is not tracked:

| file | what it is |
|---|---|
| `arcade-artwork-sorted.xml` | `arcade-artwork.xml` re-emitted in a fixed order, so it diffs against the pipeline dump line for line |
| `arcade-pipeline-sorted.xml` | the labels the pipeline renders for every rom in the cfg pack |
| `arcade-pipeline-images.xml` | the images it renders, grouped by distinct render shape |
| `platform-renders.xml` | what each platform draws under its default controller |

Each carries the command that rebuilds it. Regenerate before trusting a diff — and note the
Arcade files need a local MAME cfg pack, which is not in the repository, so they cannot be
rebuilt from a clean checkout.

## The analysis files are a paused thread, not debris

`review-notes.md`, `judgement-calls.md`, `button-duplicates-triage.md`,
`button-duplicates-cross-checked.md` and `arcade-labels-review.md` are what a data-quality pass
found. The work stopped rather than finished: 22 `<Note>` questions are still open, and more
than half the flagged button duplicates are still duplicated in the transcription.

That pass also produced a `cfg-cross-reference` pair, listing ports a cfg binds that nothing
labelled. Those are deleted — almost every ROM they flagged now has an entry, and the only part
worth keeping was the second exclusion below. `git log -- label-sources/cfg-cross-reference-*`
finds them if the detail is ever wanted.

**The A/B comparison cannot replace them, and will never surface what they hold.** It measures
whether the pipeline renders what the artwork says — so when the artwork labels two distinct
buttons the same, the pipeline renders both that way and the comparison calls the game a perfect
match. Most of the remaining duplicates sit inside that matching set. The comparison's ceiling is
"faithful to the artwork"; these files are about whether the artwork is right, which is a
different question and needs someone looking at cabinet images.

**What makes them worth keeping is the exclusions, not the lists.** Both were learned by reading
wrong answers, and re-deriving either costs a pass of noise:

- A shared label across *axis or movement* ports is not a duplicate. One function legitimately
  has several physical inputs — a stick and a button can both be Move. Only two distinct
  pushbuttons sharing text is suspicious.
- A direction is not unlabelled when its whole control carries a label. `P1_JOYSTICK_LEFT` with
  no entry of its own is covered by `P1_JOYSTICK`, and counting it as a gap flags most of the
  pack.

## Where the judgements live

[`docs/label-import.md`](../docs/label-import.md) holds the rules for turning sources into
shipped labels: which source wins, when a directional cluster collapses, why a movement label
is never guessed. Those rules read this file; they do not change it.
