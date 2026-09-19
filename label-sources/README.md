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

Reading is mechanical, so the instruction is short:

> For each image, output the ROM name, the title, and every label the artwork prints, each
> against the control it points to. Use the control vocabulary above. Copy the text exactly,
> including spelling and punctuation. Omit any control with no label. Do not collapse, shorten,
> reword or interpret anything.

Batch the images into single messages and discard context between batches. One image per turn
in a growing conversation re-sends every earlier image and makes the job quadratic.

Check the result with:

```sh
python3 ~/.work/dynamic-controls/scripts/check_artwork.py
```

It verifies control names, duplicates, missing images and clusters labelled both ways. It cannot
tell you a label is wrong — only that the file is a usable transcription.

## Where the judgements live

[`docs/label-import.md`](../docs/label-import.md) holds the rules for turning sources into
shipped labels: which source wins, when a directional cluster collapses, why a movement label
is never guessed. Those rules read this file; they do not change it.
