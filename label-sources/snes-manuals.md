# Super Nintendo labels — where they came from

A 20-game pilot in `assets/Defaults/Platforms/Super Nintendo Entertainment System/Labels.xml`, read
off the printed manuals and gap-filled from StrategyWiki. Sources and method follow
[`genesis-manuals.md`](genesis-manuals.md); only what differs is recorded here. There is no
A/B gate: this file and the table below are the only record of what each label rests on.

## Sources

| Source | What it is | Where it lives |
|---|---|---|
| Printed manual | 434 PDF scans under `romcontrols/manuals/Super Nintendo Entertainment System/` | not redistributable; never committed |
| StrategyWiki | each title's `/Controls` subpage, read through the MediaWiki API | not stored locally; re-read from the site |

Controls pages were found with `locate_controls.py` and read as images, per the Genesis note. The
pilot games were chosen to span the shapes the rest will take: a six-button fighter (Street Fighter
II), shooters, RPGs, racers, and games whose controls change by vehicle, view or configuration.

### The rule entries were taken under

The manual is read first and a binding stated by either source goes in, as on PS2; the manual wins
only on a direct contradiction. The wiki was asked about the six games where the manual left a gap:

- **Super Castlevania IV:** the manual's button list gives Y as whip and B as jump, but its prose
  says whip is the D-pad plus B. The wiki agrees with the list, so Y and B are labelled.
- **Super Bomberman:** the wiki agrees with the manual on B (detonate) and X/R (stop bombs). It says
  L does nothing where the manual says Y or L push bombs, so the manual wins on L. It says Select
  pauses where the manual says it is unused, so the manual wins and Select stays unlabelled.
- **Star Fox:** the manual prints four configurations (Types A to D) and names no default. The wiki
  names one, Blaster on Y and Brake on B, which matches Type A, so Y and B are labelled from it.
  The words are the manual's (*Laser Blaster*, *Retro-rocket*), not the wiki's (*Blaster*, *Brake*).
- **Contra III, Super Punch-Out!!, Secret of Mana:** nothing added. The wiki varies B, L, R and
  Select by level type for Contra III as the manual does; its Super Punch-Out!! controls page covers
  the arcade game, not this one; and it confirms Secret of Mana's entries.

## What rests on a single source

Star Fox's Y and B rest on the wiki alone for *which arrangement is the default*, so they are the
first to re-check. Every other entry is from the manual, two of them (Super Castlevania IV's Y and B,
Super Bomberman's B and X/R) with the wiki agreeing.

## Judgement calls

- **Configurable buttons:** only a stated default, or what is constant across configurations, is
  labelled. Axelay, Chrono Trigger, Mega Man X, Mortal Kombat II and Super Ghouls 'n Ghosts print
  their own arrangement as the default and are read as printed.
- **Controls that change by vehicle or view are not fully expressible.** Pilotwings keeps only Turn
  Left and Turn Right, which hold in all five vehicles; Contra III drops B, L, R and Select, which
  differ between side and top view; Star Fox drops its D-pad. Issue #46 tracks supporting several
  control sets per game, after which these are re-transcribed.
- **Wording that is the reader's, not the manual's:** Secret of Mana's *Attack* (B) and *Switch
  Hero* (Select), Super Metroid's *Cycle Special Item*, Mega Man X's *System Status*, F-Zero's
  *Shift Weight* and Donkey Kong Country's *Roll* (the first of Y's several functions).
- **Start overrides:** Mega Man X labels Start *System Status*, since its manual gives it no pause.
- **Secret of Mana's `Start = Pause` default is probably wrong.** Both sources say Start brings in
  a second or third player; the manual says Y pauses. A label cannot unset a default, so it stays.
- **Unlabelled arrows:** Super Punch-Out!!'s diagram points at X, L and R with no text.
- **Combinations dropped**, as on Genesis. Menu-only functions are not labels.

## What each entry rests on

Page numbers are PDF page indices, not the manual's printed numbering.

| Game | PDF page(s) | Read how |
|---|---|---|
| Axelay | 3, 5 | directly (diagram + list) |
| Chrono Trigger | 6, 7, 9 | directly (diagram + text) |
| Contra III: The Alien Wars | 4, 5 | directly |
| Donkey Kong Country | 6 | directly (diagram + bullets) |
| F-Zero | 4 | directly (diagram + table) |
| Final Fight | 4 | directly (table) |
| Kirby Super Star | 2 | directly (diagram, tiny arrows) |
| Mega Man X | 6, 10 | directly (list + diagram) |
| Mortal Kombat II | 4, 7 | directly (list) |
| Pilotwings | 2, 3, 5-9 | directly (diagram per vehicle) |
| Secret of Mana | 5, 7 | directly (table + diagram) |
| Star Fox | 4, 5, 6 | directly (table + four diagrams), wiki for the default |
| Street Fighter II | 3-6 | directly (table + diagram agree) |
| Super Bomberman | 2-4 | directly (table), wiki gap-fill |
| Super Castlevania IV | 5, 6 | directly (table + diagram), wiki gap-fill |
| Super Ghouls 'n Ghosts | 4, 5 | directly (table) |
| Super Mario Kart | 3, 4 | directly (table) |
| Super Mario World | 3, 4 | directly (table) |
| Super Metroid | 5, 6 | directly (table + diagram) |
| Super Punch-Out!! | 3, 5 | directly (diagram + table) |

## Both lookup keys

`romName` is the rom file's basename with `(...)` and `[...]` groups stripped, taken from the
`Super Nintendo Entertainment System` rows of `Files.xml`; `launchBoxId` is the `<DatabaseID>` of
the matching `Metadata.xml` game on that platform. Two differ from the manual's title: Contra III is
`Contra III - The Alien Wars` and Super Ghouls 'n Ghosts is `Super Ghouls'n Ghosts`. LaunchBox also
lists F-Zero Maximum Velocity (Game Boy Advance) and the arcade Street Fighter II under alternate
names that match, so the IDs are taken from the SNES platform's own `<Name>` rows only. `lb-ids.py`
lists the candidates for a set of titles.
