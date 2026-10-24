# Importing labels in bulk

How the shipped `Platforms/{Platform}/Labels.xml` files are produced from external sources, and the
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

**The artwork and the configs share an author**, so they are not independent witnesses. Where
they disagree, one person has contradicted themselves and one of the two is a slip — the
disagreement is evidence that something is wrong, never evidence for either side. The database
is the only one of the three written by anyone else. Knuckle Heads is the case to keep in mind:
its cfg binds the jump port to stick-up as well as two face buttons, so the pipeline reads up
as *Jump*, while the artwork says the stick means *Move*. Both are that author's own statement
about the same game, and which to correct has not been settled.

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

A specific word can also be accurate and still wrong as a caption: the artist may have watched
the game and correctly seen a button do one real thing, without knowing it does something else
in a state they didn't see — offense versus defense, before the snap versus after. All American
Football's `BUTTON1` reads *Dive* on the card; the database calls it *Action*. Both could be true
of the moment each source was looking at, and neither rules out the button meaning something
else in a state neither source saw. Where a specific word could be one of several
context-dependent actions and nothing settles which, the database's placeholder is the safer
choice — it never claims more than it knows.

### On a console platform there is only one source

No console has an artwork pack or a controls database, so the printed manual is the whole of it,
and the three-way cross-check above has nothing to cross-check against. Two consequences, both
borne out by the Sega Genesis pass ([`label-sources/genesis-manuals.md`](../label-sources/genesis-manuals.md)):

**Read the manual, not a wiki or a search result.** Both were tried and both were wrong in ways
that read as authoritative — a wiki search fallback answering about the wrong game in a series,
a search summary describing moves the manual contradicts. The manual is a primary source and
costs no more to read.

**A manual with two button columns is answering a question about hardware, and the platform's
`Controllers.xml` has already answered it.** Read the column for whichever controller that file
makes `default`. The columns are not re-lettered versions of each other: in Street Fighter II on
the Genesis, A is Light Punch on a three-button pad and Light Kick on a six-button pad, so
picking by habit mislabels half the face buttons.

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

### An analogue port's stick takes the port's label, its nudges the rest

A MAME analogue port (`P1_DIAL`, `P1_PADDLE`, `P1_POSITIONAL`, ...) is three platform buttons:
the port itself for its axis, and `.inc`/`.dec` for the two nudge sequences that turn it
digitally. Label the port with what the stick does (*Rotate*), and the nudges only when the
artwork names each turn — on triggers, typically (*Rotate Left*, *Rotate Right*).

Where the port binds the stick, the port's label is the stick's and the nudge labels keep to
whatever else the nudges bind. That holds however the cfg spells the stick: as the axis on the
port's own sequence (`tdfever`), or as a switch at each end of it, one in each nudge, so it keeps
turning while held (`downtown`). A nudge label reaches the stick only when the port has no label
of its own — so a game labelling only the nudges still shows them there, one per half.

### A port type the cfg repeats is named by its tag and mask

MAME writes one `<port>` per tag and bit a type sits on, so a cfg can carry the same type several
times: `escounts` has four `OTHER` ports, for Pass, Collect, Continue and Bonus. The last of them
keeps the bare type as its name, and each earlier one is named by its type, its tag without the
leading colon, and its mask — `OTHER:STROBE1:2`. Read the names straight off the cfg's `<port>`
attributes; a qualified port's nudges are named after it the same way (`P1_DIAL:DIAL0:255.inc`).
A tag can hold colons of its own (`COIN1:mainpcb:IN0:128`). A port with neither tag nor mask, or
one whose name another port already took, adds its place among every port of its type in the
cfg, bound or not — `P1_BUTTON1#2`, or `P1_BUTTON1:IN0#2` when it is the name that was taken.

Both tag and mask are needed: `escounts` puts all four on one tag, and `gahaha`'s two sticks share
a mask on different tags.

Every port also answers to the name it would be displaced as, the last of a repeated type and a
type the cfg has once included: its type, tag and mask, with its position added where it has no
tag or mask (`P1_BUTTON1#1`) or that name was taken (`P1_BUTTON1:IN0#2`). `escounts`' Bonus is
`OTHER:STROBE1:16` as well as `OTHER`. So name the ports of a repeated type by tag and mask
throughout, all of them: the bare name belongs to whichever repeat is last among those bound to
player 1 and moves with the cfg (rebind Bonus to player 2 and Continue's port becomes the bare
`OTHER`), while the qualified names don't. Name one port by one form only; the same text under
both is harmless, but different text keeps whichever entry comes first and logs the clash. A
`<Defaults>` entry under the bare name gives way to a game entry under the qualified one, as it
would to the bare name.

