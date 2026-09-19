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

**Run `decode.py` first and read the artwork against its output.** It answers the questions the
image cannot: which physical control a port reaches, which directions the movement control
actually has, and whether to expect one directional label or four. A reader given only the
image will invent explanations for gaps the config would have closed — and those explanations
are plausible enough to survive review.

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

## Reading the artwork

Every image in the pack uses one template — same chassis, same label positions — so a label's
position identifies the control it belongs to. That is what makes extraction mechanical, but two
parts of the layout vary and will mislead a reader that assumes they do not.

### A directional cluster holds one label or four

The D-pad and the left stick each draw four direction icons. Usually a single label sits beside
the cluster, centred on it, meaning the whole control. Sometimes each direction carries its own
label instead, aligned with its own icon.

The two modes are not interchangeable in position or extent:

| | Collapsed | Per-direction |
|---|---|---|
| Vertical position | centred on the cluster | aligned with each icon, spread above and below |
| Left extent | starts well clear of the edge | starts close to the edge, the text being longer |
| Count | one | up to four, and any of them may be blank |

Order within a cluster is **up, left, right, down** — the template's stack order, not clockwise.

**The config says which to expect before you look.** A game whose cfg binds one joystick across
four directions will have a single label. A game whose cfg puts *different ports* on individual
directions will have several, one per port, and the directions its cfg leaves unbound will be
blank. Deriving that first turns an open question into something checkable.

Bradley Trainer is the worked example. Its cfg binds three separate ports to D-pad up, left and
down, and nothing to right. The artwork labels exactly those three, leaves right blank, and each
label matches what the database says for that port — the positional method surviving its hardest
case in the pack.

### The artwork is authoritative for wording, not for spelling

Bradley Trainer's labels read *Armor Peircing*. Take the artwork's choice of words and the
database's spelling where the two describe the same thing.

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

# The brief for reading a game's artwork: which physical control each port reaches,
# which directions its movement control has, which shape of directional label to expect,
# whether its config is empty, and which buttons the database already names.
# Read the artwork against this. Never against the image alone.
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

A run over twenty-eight games found the text extracted correctly every time, and the port
structure right on nine of ten in the blind part of the sample. Both misses are worth knowing
because neither is a reading failure:

- **An empty config breaks the position-to-port mapping.** Every other config in the pack puts
  the first button on X and the second on A, and the artwork is drawn to that convention. With
  no config, the emulator's own defaults apply instead and the drawing means something else.
  Detect an empty config in the pre-pass and treat those games separately.
- **The artwork says "Move" for any stick it has nothing specific to say about**, including
  sticks that rotate rather than move. Eliminator, Star Castle and Lunar Lander all read "Move"
  on the artwork where the database says *Rotate*. Prefer the database wherever it supplies a
  whole-stick verb.

## What is left

The Arcade artwork pack holds 3,571 games. Of those:

- **721** are shipped, generated from the database and checked
- **1,066** have a movement control the database never labels
- **1,758** are absent from the database altogether

The last two groups need their artwork read, but not equally. For the games the database covers
apart from their movement control, the button labels are already known and only the directional
cluster has to be read — a narrower question, and one the config can predict the shape of.

They are also where the analogue controls cluster — driving and sports cabinets — so issue #12
will bite harder there than it does today.

### On cost

Every image costs the same to read, and the total is linear in the number of images **only if
several are sent together and the context is discarded between batches**. Feeding them one per
turn into a growing conversation re-sends every earlier image on every later turn, which is
quadratic and rules the job out entirely. Nothing else about the arrangement matters nearly as
much.

Cropping to the label columns would cut the per-image cost, and the fixed template makes it
safe in principle. It is not worth building before a batch has run unmodified: the directional
cluster changes both height and width between its two modes, so a crop sized for one silently
clips the other.
