# Sega Genesis labels — where they came from

Every entry in `assets/Defaults/Platforms/Sega Genesis/Labels.xml` was read off the game's own
printed manual. Unlike Arcade there is no machine-readable control database and no artwork
transcription for this platform, so there is no A/B gate: this file is the only record of what
each label rests on.

## Source

A local set of 379 Genesis manual scans, one PDF per game, named by title. The repository does
not carry them — they are not redistributable and the release zip carries `assets/` only.

The scans have no text layer, so each page was rendered (`pdftoppm -r 150 -png`) and read. Two
ways, both recorded per game below:

- **OCR** — macOS Vision (`VNRecognizeTextRequest`, accurate level), scanning pages 2–18 and
  keeping any page naming two or more of A/B/C/X/Y/Z. Good enough for the usual
  "Button A • Press to jump" list.
- **Read directly** — the page image read as an image rather than transcribed. Used wherever
  the controls are a *table* or a labelled controller diagram, which OCR reorders into
  nonsense: every six-button fighter is in this group, and the dark-background manuals
  (Mortal Kombat II) OCR'd to nothing at all.

### OCR finds the page; only a reading attributes it

Those two are not alternatives. **Locating a controls page and transcribing one are different
demands, and OCR meets only the first.** Asking "is this page about the controller" is a question
about vocabulary, which survives OCR's reordering intact; asking "which sentence belongs to
Button A" is a question about layout, which does not — a derivation attempted from the OCR dump
put 48% of its sentences against the wrong button and was discarded rather than shipped.

So the page is found by score and then read as an image:

```sh
python3 ~/.work/dynamic-controls/scripts/locate_controls.py \
    --manuals "<dir of PDFs>" --games-file <titles> --keep 2 --render <dir>
```

It renders every page, OCRs them, and scores each on weighted section headings (*Take Control*,
*Button Functions*), D-pad tokens (*Directional Button*, *D-Button*, *D-Pad*), pad-kind markers
(*6 Button*, *Arcade Pad*, *Mode Button*), distinct button letters and imperative-line density,
printing the tokens that earned each score so a call is checkable. **A keeper pulls in its
neighbours**, because a controls section routinely runs over the fold and leaves its second half
scoring nothing.

Measured against the table below as it stood after the first pass — 55 games, 84 pages — it
offered **every one**, 77 by score and 7 only through the neighbour rule. It is a filter and not
an oracle: the top-scoring page was the recorded one just 22 times out of 55, so the output is a
batch to read, never a page to trust. `eval_locator.py` alongside it re-runs that measurement.

## The six-button problem

**The shipped `Controllers.xml` makes `6-Button` the default**, so where a manual gives both a
three-button and a six-button column, the six-button one is what the labels say. The two
columns are not re-lettered versions of each other — in Street Fighter II: Special Champion
Edition, **A is Light Punch on a three-button pad and Light Kick on a six-button pad**. Taking
the three-button column would mislabel half the face buttons on the controller the plugin
actually assumes.

Fourteen games carry six-button labels: Art of Fighting, Beyond Oasis, Eternal Champions,
Fatal Fury 2, Judge Dredd, Mortal Kombat II, Mortal Kombat 3, Primal Rage, Samurai Shodown,
Spider-Man & Venom, Street Fighter II: Special Champion Edition, Super Street Fighter II,
Virtua Racing and Zombies Ate My Neighbors. Fatal Fury 2 is the only one that labels `Mode`.

**Four of those are not fighters, which is why the first pass missed them.** Judge Dredd,
Spider-Man & Venom, Virtua Racing and Zombies Ate My Neighbors all print the six-button
controls as a *note on the three-button list* rather than as a second column — "Where different,
controls for 6 Button Arcade Pad appear in parentheses" (Judge Dredd, Spider-Man & Venom), or a
separate **Six-button power!** panel (Zombies). Zombies is the sharpest case: on three buttons
`A` is a modifier you hold while tapping `B` or `C`, and on six buttons `A` fires the weapon
outright while `X` and `Y` take over the changing. Reading its three-button column would have
labelled `A` for a role it does not have on the pad the plugin assumes.

## What each entry rests on

Page numbers are **PDF page indices**, not the manual's own printed numbering — the scans
include covers, so the two differ by two or three pages in most.