Only Labels.xml reads the second name. Controllers.xml (`<Mapping>`, `<Control mappings=>`) and
`ControllerOverrides` files name a port by the button it ended up as: bare for the last of its
type, qualified only for an earlier one.

### Labels are captions, not prose

The database writes sentences: `Offensive Throw / Defensive Catch`, or Champion Base Ball's
`B: Bat, Extra Bases - Pitch, Throw Ball`. The overlay has no wrapping or truncation, so a long
label runs off the screen (issue #11). The artwork's own captions are almost always the fix —
*Throw / Catch*, *Bat / Pitch / Throw*.

Keep under about 24 characters. The shipped set's median is four.

### A button with two roles gets both, joined with " / "

The same separator does a second job, and it is not compression. Where a game genuinely gives one
button two distinct actions, name both rather than picking one: Shadow of the Colossus's `R1`
grips a climbing surface and ducks on horseback, and the label is *Grip / Duck*. Three parts are
allowed where they fit — 124 shipped labels have three — but check a render before using them.

Two things are not a second role. A **modifier** is not an action: that same `R1` also makes you
swim faster and mount faster, and those are left out. And a **menu binding** is not gameplay:
Burnout 3's manual gives `Cross` as both "Accelerate" and "Select/Go to next screen", and only
the first is labelled, because menu navigation is near-universal and says nothing about this
game.

Picking which two to name is a judgement. The rule that keeps it honest: name the uses a player
would look the button up for.

### A source is authoritative for the fact, not for the length

Arcade has two label sources and the short one wins on wording. A platform with only a verbose
source — a console's printed manual — has no such tie-break, so the compression from sentence to
caption is a judgement nobody made for you. Two rules keep that judgement honest:

**Where the source already supplies a usable short word, use that word.** Not a synonym, and
especially not series jargon. Castlevania: Bloodlines prints "attack with special weapon", so the
label is *Special Weapon* and not *Sub-Weapon*; Strider and Cadash both print "squat", so neither
says *Crouch*; Gaiares prints "detach TOZ", not *Launch TOZ*. Substituting here looks like editing
and is really guessing, because the replacement carries a meaning the source declined to claim.
It also drifts two games apart for no reason — the Columns and Mean Bean Machine manuals both say
"arrange", so both labels say *Arrange*.

**Where the source supplies only a sentence, the wording is yours, and a second source may
suggest it.** A wiki's control table is someone else compressing the same game into two words,
which is exactly the artwork's role on Arcade — so it is a legitimate suggestion about *wording*
and never about *fact*. A factual disagreement resolves to the primary source; see
[On a console platform there is only one source](#on-a-console-platform-there-is-only-one-source)
for what secondary sources got wrong when trusted with facts.

**Find the ones this applies to mechanically**, rather than re-reading the platform:

```sh
python3 ~/.work/dynamic-controls/scripts/coined_labels.py \
    "assets/Defaults/Platforms/{Platform}/Labels.xml" <dir of per-game source text>
```

It reports every label whose words are absent from its own source — the importer's wording rather
than the source's — and only those are worth a second opinion. On the Genesis pack that was 22
labels out of roughly 230, which is the difference between cross-referencing a platform and
cross-referencing an afternoon's worth. Two confounds to expect, both benign: a page read as an
image has no text to compare against, so everything in it reports as coined, and an OCR typo in
the source reports a faithful label as coined.

### A label that says nothing is dropped

The database marks unused controls several ways — `N/A`, `??`, `Unknown`, `(Not Used)`, `-`.
None of them belong on screen; a blank space is better.

### Letter labels are kept

`A`, `B`, `1`–`4` look like placeholders but usually are not. Most belong to Nintendo VS. System
and PlayChoice-10 cabinets, where the buttons really are named that way and the game's own
instructions refer to them.

### Every judgement call gets recorded, not just made

Most entries need no judgement — the rules above resolve them one way. Some do: which wording is
right when both sources are plausible, whether a stick's directions collapse, whether a specific
verb on an action button is trustworthy or just one context of a multi-purpose button (the
`BUTTON1` case above). A decision like that can be wrong, so it has to be checkable later without
redoing the research that produced it.

Whatever builds the shipped file keeps a review list alongside it — not a comment buried inside
one of 3,571 entries, a separate list a person can actually work through. Each row names the rom,
the control, what was chosen, what the alternative was, and why in enough detail to agree or
disagree on sight: *"aafb BUTTON1: kept 'Action' over the artwork's 'Dive' — the button may be
context-dependent (offense/defense) and neither source confirms which"* is the right shape;
*"aafb BUTTON1: chose Action"* is not, because it gives a reviewer nothing to check the decision
against.

Anything the rules above settle outright — a bare-direction collapse, a dropped `N/A`, a kept
letter label — is not a judgement call and does not belong on this list. Padding it with certain
decisions trains reviewers to skim past the ones that actually need their attention.

## The two lookup keys

**`romName` is the ROM file's basename with `(...)` and `[...]` groups stripped, not the game's
title.** For Arcade the two coincide, because the basename is the MAME short name (`1942`,
`hwchamp`). For a console they rarely do: the file is a set name, so a subtitle is ` - ` and an
article moves to the end — `Castlevania - Bloodlines`, `Lion King, The`. Neither is guessable
from a manual's cover, so take the names from the `<FileName>` rows of `Files.xml` in a LaunchBox
metadata export rather than typing them out.

**`launchBoxId` is the primary key and does not depend on the filename at all**, so an entry
carrying one survives a library named by some other convention. The IDs are the `<DatabaseID>`
of that platform's games in the same export — `Metadata.xml`, with `Mame.xml` giving
the title for an Arcade romset, all from a LaunchBox install rather than the repository.

Matching is by title: exact first, then with punctuation, case and a leading article normalised
away, then `<GameAlternateName>`. **An exact `<Name>` match must outrank a normalised or
alternate-name one** — otherwise a hack's nickname beats a real game's own title, which is how
*Streets of Rage* first resolved to *Streets of Rage Plus*. For Arcade, LaunchBox separates
same-titled cabinets as `Title (Publisher)` or `Title (Year)`, and MAME knows both, so a romset
whose plain title misses is asked again with its publisher — that is what tells `commando`
(Capcom) from `commsega` (Sega).

**An ID is only usable when every entry claiming it agrees.** The index is keyed by ID and the
last entry wins, so an ID shared by entries whose labels differ would silently pick one of them.
That is not rare on Arcade, where LaunchBox files a whole romset family as one game: 69 IDs are
claimed by entries that disagree. Those entries keep `romName` alone, which is what they had
before and costs nothing — an entry with no ID is not worse off, it is merely unchanged.

Where entries for one game disagree, that is a data-quality finding about the transcription, not
a reason to pick one. The A/B comparison cannot see it, for the same reason it cannot see
duplicate labels.

## Running an import

Scripts live outside the repo, in `~/.work/dynamic-controls/scripts/`, because they depend on
two inputs that are not distributed with the plugin: the controls database and the artwork pack.

```sh
# Generate entries for every game the artwork covers and the database labels completely.
# Existing entries are left alone — several were hand-read and say more than the database does.
python3 ~/.work/dynamic-controls/scripts/gen_labels.py /tmp/generated.xml

# The brief for reading a game's artwork: which physical control each port reaches,
# which directions its movement control has, which shape of directional label to expect,
# whether its config is empty, and what the database already names — including whether it
# gives the movement control a real verb, which the artwork cannot be trusted to supply.
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

### Measure what a pre-pass discards, not just what it keeps

Where a source has to be *found* before it can be read — a page in a scanned manual, a region of
an image — the finding step is a filter, and **a filter's silence is evidence about the filter,
not about the source.** It is also the one step whose failures leave no trace in the output, so
nothing downstream will ever flag them: a game the filter skipped looks exactly like a game whose
manual says nothing.

The Genesis pass is the worked example, and it cost two separate rounds of rework. Its page
filter kept any page naming two or more of A/B/C/X/Y/Z, which silently discarded every page whose
subject was the D-pad — so the first pass shipped no D-pad label at all — and wrote off 23
manuals as unreadable. Replacing it with a scorer that weighs the vocabulary of a controls page
(headings, D-pad tokens, pad-kind markers) found a page in 43 of the 44 games that had been
attempted and never shipped. Exactly one manual was genuinely unusable. The full account is in
[`label-sources/genesis-manuals.md`](../label-sources/genesis-manuals.md#ocr-finds-the-page-only-a-reading-attributes-it).

Two things make this measurable rather than a matter of faith, and both are cheap:

- **Score against the pages a previous pass already got right.** Recall over known-good targets
  is a number; "the filter seems to work" is not. The Genesis locator offered 84 of 84.
- **Report what each decision rested on.** The locator prints the tokens that earned a page its
  score, so a call can be checked without re-running anything.

Keep the two steps apart: a mechanical finder is fine and a mechanical *reader* is usually not,
because finding asks only what a source is about while reading asks how it is laid out. The same
Genesis OCR that located every page attributed 48% of its sentences to the wrong button.

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
