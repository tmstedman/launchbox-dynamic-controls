# Sony PlayStation 2 labels — where they came from

Nine games, 117 labels, in `assets/Defaults/Platforms/Sony Playstation 2/Labels.xml`.

## Sources

Two, and they are not equal in kind:

| Source | What it is | Where it lives |
|---|---|---|
| Printed manual | PDF scans under `romcontrols/manuals/Sony Playstation 2/` | not redistributable; never committed |
| StrategyWiki | each title's `/Controls` subpage, read through the MediaWiki API | not stored locally; re-read from the site |

Only Resident Evil 4's manual has a text layer. Every other page here was located with a
low-resolution contact sheet and then read as an image at 170–200 DPI.

### The rule entries were taken under

**A binding stated by either source goes in; the manual overrides only on a direct
contradiction.** This replaced an earlier asymmetric rule — manual-silence treated as disproof
of a wiki claim, but wiki-silence never treated as disproof of a manual claim — which was
arbitrary and cost real bindings. Two cases forced the change, both manuals that state a binding
*outside* their own controls section:

- **God of War II, R2 — "Swap Weapon".** Not on the Controls spread (p5) and not on
  Attacks/Magic (p8). On the *Game Screen* page (p4): "Kratos can quickly toggle between his
  Blades and the current Sub-Weapon by pressing R2."
- **Burnout 3, R2 — "Crashbreaker".** Not in COMPLETE CONTROLS (p3). On *Crash Events*:
  "Cause enough vehicles to crash and you can press the R2 button to detonate your car."

A manual's controls table is not a complete statement of its own game's controls. Read the
whole book.

## What rests on StrategyWiki alone

These are the single-sourced entries, resting on the weaker of the two sources with no printed
backing, so they are the first to re-check and the first to go if the wiki turns out to be wrong
about one of these games. All five were re-read against the manuals on 2026-10-23 to confirm the
manual really is silent.

| Game | Input | Label | What the manual says instead |
|---|---|---|---|
| Burnout 3: Takedown | `Dpad-Any` | Steer | COMPLETE CONTROLS (p3) gives "Steering: left analog stick"; the D-button appears only under MENU CONTROLS, for highlighting items and cycling sliders |
| Shadow of the Colossus | `Dpad-Up` | Draw Sword | EQUIP & OVERVIEW (p18): "press ← or → on the directional buttons to equip your weapons" — left and right only |
| Shadow of the Colossus | `Dpad-Down` | Stow Weapon | as above |
| Shadow of the Colossus | `L1` | Lock On | absent; see below |
| Shadow of the Colossus | `L2` | Reset Camera | absent; see below |

**Shadow of the Colossus names no shoulder button but R1.** Its CONTROLS page (p6) is a
hardware diagram only — it labels L1, L2, R1, R2 on the controller and assigns no action to any
of them. Every binding in that game therefore rests on a prose page, and L1/L2 appear on none of
the five content spreads that carry the verbs: swimming/climbing (p12–13), riding/stance
(p16–17), equip/using the sword (p18–19), stabbing/bow (p20–21).

### Two that look like they belong here and do not

- **Burnout 3, `R2` "Crashbreaker"** came from the wiki first, but the manual's Crash Events
  page then confirmed it. Manual-backed.
- **Katamari Damacy, `Cross` "Take Picture" / `Circle` "Picture Zoom"** came from the
  **manual's** own 1P/2P table for Camera Prince mode, not from the wiki at all.
- **Shadow of the Colossus, `Cross` "Call Agro"** is manual-backed: WHISTLING (p14), "...call
  Agro with the ⊗ button...". An earlier draft of this file implied the manual said something
  different; it does not.

## What each entry rests on

| Game | Manual pages read | Verified | Notes |
|---|---|---|---|
| Bully | controls page | from the original pass | — |
| Burnout 3: Takedown | 3 (COMPLETE CONTROLS), Crash Events | **2026-10-23** | `Square` = Brake confirmed by cropping and upscaling 3×; at page scale the glyph reads as Circle |
| God of War | controls spread | from the original pass | `L3`/`R3` both "Rage" |
| God of War II | 4 (Game Screen), 5 (Controls), 8 (Attacks/Magic) | from the original pass | Rage of the Titans is named by neither source and is deliberately blank rather than inferred from the first game's L3+R3 |
| Ico | controls spread (left half) | from the original pass | the right half of the spread is blank background |
| Katamari Damacy | controls + 1P/2P table | from the original pass | — |
| Killzone | controls spread | from the original pass | — |
| Resident Evil 4 | text layer (`pdftotext -layout`) | from the original pass | the only manual here with extractable text |
| Shadow of the Colossus | 6, 12–15, 16–17, 18–19, 20–21 | **2026-10-23** | no controls table exists; see above |

"From the original pass" means the entry was taken on 2026-10-22 and has **not** been re-read
since. Those seven need the same treatment the two verified games got before this file can be
called complete — particularly the negative claims, which are the ones that went wrong twice.

## Judgement calls

- **Wording follows the manual where the two sources differ on words but agree on the binding.**
  Ico's `Cross` is "Release" (manual) not "Drop down" (wiki); God of War's right stick is
  "Evade" (manual) not "Dodge" (wiki).
- **Shadow of the Colossus `Start` is "Map", not the inherited "Pause".** The manual: "the map
  shows your journeys past and present, press the START button to view", and Options opens from
  Map View.
- **A button that does two distinct things gets both, joined with " / ".** This is the
  convention the Arcade pack already uses — 947 of 19,726 shipped labels carry it, 813 with two
  parts and 124 with three. Length is the only limit: the median shipped label is 5 characters,
  the 99th percentile 21, and the longest 28. Two parts almost always fit; three need checking
  against a render.

  | Game | Input | Label | Where each half comes from |
  |---|---|---|---|
  | Shadow of the Colossus | `R1` | Grip / Duck | hold a climbing surface (p13); duck on horseback (p17) |
  | Shadow of the Colossus | `Triangle` | Jump / Mount | jump or pull yourself up while climbing (p13); mount and dismount Agro (p15) |
  | Shadow of the Colossus | `Cross` | Call Agro / Spur | call Agro (p14); spur the horse forward (p16) |
  | Burnout 3: Takedown | `R1` | Boost / Impact Time | both listed under GAMEPLAY CONTROLS (p3) |

  Shadow of the Colossus's `R1` has two further uses — swimming faster (p12) and mounting
  faster (p15) — left out as modifiers rather than actions. Picking which two to name is a
  judgement, and the rule applied here is: the uses a player would look the button up for.

- **Menu-only bindings are not labelled.** Burnout 3's table gives `Cross` as "Select/Go to next
  screen" and `Triangle` as "Return to previous screen" alongside their gameplay roles. The
  overlay describes play, and menu navigation is near-universal across games, so those halves
  are dropped rather than joined.

## Unresolved

- **God of War II's Rage of the Titans meter** (manual p5) is named by neither source. Left
  blank rather than inferred.
- **Seven of the nine games are unverified** against their manuals since the original pass.