| Game | PDF page(s) | Read how |
|---|---|---|
| Altered Beast | 5, 6 | OCR |
| Art of Fighting | 4, 5 | directly (table) |
| Ayrton Senna's Super Monaco GP II | 4 | directly (OCR-located) |
| Ballz 3D | 5 | directly (OCR-located) |
| Battletoads | 7 | OCR |
| Battletoads & Double Dragon | 9, 10 | directly (OCR-located) |
| Beyond Oasis | 7 | directly (diagram) |
| Bio-Hazard Battle | 4 | directly (OCR-located) |
| Bonkers | 5 | directly (OCR-located) |
| Boogerman | 4 | directly (OCR-located) |
| Bubsy | 14 | directly (OCR-located) |
| Cadash | 5 | OCR |
| Captain America and the Avengers | 6, 7 | directly (OCR-located) |
| Castle of Illusion Starring Mickey Mouse | 4, 5 | OCR |
| Castlevania: Bloodlines | 6, 7 | OCR |
| Chakan: The Forever Man | 4, 5 | OCR |
| Chuck Rock | 5 | directly (OCR-located) |
| Columns | 4, 5 | OCR |
| Comix Zone | 7 | OCR |
| Contra: Hard Corps | 9 | OCR |
| Crusader of Centy | 8, 9, 10 | directly (OCR-located) |
| Dashin' Desperadoes | 4 | OCR |
| Deadly Moves | 3 | OCR |
| DecapAttack | 6, 7 | OCR |
| Disney's Aladdin | 5 | directly (OCR-located) |
| Dr. Robotnik's Mean Bean Machine | 3, 4 | OCR |
| Dragon's Fury | 3 | directly (OCR-located) |
| Dynamite Headdy | 4, 5 | OCR |
| ESWAT: City Under Siege | 5, 7 | directly (OCR-located) |
| Earthworm Jim | 4 | OCR |
| Earthworm Jim 2 | 4 | OCR |
| Ecco the Dolphin | 4 | OCR |
| Ecco: The Tides of Time | 7, 8 | OCR |
| Elemental Master | 8 | OCR |
| Eternal Champions | 7 | directly (table) |
| Fatal Fury | 4 | directly (OCR-located) |
| Fatal Fury 2 | 6, 7 | directly (table) |
| Flashback | 18 | directly (OCR-located) |
| Forgotten Worlds | 4, 5 | OCR |
| Gaiares | 5 | OCR |
| Gain Ground | 6 | OCR |
| Gargoyles | 4, 5 | directly (OCR-located) |
| Gods | 4, 5 | OCR |
| Golden Axe | 4, 6 | OCR |
| Golden Axe II | 3, 4 | OCR |
| Gunstar Heroes | 5, 6 | OCR |
| Haunting Starring Polterguy | 5 | directly (OCR-located) |
| Herzog Zwei | 6, 7 | OCR |
| Judge Dredd | 5, 6 | directly (OCR-located) |
| Jurassic Park | 5 | directly (OCR-located) |
| Landstalker | 4, 5 | OCR |
| Light Crusader | 4 | directly (table) |
| Lightening Force | 3 | directly (OCR-located) |
| Mega Turrican | 5 | OCR |
| Mercs | 3, 4 | OCR |
| Midnight Resistance | 3, 4 | OCR |
| Mortal Kombat II | 10, 11 | directly (dark scan, OCR empty) |
| Mortal Kombat 3 | 4 | directly (diagram) |
| NBA Jam | 9 | directly (OCR-located) |
| Out of This World | 4 | OCR |
| OutRun | — | pre-existing, not from this pass |
| Phantasy Star III | 3, 4 | directly (OCR-located) |
| Primal Rage | 7, 8 | directly (table) |
| Ranger X | 5, 6 | OCR |
| Road Rash | 3, 7 | directly (OCR-located) |
| Road Rash 3 | 3 | directly (OCR-located) |
| Rocket Knight Adventures | 6 | OCR |
| Rolling Thunder 2 | 3 | directly (OCR-located) |
| Samurai Shodown | 4 | directly (table) |
| Shadow Dancer | 3, 4 | directly (OCR-located) |
| Shining Force II | 6, 7 | directly (OCR-located) |
| Shining in the Darkness | 5 | directly (OCR-located) |
| Shinobi III: Return of the Ninja Master | 3, 5 | OCR |
| Sonic & Knuckles | 5 | directly (OCR-located) |
| Sonic the Hedgehog | 3 | OCR |
| Sonic the Hedgehog 2 | 4, 5 | OCR |
| Sonic the Hedgehog 3 | 5 | directly (OCR-located) |
| Spider-Man & Venom | 4 | directly (OCR-located) |
| Splatterhouse 3 | 5 | directly (OCR-located) |
| Street Fighter II: Special Champion Edition | 4 | directly (table) |
| Streets of Rage | 3, 4 | OCR |
| Streets of Rage 2 | 4, 5 | OCR |
| Strider | 4 | OCR |
| Sunset Riders | 4 | directly (OCR-located) |
| Super Monaco GP | 3, 4 | directly (OCR-located) |
| Super Street Fighter II | 4 | directly (table) |
| Sword of Vermilion | 3 | directly (OCR-located) |
| Teenage Mutant Ninja Turtles | 6 | directly (OCR-located) |
| The Lion King | 2, 5 | OCR |
| The Revenge of Shinobi | 3, 4 | OCR |
| Thunder Force III | 3, 4 | directly (OCR-located) |
| Toy Story | 4 | OCR |
| Truxton | 3 | directly (OCR-located) |
| Vectorman | 6 | OCR |
| Virtua Racing | 6, 7 | directly (OCR-located) |
| World of Illusion | 4 | directly (OCR-located) |
| X-Men | 4 | directly (OCR-located) |
| Zero Tolerance | 8, 9 | directly (OCR-located) |
| Zombies Ate My Neighbors | 6 | directly (OCR-located) |

