# Label sources

Inputs the shipped label files are built from. Nothing here is packaged — the release zip
carries `assets/` only.

## `arcade-artwork.xml`

What the controller artwork says, transcribed and nothing more. One `<Game>` per image, one
`<Label>` per control the artwork draws a line to, holding the text exactly as printed.

**This file records what is drawn, not what should ship.** Labels are not collapsed, shortened,
spell-corrected, reconciled against the MAME controls database, or mapped to emulator ports.
Every one of those is a judgement that can change, and keeping them downstream means a rule
change never costs another read of 3,571 images.

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

## `arcade-artwork copy.xml`

The transcription as first read, kept deliberately. `arcade-artwork.xml` is corrected whenever a
re-read finds the original reading wrong — After Burner's d-pad was transcribed with a movement
label neither cabinet has, and a steering wheel was paired onto controls it never reached — and
this file is what those corrections can be measured against. Without it there is no way to tell a
fix from a drift, because the images are not re-read every time.

Not generated and not an input to anything. Leave it where it is; the name is unfortunate but
changing it would break the one thing it is for, which is being the version that predates the
edits.

## Generated files

`arcade-artwork-sorted.xml`, `arcade-pipeline-sorted.xml`, `arcade-pipeline-images.xml` and
`platform-renders.xml` are written by `tools/LabelDump` and carry the command that rebuilds them.
Do not edit them: the first is `arcade-artwork.xml` re-emitted in a fixed order, and the rest are
what the pipeline actually renders, recorded so a change can be diffed against what came before.

## Where the judgements live

[`docs/label-import.md`](../docs/label-import.md) holds the rules for turning sources into
shipped labels: which source wins, when a directional cluster collapses, why a movement label
is never guessed. Those rules read this file; they do not change it.
