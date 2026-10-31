# Nintendo 64 labels — where they came from

An 18-game pilot in `assets/Defaults/Platforms/Nintendo 64/Labels.xml`, read off the printed
manuals. Sources and method follow [`genesis-manuals.md`](genesis-manuals.md); only what differs is
recorded here. There is no A/B gate: this file and the table below are the only record of what each
label rests on.

## Sources

| Source | What it is | Where it lives |
|---|---|---|
| Printed manual | 202 PDF scans under `romcontrols/manuals/Nintendo 64/` | not redistributable; never committed |

Controls pages were found with `locate_controls.py` and read as images, per the Genesis note. The
pilot was twenty games chosen to span the shapes the rest will take: first-person shooters (GoldenEye,
Perfect Dark, Turok), action-adventures (Super Mario 64, the two Zelda games, Banjo-Kazooie, Conker),
racers, a fighter, a party game, and games whose controls change by vehicle.

**StrategyWiki was not consulted.** The Super Nintendo and PS2 notes take a binding either source
states; here every label is the manual's alone. Where the manual prints no controls page the game
has few or no labels (see below), and those are the first candidates for a wiki pass.

## What rests on a single source

Every entry, since the manual is the only source. The ones to re-check first are those the readers
flagged as their own inference rather than something printed:

- **GoldenEye 007's and Perfect Dark's D-pad.** The manual prints the D-pad block as "side step left
  or right / look up or down" against one control pad. Pairing left and right with the first and up
  and down with the second is inferred from the order of the words. Perfect Dark says the D-pad "also
  performs this function" as the C buttons, so its D-pad copies the C labels.
- **Super Mario 64's left and right C buttons** are both "circle left and right around Mario"; which
  of Circle Left and Circle Right goes on which button is the reader's pairing.
- **Banjo-Kazooie's `Camera Zoom`** on both C-Up and C-Down: the manual describes two zoom levels
  (C-Down toggles between two outside views, C-Up is first person). One shared caption covers both.

## Judgement calls

- **Configurable layouts:** only the stated default is labelled. GoldenEye prints one default layout
  and notes that the control style can be changed; Perfect Dark's is the described "basic control
  method, option 1.1" of eight; Rogue Squadron's is Luke's settings, which three other pilots swap
  around; Turok's is right-handed, which the manual says is the default.
- **Controls that change by vehicle or mode are not fully expressible.** Pilotwings 64 keeps only R
  (*Switch Viewpoint*, constant in all six vehicles); the stick, the C buttons, A, B and Z all vary,
  and are left blank. Conker and Rogue Squadron take their on-foot and default-pilot sets. Issue #46 tracks several control sets per game, after which these are re-read.
- **Printed as is, though it looks inverted:** GoldenEye's C-Up is *Look Down* and C-Down is *Look
  Up*; Turok's stick-up is *Look/Aim Downwards* and stick-down *Look/Aim Upwards*. Both are what the
  manual's diagram prints, so they are labelled as printed.
- **Wording that is the reader's, not the manual's:** Wave Race 64's *Slide* (R), Conker's *Attack*
  (B), *Camera Zoom* (C-Up) and *Align Camera* (C-Down), 1080 Snowboarding's *See Behind Rider*,
  Mario Party's *Hit Dice Blocks* and *Map View*, Rogue Squadron's *Roll* (R) and *Brakes* (Z),
  Super Mario 64's *Get Close* and *Pull Away*, and shortened captions such as *Respond To ROB* and
  *Show Or Hide Map*.
- **Start:** `Start = Pause` is the platform default. The two Zelda games override it (*Subscreen*,
  *Subscreens*) because their manuals give Start a subscreen and no pause.
- **Mortal Kombat Trilogy leaves Z and C-Left blank.** A second BLOCK arrow ends near C-Left, and Z is
  labelled "(underneath)" with no function; neither is stated clearly enough to label.
- **Mario Party's labels are mostly menu actions** (*Cancel*, *Move Cursor*); the manual describes
  little else, since mini-game controls are shown on screen. They are kept, doubtfully.
- **A and B on Turok** are both *Weapon Select*: the diagram gives A as menu select and B as weapon
  select, but the text says A cycles weapons up and B down.
- **Dropped, as on Genesis and SNES:** combinations (Super Mario 64's A/B/Z moves, GoldenEye's lean and
  duck, Star Fox 64's barrel roll, Wave Race 64's stunts) and menu-only functions.
- **Super Mario 64's earlier hand-written entry** (C-Any *Camera*, Start *Start*) is replaced. The
  manual gives each C button its own job and Start only pauses; R is left blank because its camera
  mode depends on a pause-screen option.

## Games with few labels, or none

- **Mario Kart 64** has no entry. Its manual defers every button to the Operation Card and prints
  only scattered, qualified advice.
- **Diddy Kong Racing** has no entry. Its scan is low resolution with no controls page, and the one
  button it states (Z honks, in Adventure mode) is not enough to label.
- **F-Zero X** has a single label (L, *Time Difference*): the manual defers controls to the
  Operation Card.
- **1080 Snowboarding and Wave Race 64** are partial for the same reason; Wave Race 64's source is
  the Operation Card itself, a two-page scan.

## What each entry rests on

Page numbers are PDF page indices, not the manual's printed numbering.

| Game | PDF page(s) | Read how |
|---|---|---|
| 007 GoldenEye | 3-5 | directly (default controller page), C glyphs zoomed |
| 1080 Snowboarding | 3-5, 9-13 | directly; controls only in fragments |
| Banjo-Kazooie | 6 | directly |
| Conker's Bad Fur Day | 6-8 | directly |
| Diddy Kong Racing | 1-3, 5-7, 9-13 | directly, low resolution; no entry |
| F-Zero X | 3-9 | directly; one label |
| Kirby 64: The Crystal Shards | 3, 8-10 | directly |
| Mario Kart 64 | 2-4, 6-9, 12-14 | directly; no entry |
| Mario Party | 6, 7 | directly |
| Mortal Kombat Trilogy | 4-6 | directly, page 5 cropped at 200 dpi |
| Paper Mario | 3, 6-11 | directly (field screen controls) |
| Perfect Dark | 5 | directly |
| Pilotwings 64 | 4-11 | directly (diagram per vehicle) |
| Star Fox 64 | 6 | directly |
| Star Wars: Rogue Squadron | 3-6 | directly |
| Super Mario 64 | 3, 6-9, 12 | directly (operations pages, camera page) |
| The Legend of Zelda: Majora's Mask | 9-11 | directly |
| The Legend of Zelda: Ocarina of Time | 3, 4, 16-19 | directly, low resolution |
| Turok: Dinosaur Hunter | 4, 6-14 | directly, 170 dpi crops |
| Wave Race 64 | 1, 2 | directly (Operation Card) |

## Both lookup keys

`romName` is the rom file's basename with `(...)` and `[...]` groups stripped, taken from the
`Nintendo 64` rows of `Files.xml`; `launchBoxId` is the `<DatabaseID>` of the matching `Metadata.xml`
game on that platform. Titles whose rom file differs from the manual's: GoldenEye is
`007 - GoldenEye`, the Zelda games are `Legend of Zelda, The - ...`, Turok is
`Turok - Dinosaur Hunter`, Rogue Squadron is `Star Wars - Rogue Squadron`, and 1080° Snowboarding is
`1080 Snowboarding`. `lb-ids.py` lists the candidates for a set of titles.