## The D-pad

The first pass labelled buttons only — its page filter kept lines naming A/B/C/X/Y/Z and threw the
D-pad paragraph away. A second pass read it back: 88 of the manuals carry a D-pad section, and
all but six of the shipped games now say something about it.

Which shape a game gets follows [the rule in
`label-import.md`](../docs/label-import.md#a-whole-control-gets-one-label-a-direction-gets-its-own-only-when-it-earns-it) —
one label for the whole control unless a direction names something that is not moving:

- **One label, on `Dpad-Any`** where the manual describes movement and nothing else: Mercs
  ("move in 8 directions"), Ecco ("swim in any direction"), Gain Ground, Streets of Rage. The
  `<Control name="Dpad-Any">` already in `Controllers.xml` spreads it over the four directions,
  and `analogToDigital="left"` carries it to the stick, so one element labels both pads.
- **Per direction** where one earns it: Vectorman's `Up` is *Look Up* and `Down` is *Crouch* while
  left and right are *Run*; Columns' `Down` is *Drop Faster*; Midnight Resistance's `Up` fires a
  backpack weapon. Every fighter with an eight-way diagram is here too — `Up` jumps and `Down`
  crouches in Mortal Kombat II, Mortal Kombat 3 and both Street Fighter IIs.

Five cases are deliberately absent. Dynamite Headdy and Mega Turrican never describe the D-pad
outside a special move, and Primal Rage's diagram labels it `Up`/`Down`/`Left`/`Right` and stops —
bare direction names say nothing a player cannot see, so there is nothing to write. The second
pass added two more: Jurassic Park sends the reader to an Options screen the manual never prints,
and Thunder Force III describes the D-pad only in Configuration Mode and on the planet-select
screen.

Two rules did the most work here:

- **Menu text is not a label.** Most D-pad paragraphs mix play with the options screen — "press up
  or down to select a game mode" (Columns), "moves the highlight on the Password screen" (Ecco).
  Only the in-play sentence is read. This is where a careless pass produces wrong labels, because
  the menu text usually comes first.
- **The verb is never defaulted.** *Move*, *Swim*, *Run*, *Walk* and *Steer* are each right for
  some game here and wrong for the next, and nothing but the manual separates them.

## Judgement calls

- **Wording is shortened, and the manual's own short word wins.** Manuals describe actions in
  sentences ("Press to make Mickey throw things at his enemies"), and a label renders beside a
  button, so these are cut to one or two words. Where the manual already prints a usable word the
  label uses *that* word rather than a synonym — *Special Weapon* not *Sub-Weapon*, *Squat* not
  *Crouch*, *Detach TOZ* not *Launch TOZ*, *Arrange Beans* not *Rotate Beans*. Each of those four
  was written the wrong way first and corrected; substituting reads as editing but is really
  guessing, since the replacement claims something the manual did not.
- **Where only a sentence exists, the wording is mine.** Midnight Resistance's "fire in one
  direction only", Herzog Zwei's "switch your fighting force between infantry soldier and attack
  jet", Columns' "increase the speed at which the columns drop" and Sonic's "see the top or bottom
  section of the screen" have no short form on the page, so *Lock Aim*, *Change Form*,
  *Drop Faster* and *Look Up*/*Look Down* are choices rather than readings. They are the entries a
  second source could usefully second-guess; see
  [the rule in `label-import.md`](../docs/label-import.md#a-source-is-authoritative-for-the-fact-not-for-the-length).
- **Combinations are dropped.** Only rows naming a single unqualified button were used. A move
  that needs `B` + `C`, or a direction plus a button, has no single button to attach to.
- **Menu-only functions are not labels.** Most manuals list what a button does on the options
  screen alongside what it does in play; only the in-play function is kept. Where a button has
  *no* in-play function the entry is simply absent — Battletoads ("A Button: Not used"), Mercs
  and Mega Turrican are the cases here.
- **A button the manual declines to name stays unlabelled.** Beyond Oasis says only that A, B
  and C "perform special fighting moves (in conjunction with other buttons)", so only its
  X/Y/Z — Map, Weapons, Items — are labelled.
- **Repeated text is kept as repeated text.** Sonic's three jump buttons all say Jump, and Art
  of Fighting's X, Y and Z all say Provoke, because that is what the manuals say. The render
  path decides what to do about agreement; this file does not pre-empt it.
- **`Start` = Pause** is in `<Defaults>`, so it applies to every Genesis game rather than being
  repeated 99 times. Manuals that call it "Start game" are describing the title screen, not play.
  Flashback is the one game that overrides it: there `Start` "opens and closes the inventory",
  and the manual gives pausing to no button at all.

### Where the buttons are configurable

Fourteen of the games read in the second pass let the player reassign A, B and C, which makes
"what does A do" a question the manual may not answer. Three shapes, and what each one gets:

- **The manual names a default.** Ballz 3D ("Basic Moves (Default Setting)"), Disney's Aladdin
  ("If you don't change the buttons, the controls will be..."), Flashback ("Default control
  settings"), TMNT ("These are the settings at the start of the game") and Shadow Dancer (whose
  prose matches its own TYPE 1 column) are read exactly as printed. A stated default is a fact,
  not a guess, and the reassignment is the player's business.
- **The manual prints the arrangements and names no default.** Super Monaco GP and Ayrton
  Senna's Super Monaco GP II both tabulate six arrangements (TYPE A–F) over `Up`, `Down`, `A`,
  `B` and `C`. Only what is **constant down every column** is labelled — `C` is *Pit In* in all
  six — plus the steering, which both manuals state outside the table ("to steer, always press
  the D-Button left or right"). `A`, `B`, `Up` and `Down` are left blank rather than read off
  the first row, which is a layout accident and not a declaration.
- **The manual never states the mapping at all.** Captain America and the Avengers describes
  "an attack button" and "the jump button" and sends the reader to the Options screen; Jurassic
  Park's `A` means *Choose weapon* as Grant and *Bite* as the Raptor. Those buttons stay
  unlabelled, and the games keep whatever the manual does pin down — movement for Captain
  America, `B` and `C` for Jurassic Park.

Thunder Force III is the one direct contradiction: its prose gives `A` speed, `B` weapon select
and `C` fire, while the configuration-screen photograph on the same spread shows TYPE 1 as
`A` SPEED CHANGE, `B` FIRE BUTTON, `C` WEAPON SELECT. The prose is the manual stating the
buttons' functions and the photograph is an illustration of a screen, so the prose is taken.
Its D-pad is unlabelled — remarkably for a shooter, no page says the pad moves the ship.

### Two readings that stayed unresolved

- **Crusader of Centy** prints `SWORD — Button A` on page 9 and `TALK — Button A` on page 10.
  One of the two is a misprint and the manual gives no third statement to break the tie, so only
  `A` = *Sword* (the button-functions page) and the D-pad are labelled; `B` and `C` are never
  given a function anywhere.
- **Bubsy** defines `C` only in combination — "Button C + ◄ I'll look to my right". *Look* is
  therefore a reading of what the modifier is for, not a function the page states on its own.

## Both lookup keys, and what each one is

**`romName` is the ROM file's basename with `(...)` and `[...]` groups stripped** — region and
revision tags — and nothing else; it is not the game's display title. The names here are
therefore taken from the `Sega Genesis` rows of `Files.xml` in a local LaunchBox metadata export,
which lists the filenames LaunchBox knows, with those groups removed. That is why nine entries
do not read like their manual's cover:

| Manual title | `romName` |
|---|---|
| Castlevania: Bloodlines | `Castlevania - Bloodlines` |
| Chakan: The Forever Man | `Chakan` |
| Contra: Hard Corps | `Contra - Hard Corps` |
| Ecco: The Tides of Time | `Ecco - The Tides of Time` |
| Shinobi III: Return of the Ninja Master | `Shinobi III - Return of the Ninja Master` |
| Street Fighter II: Special Champion Edition | `Street Fighter II' - Special Champion Edition` |
| Super Street Fighter II | `Super Street Fighter II - The New Challengers` |
| The Lion King | `Lion King, The` |
| The Revenge of Shinobi | `Revenge of Shinobi, The` |

A subtitle becomes ` - `, an article moves to the end, and the file keeps whatever punctuation
the set uses — `Street Fighter II'` carries its apostrophe. None of that is guessable from the
manual, which is the argument for reading the names out of the export rather than typing them.

**`launchBoxId` is the primary key and does not depend on the filename at all**, so it still
resolves a library named by some other convention. The IDs are the `<DatabaseID>` of the
`Sega Genesis` games in the same export (`Metadata.xml`), matched by title: punctuation, case and
article position normalised away, falling back to `<GameAlternateName>`.

One collision had to be broken by hand: normalising punctuation made `Streets of Rage` match
both the game (971) and a ROM hack, *Streets of Rage Plus*, through its alternate name
`Streets of Rage +`. An exact `<Name>` match now outranks an alternate-name match, which is the
general fix — a hack's alternate name should never outrank a real game's own title.

The export is not in the repository (`romcontrols/` is excluded), so re-deriving these needs a
LaunchBox install to export from.

## What was attempted and failed

- **Tesseract** was the first OCR choice and could not be installed: Homebrew failed with
  `NameError: uninitialized constant CurlDownloadStrategy::Timeout`, an internal bug in the
  installed Homebrew, not a missing dependency. macOS's own Vision framework replaced it and
  needed nothing installed.
- **StrategyWiki**, through the MediaWiki API, was the source for an earlier 11-game pass.
  It is thinner than the manuals and twice gave a wrong answer: a search fallback resolved
  "Streets of Rage" to *Streets of Rage 3*'s controls page, and its Sonic 2 entry put Spin Dash
  on `Down` alone, which the manual contradicts. Every game it had covered has been re-read from
  its manual, and the entries it produced now match the printed page or are gone.
- **Web search summaries** were tried and rejected: asked about Streets of Rage 2 they returned
  "B: body throw, C: used with B for super slam", which the manual contradicts. They read as
  authoritative and are not consistently right.
- **The "manual too damaged to read" verdict was mostly wrong, and the page filter was the
  reason.** This entry used to record 23 manuals as yielding no controls page the scan could
  find — Boogerman, Bubsy, Bonkers, Dragon's Fury, Judge Dredd, Jurassic Park, Crusader of
  Centy, Disney's Aladdin, Chuck Rock and Battletoads & Double Dragon among them. Scoring those
  pages instead of keyword-filtering them found a controls page in 43 of the 44 manuals that had
  been attempted and never shipped, and all 43 are now in the table above. Crusader of Centy's
  page 8 scores higher than any page in the shipped set, and Judge Dredd turned out to be a
  six-button game with no entry at all. **A filter's silence is not evidence about the page** —
  it is evidence about the filter, and reading "found nothing" as "nothing to find" cost this
  platform 43 games and every D-pad label in the first pass.

  Only **Dragon: The Bruce Lee Story** genuinely produced no entry. Its manual names functions
  (Fast Punch, Hard Punch, Fast Kick, Hard Kick) across pages 3, 4, 10, 11 and 12 but never maps
  one onto a lettered button, so there is nothing to attach a label to. Boogerman is the one
  whose scan is actually poor; its page 4 had to be re-rendered at 300 dpi to read the diagram.
