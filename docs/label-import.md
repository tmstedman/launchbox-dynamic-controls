# Importing labels in bulk

How the shipped `Labels/{Platform}.xml` files are produced from external sources, and the
rules that decide what a label says. Written during the Arcade import; the reasoning applies
to any platform where labels come from somewhere other than a person typing them.

For what a label file *is*, see [architecture.md](architecture.md#3-label-resolution). This
document is about filling one.

## The three sources

| Source | What it is | What it is good for |
|---|---|---|
| Controller artwork | One image per game, drawn on a controller chassis | The wording a player would use |
| MAME controls database | `controls.xml`, the BYOAC project's label database | Per-direction detail, and machine-readable |
| MAME config | One `cfg` per game, remapping ports to physical buttons | Which control each port actually reaches |

**The config is never optional.** A game's cfg decides which physical button a port lands on,
and those configs rotate the buttons freely — reading a label's position off the artwork and
assuming the port beneath it will mislabel most multi-button games. Red Baron is the clearest
case: one port drives both the A button and the right trigger, so a single entry has to reach
two controls, which no image can tell you.

**Where the two label sources disagree, prefer the artwork for wording and the database for
detail.** The artwork was drawn by someone who played the game: Crowns Golf's four buttons are
`Address Up/Down/Left/Right` in the database, which reads like a cursor being nudged, and
*Move Club* and *Change Stance* in the artwork, which is what the buttons do. But the artwork
summarises a whole stick as one word, so where the database names each direction separately —
Street Fighter's *Jump* and *Crouch* — that detail is worth keeping.

Neither source is automatically right about *content*. Giga Wing's artwork labels a button
*Jump*, which is impossible in a vertical shooter.

## The rules

### A whole control gets one label; a direction gets its own only when it earns it

Four directions reading `Up`, `Down`, `Left`, `Right` say nothing a player cannot see. Collapse
them to the whole control and use the artwork's word for it.

Four directions naming different actions stay separate, and any of them reading as a bare
direction stays too — a stick labelled *Fast* and *Slow* with blank sides looks broken.

The hard case is a stick where one direction differs. Ask whether that word means **moving**:

| Game | Directions | Verdict |
|---|---|---|
| Arabian | Walk, plus bare up/down | Walk is moving — collapses to one label |
| Rampage | Climb, plus bare directions | Climb is moving — collapses |
| Columns | Drop, plus bare left/right | Dropping a piece is not moving — stays separate |
| Elevator Action | Kneel | A posture, not movement — stays separate |

No pattern over the text decides this. It needs someone who knows what the game does.

### Never guess the movement label

A plain joystick is *Move* in one game and *Steering* in the next; both are single four-way
sticks and nothing in the data separates them. A game whose movement control the database
never labels is left out of the generated set entirely and imported from its artwork instead.

That exclusion is why the generated Arcade set covers 721 games rather than 1,787.

### Only label directions the config offers

A whole-stick label claims the entire control. Where a config binds only two of a stick's
directions — a horizontal-only dial, a vertical-only joystick — label those directions
individually and leave the rest blank. Otherwise the overlay claims a control that does
nothing, which is the same defect as labelling a dead D-pad.

### Labels are captions, not prose

The database writes sentences: `Offensive Throw / Defensive Catch`, or Champion Base Ball's
`B: Bat, Extra Bases - Pitch, Throw Ball`. The overlay has no wrapping or truncation, so a long
label runs off the screen (issue #11). The artwork's own captions are almost always the fix —
*Throw / Catch*, *Bat / Pitch / Throw*.

Keep under about 24 characters. The shipped set's median is four.

### A label that says nothing is dropped

The database marks unused controls several ways — `N/A`, `??`, `Unknown`, `(Not Used)`, `-`.
None of them belong on screen; a blank space is better.

### Letter labels are kept

`A`, `B`, `1`–`4` look like placeholders but usually are not. Most belong to Nintendo VS. System
and PlayChoice-10 cabinets, where the buttons really are named that way and the game's own
instructions refer to them.

## Running an import

Scripts live outside the repo, in `~/.work/dynamic-controls/scripts/`, because they depend on
two inputs that are not distributed with the plugin: the controls database and the artwork pack.

```sh
# Generate entries for every game the artwork covers and the database labels completely.
# Existing entries are left alone — several were hand-read and say more than the database does.
python3 ~/.work/dynamic-controls/scripts/gen_labels.py /tmp/generated.xml

# Decode a game's config to physical buttons, for reading its artwork against.
python3 ~/.work/dynamic-controls/scripts/decode.py crgolf redbaron

# Check a platform's label file for every defect class this document describes.
python3 ~/.work/dynamic-controls/scripts/audit_labels.py Arcade
```

`DC_REPO`, `DC_CONTROLS` and `DC_ROMCONTROLS` override the default paths.

Run the audit after every batch. Each check in it exists because a real defect shipped and was
caught by eye rather than by code.

## Measuring an extraction before trusting it

The shipped set is ground truth for 721 games, plus fifteen read by hand. To judge whether a
process — or a model — reads the artwork accurately enough, run it over fifty of those and diff
against what is already there. That gives an error rate on this task rather than an impression.

Reading these images is mechanical: one template, one chassis, fixed label positions. The
judgement calls above are where errors actually come from, and they are perhaps a tenth of
games.

## What is left

The Arcade artwork pack holds 3,571 games. Of those:

- **721** are shipped, generated from the database and checked
- **1,066** have a movement control the database never labels
- **1,758** are absent from the database altogether

The last two groups need their artwork read. They are also where the analogue controls cluster —
driving and sports cabinets — so issue #12 will bite harder there than it does today.
