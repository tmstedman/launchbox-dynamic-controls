# Arcade artwork transcription — judgement calls

Per `docs/label-import.md`'s "Every judgement call gets recorded, not just made": a separate,
hand-maintained list of decisions that were *checked and kept as transcribed*, not "corrected"
to a more expected pattern. These are not open questions — that's what `review-notes.md`
(regenerated from `<Note>` in `arcade-artwork.xml`) is for. This file exists so a future pass
doesn't quietly re-break a correct-but-unusual reading, and so a decision can be agreed or
disagreed with on sight without redoing the research behind it.

Append to this file directly during a batch — don't just mention a finding in chat.

- **imolagp** — `ButtonLeftShoulder`="Brake", `AxisTriggerRight`="Accelerate": kept this
  asymmetric pairing (Brake on the shoulder, Accelerate on the trigger) rather than moving Brake
  to match Accelerate's row. Most racing cards in this pack pair Brake/Accelerate on the same
  row (both shoulders, e.g. `hyprdriv`, or both triggers, e.g. `hangon`/`harddriv`); this one
  genuinely doesn't.
- **gticlub** — `ButtonLeftShoulder`/`ButtonB` both ="Shift Down" (and `ButtonRightShoulder`/
  `ButtonX` both ="Shift Up"): kept as a redundant physical mapping rather than a misread. User
  confirmed this is correct against the game's cfg.
- **gunfight** — `AxisRightStick`="Move Shoot Postiton Up/Down": kept the card's own apparent
  typo ("Postiton") verbatim rather than silently correcting it.
- **hangonjr** — no `AxisTriggerLeft`/Brake label at all, unlike sibling cards `hangon`/
  `harddriv` (both have Brake+Accelerate): kept as drawn, not inferred from the siblings.
- **hidctch3** — `ButtonDpad` left unlabeled (only `AxisLeftStick`="Move"), unlike its family
  (`hidctch2`, `hidnctch`, `hidnc2k`), which label both the stick and the D-pad.
- **hyprdriv** — only three of four D-pad directions labeled ("View 1"/"View 2"/"View 3"; the
  fourth left unlabeled, no "View 4" invented). Originally recorded as `Up`/`Down`/`Left`,
  assuming top-to-bottom icon order Up/Down/Left/Right; **corrected** to `Up`/`Left`/`Right`
  (leaving `Down` unlabeled) after the Pop'n Music batch pixel-zoomed the actual icon stacking
  order and found it's Up/Left/Right/Down — see the `popn1`–`popn8` entry below.
- **hangplt** — only `ButtonDpadLeft`/`Right` labeled ("Menu Left"/"Menu Right");
  `ButtonDpadUp`/`Down` left unlabeled.
- **headonch**, **hiimpact** — `ButtonA`="Button": kept the card's literal generic caption
  verbatim rather than inventing a game-specific action name.
- **indyheat** — `AxisTriggerRight`="Accelerate", no Brake label at all: same shape as
  `hangonjr` — a racing card with only Accelerate printed, kept as drawn rather than assumed to
  be missing a Brake label by analogy to other racing cards in the pack.
- **jajamaru** — `ButtonA`="A", `ButtonX`="B": kept these literal single-letter captions
  verbatim (same pattern as `headonch`/`hiimpact`'s "Button") rather than treated as a
  placeholder or omitted.
- **jdredd** — `AxisLeftStick`="Move Crosshair", `ButtonDpad` left unlabeled: unlike the
  majority pattern in this pack (a "Move"/"Move" pair on both the stick and D-pad), this card
  only labels the stick. Kept as drawn.
- **jdreddp** — same shape as `jdredd`: `AxisLeftStick`="Move", `ButtonDpad` left unlabeled,
  despite being a different card (Midway release vs. Capcom/Sony ZN-1) for the same game.
- **jetwave** — `AxisLeftStick`="Turn", `ButtonDpad` left unlabeled: same single-stick-only
  shape as `jdredd`/`jdreddp`, here on a driving game rather than a shooter.
- **jongbou**, **jongbou2** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: same
  single-stick-only shape as `jdredd`/`jdreddp`/`jetwave`, here on two Mahjong tile games.
- **joyfulr** — `ButtonLeftShoulder`="Arm Left", `AxisTriggerRight`="Arm Right": kept this
  asymmetric pairing (left shoulder, right trigger — not matching rows) rather than moving one
  to mirror the other. Same shape of decision as `imolagp`'s Brake/Accelerate pairing.
- **jumbogod** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, unlike every other card in the pack seen so far, which labels at least one. Kept as
  drawn rather than assumed to be a miss — this card genuinely has no "Move" printed anywhere.
- **kabukikl** — `ButtonLeftShoulder`="Strong Attack", `ButtonRightShoulder`="Strong Kick",
  `AxisTriggerRight`="Super Attack": three of the four shoulder/trigger positions are labeled
  but `AxisTriggerLeft` (LT) is not — checked explicitly against the LB/LT vertical-alignment
  rule rather than assumed to be a misplaced fourth label.
- **kattobas** — no movement label at all, same shape as `jumbogod`: the left stick icon carries
  no "Move"/"Steering" text and the D-pad is unlabeled, only `ButtonA`="Bat" is drawn. Kept as
  drawn.
- **klax** — `ButtonA`="Button": same literal generic caption pattern as `headonch`/`hiimpact`/
  `jajamaru` — kept verbatim rather than invented.
- **kof2002** — `AxisTriggerRight` (RT) left unlabeled, unlike every neighboring King of Fighters
  card in this batch (`kof2000`/`2001` both label RT "Charge"; `kof2003` labels it "Tag In"): a
  single break in an otherwise consistent family pattern, checked against the vertical-alignment
  rule rather than assumed to be a missed "Charge" label.
- **kof2003** — `ButtonRightShoulder` (RB) left unlabeled while `AxisTriggerLeft` (LT) and
  `AxisTriggerRight` (RT) both print "Tag In": unlike every other King of Fighters card in this
  batch, which labels RB ("Very Strong Attack" or "Grab"). Kept exactly as drawn — both triggers
  independently reading "Tag In" is unusual but that is what the card shows, not what a sibling
  card would predict.
- **konami88** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same shape as `jumbogod`/`kattobas`/`korokoro`/`koropens`. Kept as drawn.
- **konamigt** — `ButtonLeftShoulder`="Brake", `AxisTriggerRight`="Accelerate": kept this
  asymmetric pairing (Brake on the shoulder, Accelerate on the trigger) rather than moving Brake
  to match Accelerate's row — same shape of decision as `imolagp`'s and `joyfulr`'s pairings.
- **korokoro** — no movement label at all, same shape as `jumbogod`/`kattobas`: only
  `ButtonX`="(Tap) Roll Dice" is drawn. Kept as drawn.
- **koropens** — no movement label at all, same shape as `korokoro`: only `ButtonA`="Bowl" is
  drawn. Kept as drawn.
- **kroozr** — `AxisRightStick`="Move Shoot Postiton": kept the card's own apparent typo
  ("Postiton") verbatim, same misspelling already seen on `gunfight`'s card.
- **kzaurus** — no movement label at all, same shape as `jumbogod`/`kattobas`/`konami88`/
  `korokoro`/`koropens`: only `ButtonA`="Throw" is drawn. Kept as drawn.
- **lagunar** — `AxisTriggerRight`="Accelerate", no Brake label at all: same shape as
  `hangonjr`/`indyheat` — a racing card with only Accelerate printed, kept as drawn.
- **lastkm** — no movement/steering label at all: neither `AxisLeftStick` nor `ButtonDpad`
  carries any text, same shape as `jumbogod`/`kattobas`/`konami88`/`korokoro`/`koropens`/
  `kzaurus` — unusual here specifically because it's a driving game (gear-shift controls are
  fully labeled), not one of the pack's usual no-movement single-action games. Kept as drawn.
- **lbowling** — `ButtonX`="A Button": the physical X button's own caption literally names a
  different button ("A Button") rather than a game action. Kept exactly as printed, same
  literal-caption handling as `headonch`/`hiimpact`/`jajamaru`/`klax`, not corrected or
  reassigned to `ButtonA`.
- **luckywld** — `ButtonLeftShoulder`="Brake", `AxisTriggerRight`="Accelerate": kept this
  asymmetric pairing rather than moving Brake to match Accelerate's row — same shape of decision
  as `imolagp`'s, `joyfulr`'s, and `konamigt`'s pairings.
- **lufykzku** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same shape as `jumbogod`/`kattobas`/`konami88`/`korokoro`/`koropens`/`kzaurus`/`lastkm`.
  Kept as drawn — only `ButtonA`="Punch" is on the card.
- **mag_pdak** — `ButtonLeftShoulder`="Brake", `AxisTriggerRight`="Accelerate": kept this
  asymmetric pairing rather than moving Brake to match Accelerate's row — same shape of decision
  as `imolagp`'s, `joyfulr`'s, `konamigt`'s, and `luckywld`'s pairings.
- **mag_time** — `ButtonLeftShoulder`/`ButtonX` both ="Left Flipper" (and `ButtonRightShoulder`/
  `ButtonA` both ="Right Flipper"): kept as a redundant physical mapping rather than a misread —
  same shape as `gticlub`'s confirmed-correct Shift Down/Up pairing.
- **magspeed** — `ButtonDpadUp`="Select Card 1", `ButtonDpadLeft`="Select Card 2",
  `ButtonDpadRight`="Select Card 4", `ButtonDpadDown`="Select Card 3": non-sequential card
  numbering (1, 2, 4, 3), kept exactly as printed rather than "corrected" to 1-2-3-4 order.
  The Up/Down/Left/Right *assignment* was originally inferred from `hyprdriv`'s (unverified)
  icon-stacking precedent and got it wrong; **corrected** after pixel-zooming this card's own
  D-pad icon column directly (cropped and enlarged via `sips`) and confirming top-to-bottom
  order Up/Left/Right/Down, matching the independent Pop'n Music finding below.
- **magicstk** — no movement label at all on either `AxisLeftStick` or `ButtonDpad`, only
  `ButtonA`="Button": unlike most cards in this pack, which label movement even when the rest of
  the card is sparse. Kept as drawn.
- **maletmad** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: unlike the pack's usual
  "Move"/"Move" pair on both stick and D-pad, this lightgun card's leader line only reaches the
  analog stick. Also `AxisTriggerRight` and `ButtonA` both ="Shoot" — a redundant mapping, same
  shape as `gticlub`'s confirmed-correct pairing.
- **mainsnk** — no "Move" label anywhere on the card at all: `AxisLeftStick`="Left Hand",
  `AxisRightStick`="Right Hand" (both analog sticks are attack controls, not navigation), and
  movement is instead a modifier — `ButtonRightShoulder` and `ButtonX` both ="(Hold) Move". Also
  asymmetric left/right: the right side labels both `ButtonRightShoulder` ("(Hold) Move") and
  `AxisTriggerRight` ("Right Uppercut"), but the left side labels only `AxisTriggerLeft` ("Left
  Uppercut") — `ButtonLeftShoulder` has no matching "(Hold) Move" text, unlike its mirror on the
  right. Both kept exactly as drawn rather than assumed symmetric with the right side.
- **marioun** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text; the card's six labeled controls (`ButtonLeftShoulder`/`RightShoulder`/`Y`/`B`/`A`/`X`) are
  all named by their physical color/position ("Left Grey", "Blue", etc.), not an action — same
  no-movement shape as `jumbogod`/`kattobas`/etc. Kept as drawn.
- **matrim** — `ButtonLeftShoulder`="Roll / Evade", `ButtonRightShoulder`="Crush Attack",
  `AxisTriggerRight`="Tag Enabler": three of the four shoulder/trigger positions labeled,
  `AxisTriggerLeft` (LT) left unlabeled — checked against the vertical-alignment rule rather than
  assumed to be a missed fourth label. Same shape as `kabukikl`'s LT gap.
- **maxforce** — `ButtonRightShoulder`, `AxisTriggerRight`, and `ButtonA` all ="Shoot": three
  separate controls redundantly mapped to the same action, kept as drawn rather than treated as a
  misread — same shape as `gticlub`'s/`mag_time`'s confirmed-correct redundant pairings, just
  three-way instead of two.
- **maxrpm** — two things kept as drawn rather than corrected: (1) `ButtonLeftShoulder`/`ButtonB`
  both ="Shift Down" and `ButtonRightShoulder`/`ButtonX` both ="Shift Up", a redundant mapping
  matching `gticlub`'s confirmed-correct pattern; (2) no `AxisTriggerLeft`/Brake label at all
  (only `AxisTriggerRight`="Accelerate"), same shape as `hangonjr`/`indyheat`/`lagunar`.
- **mchampdx** — `ButtonA`="Button B", `ButtonX`="Button A": each control's caption literally
  names the *other* physical button, not its own — a genuinely confusing mislabel (or the game's
  own internal button naming diverging from position), but kept exactly as printed rather than
  swapped or corrected. Same literal-caption handling as `lbowling`'s "A Button" on `ButtonX`.
- **megablst** — `ButtonA`/`ButtonX` both ="Shoot": redundant mapping across two face buttons,
  kept as drawn — same shape as `gticlub`'s/`mag_time`'s confirmed-correct redundant pairings.
- **megadon** — `ButtonA`="(Hold) Move": an unusual parenthetical movement instruction on a face
  button, distinct from the dedicated `AxisLeftStick`/`ButtonDpad` movement controls already on
  the card. Kept exactly as printed rather than folded into or treated as duplicating "Move."
- **mk**, **mk2** — `ButtonLeftShoulder`/`RightShoulder` both ="Block" (redundant mapping, same
  shape as `gticlub`'s confirmed-correct pairing), and `ButtonY`/`ButtonA` both ="Low Kick" —
  kept as drawn. Worth noting `mk3`/`mk4` (same template, transcribed in this same batch) don't
  share the Y/A redundancy: `mk3` keeps Y/A as "Low Kick" too but changes LB to "Run", while
  `mk4` breaks the pair entirely (`ButtonY`="High Punch"), so this isn't a template artifact —
  each card's redundancy (or lack of it) was checked individually.
- **mmagic** — `ButtonA`="Button": same literal generic caption pattern as `headonch`/
  `hiimpact`/`jajamaru`/`klax` — kept verbatim rather than invented.
- **mnchmobl** — `ButtonLeftShoulder`="Arm Left", `AxisTriggerRight`="Arm Right": same
  asymmetric shoulder/trigger pairing already established for `joyfulr`, its sibling rom name on
  the same combo card ("Joyful Road / Munch Mobile") — matched rather than re-derived from a
  fresh, less certain vertical-alignment read of this second image file.
- **monymony** — `ButtonA`="Button": same literal generic caption pattern as `headonch`/
  `hiimpact`/`jajamaru`/`klax`/`mmagic` — kept verbatim rather than invented.
- **montecar** — `AxisTriggerRight`="Accelerate", no Brake label at all: same shape as
  `hangonjr`/`indyheat`/`lagunar`/`maxrpm` — kept as drawn rather than assumed missing.
- **moonal2**, **moonaln** — no `ButtonStart` label at all, unlike sibling cards `mooncrst`/
  `moonqsr` (same "Moon" family, both show "Start" plainly) — checked explicitly per the header's
  documented missing-Start-label risk, not assumed present by analogy to the siblings.
- **moonwar** — `AxisTriggerRight`/`ButtonA` both ="Thrust": redundant mapping across a trigger
  and a face button, kept as drawn — same shape as `gticlub`'s/`mag_time`'s/`megablst`'s
  confirmed-correct redundant pairings.
- **moremore**, **moremorp** — `ButtonA`="B", `ButtonX`="A": each control's caption literally
  names the *other* physical button, same swapped-letter shape as `mchampdx`'s "Button B"/
  "Button A". Kept exactly as printed on both cards (identical template, "More More" and "More
  More Plus") rather than corrected to match physical position.
- **mouseatk** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. — only
  `ButtonA`="WHACK!!" is on the card. Kept as drawn.
- **mplanets** — two things kept as drawn rather than corrected: (1) `ButtonLeftShoulder`="Rotate
  Left" paired with `AxisTriggerRight`="Rotate Right" — an asymmetric shoulder/trigger pairing,
  same shape of decision as `imolagp`'s/`joyfulr`'s/`konamigt`'s pairings; (2)
  `ButtonRightShoulder`, `ButtonA`, and `ButtonX` all ="Shoot" — three separate controls
  redundantly mapped to the same action, same shape as `maxforce`'s three-way "Shoot" redundancy.
  A fourth control, `AxisRightStick`="Rotate", is the whole-stick circle below the D-pad — read
  as `AxisRightStick` per the established convention, not `ButtonRightStick`.
- **mslugx** — `ButtonY` left unlabeled ("Full Frontal Tank Attack" absent), unlike sibling cards
  `mslug4`/`mslug5` (same template, both label it), kept as drawn — this entry in the series
  genuinely doesn't have a tank-attack move printed.
- **multchmp** — `ButtonA`="Button B", `ButtonX`="Button A": each control's caption literally
  names the *other* physical button, same swapped-letter shape as `mchampdx`/`moremore`. Kept
  exactly as printed.
- **musclhit** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text (the analog stick icon on this card has no leader line to it at all), same no-movement
  shape as `jumbogod`/`kattobas`/etc. Only `ButtonA`="Bat" is drawn.
- **mushitam** — `AxisLeftStick`="Pull Down To Launch": an unusual launcher-mechanic instruction
  in place of the pack's usual "Move", kept exactly as printed; `ButtonDpad` left unlabeled, and
  `ButtonX`="Button" is the same literal generic caption as `headonch`/`hiimpact`/etc.
- **musicbal** — `ButtonLeftShoulder`/`ButtonX` both ="Left Flipper" (and
  `ButtonRightShoulder`/`ButtonA` both ="Right Flipper"): redundant mapping, same shape as
  `gticlub`'s/`mag_time`'s confirmed-correct pairings.
- **mutantf1** — card image is identical to `mutantf`'s and prints only "mutantf" (never
  "mutantf1"); transcribed with `romName="mutantf1"` to match the pack's filename, same content
  as `mutantf` — a clone reusing its parent's card, not a cross-image contradiction.
- **navarone** — `ButtonA`="Button": kept the card's literal generic caption verbatim, same
  shape as `headonch`/`hiimpact`/`monymony`.
- **nametune** — no movement label at all (`AxisLeftStick`/`ButtonDpad` both unlabeled); the
  card's Y/B/A/X are numbered "4"/"3"/"2"/"1" instead of named actions — a trivia/quiz game with
  no movement at all, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc.
- **nbahangt**, **nbajam**, **nbajamte**, **nbamht** — `AxisTriggerRight`/`ButtonB` both ="(Hold)
  Turbo": redundant mapping across a trigger and a face button, kept as drawn on all four
  near-identical sibling cards — same shape as `gticlub`'s/`mag_time`'s/`megablst`'s confirmed-
  correct redundant pairings. `nbashowt`/`nbajamex` (same template family, same batch) don't
  share it — `nbashowt` only labels `ButtonB`="Turbo" (RT unlabeled) and `nbajamex` labels neither
  — checked individually rather than assumed uniform across the family.
- **ncv1**, **ncv2** — `ButtonA`="B Button", `ButtonX`="A Button": each control's caption
  literally names the *other* physical button, same swapped-letter shape as `mchampdx`/
  `moremore`/`multchmp`. Kept exactly as printed on both volumes.
- **neodrift** — `ButtonLeftShoulder`/`ButtonA` both ="Brake" (and `AxisTriggerRight`/`ButtonX`
  both ="Accelerate"): redundant mapping across a shoulder/face-button pair and a trigger/face-
  button pair, kept as drawn — same shape as `gticlub`'s/`mag_time`'s confirmed-correct
  redundant pairings.
- **nitedrvr** — only `ButtonDpadUp`/`Down`/`Left` labeled ("Expert Track"/"Novice Track"/"Pro
  Track"); `ButtonDpadRight` left unlabeled — same partial-D-pad shape as `hyprdriv`. Also
  `AxisTriggerRight`="Accelerate" with no Brake label anywhere on the card, same shape as
  `hangonjr`/`indyheat`/`lagunar`/`montecar`.
- **nmg5** — `ButtonA`="B", `ButtonX`="A": each control literally names the other physical
  button, kept exactly as printed — same swapped-letter shape as `mchampdx`/`moremore`/`ncv1`/
  `ncv2`.
- **ninja** — `AxisTriggerRight`/`ButtonB` both ="Special": redundant trigger/face-button
  mapping, kept as drawn — same shape as `gticlub`'s confirmed-correct redundant pairings.
- **nouryoku** — `ButtonA`="B", `ButtonX`="A": same swapped-letter shape as `mchampdx`/
  `moremore`/`ncv1`/`ncv2`/`nmg5`, kept exactly as printed.
- **nss_actr**, **nss_adam**, **nss_aten**, **nss_fzer**, **nss_skin** — a new recurring
  template, not seen before this batch: all five "(Nintendo Super System)" cabinet-conversion
  cards print `ButtonLeftShoulder`="L", `ButtonRightShoulder`="R" (bare shoulder-letter
  captions, not game actions), plus a systematic `ButtonY`="X", `ButtonB`="A", `ButtonA`="B",
  `ButtonX`="Y" cross-mapping — the SNES's own button layout named onto this project's Xbox-style
  slots, not a misread. Worth checking future NSS-family cards against this same template rather
  than re-deriving it each time.
- **nvs_machrider**, **nvs_machridera**, **nvs_mightybj**, **nvs_platoon** — a second new
  recurring template: all four "Vs." series (NES arcade conversion) cards print `ButtonA`="A"
  (self-consistent) and `ButtonX`="B" — the original two-button NES pad's A/B naming carried
  onto this project's A/X slots. Distinct from the NSS template above (different console,
  different remap shape); worth checking future "Vs."-family cards against this template too.
- **orunners**, **orunnersu** — `ButtonDpadLeft`="Skip Music Track",
  `ButtonDpadRight`="Skip Music Track" (Up and Down left unlabeled): only 2 of the 4 stacked
  D-pad direction icons on this card carry text — both read the identical text, so which two
  slots carry it doesn't change what's displayed, but originally recorded as `Down`/`Left`
  (assuming top-to-bottom order Up/Down/Left/Right); **corrected** to `Left`/`Right` after the
  Pop'n Music batch pixel-zoomed the actual icon order and found it's Up/Left/Right/Down — see
  the `popn1`–`popn8` entry below. Both rom images (`orunners.jpg`, `orunnersu.jpg`) show the
  identical card, captioned with both rom names together, so the same labels apply to both
  `<Game>` entries.
- **paperboy** — `ButtonA`="Thtow Papaer": kept the card's own apparent typo verbatim (should
  read "Throw Paper"), same handling as `gunfight`'s/`kroozr`'s "Postiton".
- **panicr** — `ButtonLeftShoulder`/`ButtonX` both ="Left Flipper" (and `ButtonRightShoulder`/
  `ButtonA` both ="Right Flipper"): kept as a redundant physical mapping rather than a misread,
  same shape as `gticlub`'s/`mag_time`'s/`musicbal`'s confirmed-correct pinball flipper pairings.
- **pbillian** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `hidctch3`/`jdredd`/`jdreddp`/`jetwave`/`jongbou`/`jongbou2`/`maletmad`.
- **pasha2** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/`korokoro`/`koropens`/
  `kzaurus`/`lastkm`/`lufykzku`/`magicstk`/`marioun`/`mouseatk`/`musclhit`/`nametune`. Only
  `ButtonB`/`ButtonA`/`ButtonX` ("Blue"/"Green"/"Red") are drawn.
- **pblbeach** — `ButtonA`="Button": kept the card's literal generic caption verbatim rather
  than the "Shoot" its Puzzle Bobble/Bust-A-Move siblings in the same batch all use, same
  pattern as `headonch`/`hiimpact`/`monymony`/`klax`/`navarone`.
- **pclubys** — `ButtonA`="B", `ButtonX`="A": the card's two face-button captions are swapped
  relative to every other card in this pack's generic Xbox-controller template family (which
  reads `ButtonA`="A", `ButtonX`="B" — e.g. the PlayChoice-10 cards `pc_1942`/`pc_bball`/etc.).
  Kept as drawn rather than corrected to match the family, same shape as `jajamaru`'s/`klax`'s
  literal single-letter captions kept verbatim.
- **pgm3in1** — `ButtonY`="D", `ButtonB`="C", `ButtonA`="B", `ButtonX`="A": a fighting-game
  four-button naming (A/B/C/D) carried onto this project's Y/B/A/X slots, not a misread. Kept
  exactly as printed.
- **phozon** — `ButtonX`="Button": kept the card's literal generic caption verbatim, same
  pattern as `headonch`/`hiimpact`/`klax`/`mmagic`/`monymony`/`navarone`/`pblbeach`.
- **pikkaric** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/`korokoro`/`koropens`/
  `kzaurus`/`lastkm`/`lufykzku`/`magicstk`/`marioun`/`mouseatk`/`musclhit`/`nametune`/`pasha2`.
  Only `ButtonA`="Snap Photo" is drawn.
- **piratesh** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `hidctch3`/`jdredd`/`jdreddp`/`jetwave`/`jongbou`/`jongbou2`/`maletmad`/`pbillian`.
- **pkgnsh**, **pkgnshdx** — `AxisLeftStick`="Power" (not "Move"), `ButtonDpad` left unlabeled:
  both the unusual "Power" wording (in place of the pack's usual "Move"/"Steering") and the
  single-stick-only shape (same as `piratesh`) checked and kept as drawn on both DX and
  non-DX cards.
- **pkscram** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. — only the four face
  buttons ("Top Right"/"Bottom Right"/"Bottom Left"/"Top Left") and Start/Insert Coin are drawn.
- **plgirls** — `ButtonX`="Button": kept the card's literal generic caption verbatim, same
  pattern as `headonch`/`hiimpact`/`klax`/`phozon`/`mmagic`/`monymony`/`navarone`/`pblbeach`.
- **pkladies** — D-pad card-slot assignment ("Up" unlabeled, "Left"="Card 1", "Right"="Card 3",
  "Down"="Card 2"): originally recorded as Left="Card 1"/Right="Card 3"/Down="Card 2" from the
  same (wrong) assumed top-to-bottom order as `magspeed`/`orunners`. First corrected (wrongly)
  to Left="Card 2"/Right="Card 1"/Down="Card 3" after the Pop'n Music batch's Up/Left/Right/Down
  finding — that correction mis-derived the rotation and was itself wrong. **Re-corrected** back
  to Left="Card 1"/Right="Card 3"/Down="Card 2" (matching the original reading exactly) after a
  direct pixel-zoom re-check of this card specifically confirmed that reading was right all
  along — see
  the `popn1`–`popn8` entry below.
- **pnyaa** — `ButtonA`="Rotate", `ButtonX`="Rotate": both face buttons carry the identical
  literal caption rather than a distinguishing pair like `pnickj`'s "Rotate Right"/"Rotate Left".
  Checked and kept as printed — the card genuinely doesn't distinguish the two rotate directions
  in text.
- **popbounc** — `ButtonX`="Button": kept the card's literal generic caption verbatim, same
  pattern as `headonch`/`hiimpact`/`klax`/`phozon`/`mmagic`/`monymony`/`navarone`/`pblbeach`/
  `plgirls`.
- **popn1**–**popn8**, **popnanm**, **popnanm2**, **popnmt**, **popnstex** (Pop'n Music family,
  12 near-identical cards) — `ButtonDpadLeft`="Button 3", `ButtonDpadRight`="Button 4" (Up and
  Down left unlabeled). This was pixel-zoomed rather than assumed: cropping and enlarging the
  four stacked D-pad icons showed each one is the same clover glyph with a different single
  petal lit — top, left, right, bottom in that stacking order — so icon 2 (top-to-bottom) is
  Left and icon 3 is Right, not Down/Left as the convention recorded on `magspeed`/`orunners`/
  `pkladies` (itself inherited from `hyprdriv`'s unverified View1/2/3 reading) would predict.
  The Button 1–9 sequence assigning cleanly onto LT/LB/DpadLeft/DpadRight/X/A/B/Y/RB in numeric
  order is what gives confidence in this reading. This meant the top-to-bottom order assumed
  for `hyprdriv`, `magspeed`, `orunners`/`orunnersu`, and `pkladies` (Up/Down/Left/Right) was
  wrong — confirmed by independently pixel-zooming `magspeed`'s own D-pad icon column directly
  (see its entry above) — so all four have since been **corrected** to Up/Left/Right/Down in
  `arcade-artwork.xml`; see each one's own entry above for the specific correction applied.
- **potogold** — no face-button label at all: `ButtonY`/`ButtonB`/`ButtonA`/`ButtonX` are all
  unlabeled, unlike sibling Arcade Classics cards in this same batch (`popobear`, `popper`,
  `porky`, `portrait`, `potopoto`, `powerbal`, all of which label at least one action). Only
  `AxisLeftStick`/`ButtonDpad` (both "Move"), `ButtonStart`, and `ButtonBack` are printed. Kept
  as drawn rather than guessing an action from the sibling cards.
- **ppsatan** — `AxisLeftStick`="Move Crosshair", `ButtonDpad` left unlabeled (and `ButtonX` also
  unlabeled despite `ButtonA`="Hit"): single-stick-only shape, same as `jdredd`'s established
  "Move Crosshair" pattern.
- **primrag2** — `ButtonY`="Feirce Mid", `ButtonB`="Feirce Low": kept the card's own apparent
  typo ("Feirce" for "Fierce") verbatim, same handling as `gunfight`'s/`kroozr`'s/`paperboy`'s
  misspellings. Sibling card `primrage` (same batch, different publisher card) spells it
  correctly ("Fierce High"/"Fierce Low"), confirming this is the card's own error and not a
  misread carried over from the sibling.
- **profpac** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, unusual for a Pac-Man-style maze game but the same no-movement shape as
  `jumbogod`/`kattobas`/etc. Only the three face buttons ("C Button"/"B Button"/"A Button") and
  Start/Insert Coin are printed.
- **propcycl** — `AxisRightStick`="Throttle": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `mplanets`'s "Rotate". Paired with `ButtonLeftShoulder`="Throttle Down"/
  `ButtonRightShoulder`="Throttle Up" — a game with two separate throttle controls (coarse via
  shoulders, fine via the stick), kept as drawn.
- **prosport**, **pspikes** — `ButtonX`="Button": kept the card's literal generic caption
  verbatim, same pattern as `headonch`/`hiimpact`/`klax`/`phozon`/`mmagic`/`monymony`/
  `navarone`/`pblbeach`/`plgirls`/`popbounc`.
- **puckpkmn** — `ButtonA`="Button": kept the card's literal generic caption verbatim, same
  pattern as `headonch`/`hiimpact`/`klax`/`phozon`/`mmagic`/`monymony`/`navarone`/`pblbeach`/
  `plgirls`/`popbounc`/`prosport`/`pspikes`.
- **puyopuy2**, **puyosun** — `ButtonA`/`ButtonX` both ="Spin Piece": redundant mapping across
  two face buttons, kept as drawn — same shape as `gticlub`'s/`megablst`'s confirmed-correct
  redundant pairings. The series' first card, `puyo`, only labels `ButtonX`="Spin Piece" —
  checked individually rather than assumed uniform across the series.
- **pwrchanc**, **pwrkick** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad`
  carries any text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. `pwrchanc`
  only labels `ButtonA`="Throw"; `pwrkick` labels `ButtonB`/`ButtonA`/`ButtonX` ("Shoot
  Right"/"Shoot Middle"/"Shoot Left") but nothing for movement.
- **pwrflip** — `AxisLeftStick`="Tilt", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `jdredd`/`pbillian`/`piratesh`, but with "Tilt" in place of the pack's usual "Move" —
  same unusual-wording shape as `pkgnsh`/`pkgnshdx`'s "Power".
- **pzlestar** — no face-button label at all (`ButtonY`/`ButtonB`/`ButtonA`/`ButtonX` all
  unlabeled), only `AxisLeftStick`/`ButtonDpad` (both "Move") plus Start/Insert Coin: checked
  carefully against its same-titled but different-rom sibling `puzlstar` ("Puzzle Star", no
  subtitle), which does label B/A/X — the two are distinct games, not a duplicate read.
- **qos**, **quizvadr** — `ButtonY`="Pass", `ButtonB`="C", `ButtonA`="B", `ButtonX`="A": a
  multiple-choice quiz game's own answer lettering (A/B/C plus Pass) carried onto the Y/B/A/X
  slots, not a misread — same shape as `pgm3in1`'s fighting-game A/B/C/D lettering and
  `pclubys`'s single-letter swap. Two near-identical Arcade Classics cards, checked
  individually; both also read `ButtonLeftShoulder`="Collect", `ButtonRightShoulder`="Continue",
  `AxisTriggerRight`="Bonus" by exact row alignment (`Collect` sits at the LB row, `Bonus` at
  the RT row) rather than assumed.
- **racingj**, **racingj2** — `ButtonLeftShoulder`/`ButtonB` both ="Shift Down" (and
  `ButtonRightShoulder`/`ButtonA` both ="Shift Up"): kept as a redundant physical mapping rather
  than a misread, same shape as `gticlub`'s confirmed-correct Shift Down/Up pairing. Both cards
  (base game and its "Chapter II" sequel) share the identical control layout, checked
  individually rather than assumed identical.
- **radikalb** — `AxisTriggerLeft`/`ButtonA` both ="Brake" (and `AxisTriggerRight`/`ButtonX` both
  ="Accelerate"): redundant trigger/face-button mapping, kept as drawn — same shape as
  `neodrift`'s/`moonwar`'s confirmed-correct redundant pairings.
- **rallybik** — two things kept as drawn: (1) `AxisTriggerLeft`/`ButtonA` both ="Brake" and
  `AxisTriggerRight`/`ButtonX` both ="Accelerate", same redundant trigger/face-button shape as
  `radikalb`; (2) both `AxisLeftStick` and `ButtonDpad` ="Steering" (rather than the pack's usual
  "Move"/"Move" pair) — the card leader-lines both the stick and the D-pad column to the same
  steering function, checked against the vertical-alignment rule rather than assumed.
- **revx** — `AxisTriggerLeft`/`ButtonA` both ="CD" (and `AxisTriggerRight`/`ButtonX` both
  ="Shoot"): redundant trigger/face-button mapping, kept as drawn — same shape as
  `radikalb`'s/`rallybik`'s confirmed-correct redundant pairings.
- **ridgera2**, **ridgerac** — `ButtonLeftShoulder`/`ButtonB` both ="Shift Down" (and
  `ButtonRightShoulder`/`ButtonX` both ="Shift Up"): kept as a redundant physical mapping rather
  than a misread, same shape as `racingj`'s/`racingj2`'s confirmed-correct Shift Down/Up
  pairing. Both cards (base game and its sequel) share the identical control layout, checked
  individually rather than assumed identical.
- **ridhero** — `AxisTriggerLeft`="Brake"/`ButtonA`="Brake" and `AxisTriggerRight`="Accelerate"/
  `ButtonX`="Accelerate": redundant trigger/face-button mapping, same shape as `radikalb`'s/
  `rallybik`'s confirmed-correct redundant pairings. Also checked by exact row: "Brake" and
  "Accelerate" sit at the LT/RT height, not LB/RB, so the shoulder buttons themselves are left
  unlabeled rather than assumed to carry the trigger row's text.
- **ridleofp** — `ButtonA`="B Button", `ButtonX`="A Button": kept the card's own lettering
  verbatim even though it reads as swapped from the Xbox-style A/X mapping used to draw the
  diagram — same "own lettering carried onto Y/B/A/X" shape as `qos`'s/`quizvadr`'s, checked
  rather than corrected to the "expected" A-on-X, B-on-A pairing.
- **ripribit** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, unlike its near-identical Arcade Classics siblings in this same batch (`ringdest`,
  `ringfgt`, `ringking`, `ringrage`, `riot`, `riotcity`, `ripcord`, `ripoff`, `riskchal`, all of
  which print "Move" on both). Only `ButtonA`="Shoot" is drawn. Kept as drawn rather than assumed
  to be a miss — same no-movement shape as `jumbogod`/`kattobas`/etc. from earlier batches.
- **robocop2j** — card image is identical to `robocop2`'s and prints only "robocop2" (never
  "robocop2j"); transcribed with `romName="robocop2j"` to match the pack's filename, same
  content as `robocop2` — a clone reusing its parent's card, same shape as `mutantf1`'s reuse of
  `mutantf`'s card.
- **roughrac** — `AxisTriggerRight`/`ButtonX` both ="Accelerate": redundant trigger/face-button
  mapping, kept as drawn — same shape as `radikalb`'s/`rallybik`'s/`revx`'s/`ridhero`'s
  confirmed-correct redundant pairings.
- **runaway**, **runpuppy** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad`
  carries any text (both cards show the plain, line-less stick and D-pad icons), same no-movement
  shape as `jumbogod`/`kattobas`/`konami88`/etc. from earlier batches. Confirmed by a second,
  closer read of both images rather than assumed to be a miss — `runaway` only labels
  `ButtonB`/`ButtonA`/`ButtonX` ("Change Direction"/"Jump"/"Change Track"); `runpuppy` only labels
  `ButtonA`="Throw".
- **rushhero** — `ButtonY` and `ButtonB` both left unlabeled, unlike its Konami Classics siblings
  in this same batch (`rungun`/`rungund`/`rungun2`/`rushatck`), all of which label `ButtonB`
  ("Change Player" or "Shoot") even though `ButtonY` stays blank across the whole family. Checked
  against the vertical-alignment rule rather than assumed to be a missed `ButtonB` label —
  `rushhero` genuinely has no text between `Start` and `ButtonA`="Change Player / Select".
- **ryujin** — the card's own subtitle text under the title reads "ryugin", not "ryujin", but
  the pack's image file is `ryujin.jpg`; transcribed with `romName="ryujin"` to match the pack's
  filename rather than the on-card text, same shape as `robocop2j`'s filename-over-card-content
  precedent.
- **salarymc** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text or leader line (unlike every other Konami Classics card in this batch), same no-movement
  shape as `runaway`/`runpuppy`/`jumbogod`/etc. from earlier batches. Confirmed by a second,
  closer read of the image rather than assumed to be a miss — only `ButtonB`="Blue",
  `ButtonA`="Green", `ButtonX`="Red" are labelled.
- **sbishi**, **sbishika** — the pack supplies two separate image files (`sbishi.jpg`,
  `sbishika.jpg`) for "Super Bishi Bashi Champ", but both render pixel-identical artwork with
  both rom names underlined together beneath the one title. Transcribed as two `<Game>` entries
  with identical labels (one per file), rather than merging into a single entry — matching how
  the pack itself splits the pair into two files, not how the card groups the names visually.
  Neither `AxisLeftStick` nor `ButtonDpad` carries any text on this card (only
  `ButtonB`="Blue"/`ButtonA`="Green"/`ButtonX`="Red" plus Start/Insert Coin) — same no-movement
  shape as `salarymc`/`runaway`/etc.
- **sbowling** — only `AxisLeftStick`="Move / Bowl Ball" is labelled; the D-pad column below it
  carries leader lines but no text at all, unlike the pack's usual matched "Move"/"Move" pair on
  both the stick and the D-pad. Kept as drawn rather than assumed to be a missed second "Move" —
  same no-movement-on-one-control shape as `ripribit`'s asymmetric case.
- **scross**, **scrossa** — the pack supplies two separate image files for "Stadium Cross" but
  both render pixel-identical artwork with both rom names underlined together beneath the one
  title. Transcribed as two `<Game>` entries with identical labels (one per file), same shape as
  `sbishi`/`sbishika`'s precedent.
- **sdi**, **sdib** — same shape again: two separate image files for "SDI: Strategic Defence
  Initiative", pixel-identical artwork, both rom names underlined together. Two `<Game>` entries
  with identical labels, one per file.
- **sdi**, **sdib** — `ButtonRightShoulder`="Shoot", but `AxisTriggerRight` (RT, same column,
  row below) is left unlabeled. Checked against the vertical-alignment rule rather than assumed
  to be a misread trigger — "Shoot" sits exactly at RB's height, not RT's.
- **scrabble** — both `AxisLeftStick`="Move Crosshair" and `ButtonDpad`="Move" carry leader
  lines and text, unlike the pack's usual single "Move" claim per card. Kept as drawn — both
  controls genuinely move the crosshair on this card, rather than treating one as redundant.
- **sdungeon** — `AxisRightStick`="Shoot": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `mplanets`'s "Rotate"/`propcycl`'s "Throttle".
- **searchar** — `AxisRightStick`="Rotate Shoot Position": same plain-circle-below-the-D-pad
  convention as `sdungeon`'s "Shoot"/`mplanets`'s "Rotate"/`propcycl`'s "Throttle".
- **sexyparo**, **sexyparoa** — the pack supplies two separate image files for "Sexy Parodius"
  but both render pixel-identical artwork with both rom names underlined together beneath the
  one title. Transcribed as two `<Game>` entries with identical labels (one per file), same
  shape as `sbishi`/`sbishika`'s, `scross`/`scrossa`'s, and `sdi`/`sdib`'s precedent.
- **sfiii**/**sfiiin**, **sfiii2**/**sfiii2n**, **sfiii3**/**sfiii3n** — three more pairs sharing
  this pack's duplicate-image shape: each pair's two image files render pixel-identical artwork
  with both rom names underlined together beneath the one title ("Street Fighter III: New
  Generation", "...2nd Impact: Giant Attack", "...3rd Strike: Fight for the Future"
  respectively). Transcribed as two `<Game>` entries with identical labels (one per file), same
  shape as `sbishi`/`sbishika`'s, `scross`/`scrossa`'s, `sdi`/`sdib`'s, and `sexyparo`/
  `sexyparoa`'s precedent.
- **sf2049**, **sf2049se**, **sf2049te** — `ButtonDpadUp`="View 2", `ButtonDpadLeft`="View 1",
  `ButtonDpadRight`="View 3", `ButtonDpadDown`="Music": applied the pack's confirmed
  Up/Left/Right/Down top-to-bottom icon-stacking order (established by the Pop'n Music batch's
  pixel-zoom, see `popn1`–`popn8` above) rather than a naive Up/Down/Left/Right reading. Not a
  fresh pixel-zoom on this card itself, so flagged here for a second look if anyone wants to
  verify directly against the image.
- **sfrush**, **sfrushrk** — same shape as `sf2049`/`sf2049se`/`sf2049te`: `ButtonDpadUp`="View 2",
  `ButtonDpadLeft`="View 1", `ButtonDpadRight`="View 3", `ButtonDpadDown`="Music". Applied the
  pack's confirmed Up/Left/Right/Down top-to-bottom order rather than a naive
  Up/Down/Left/Right reading; the printed text sequence itself (View 2/View 1/View 3/Music) is
  kept exactly as ordered top-to-bottom on the card, only the direction assignment follows the
  established convention.
- **shangha2**, **shangha3**, **shanghai** — title kept as printed, "Sahnghai II"/"Sahnghai
  III"/"Sahnghai" (missing the 'g' after "Sh"), rather than corrected to "Shanghai II"/"Shanghai
  III"/"Shanghai". Each card's own subtitle line (`shangha2`/`shangha3`/`shanghai`) matches the
  pack's filename exactly, so the robocop2j/ryujin filename-wins precedent doesn't apply here —
  this is a typo in the title text itself, kept verbatim like `gunfight`'s/`kroozr`'s/
  `paperboy`'s/`primrag2`'s misspelled labels.
- **shinobi**, **shinobi6** — the pack supplies two separate image files for "Shinobi" but both
  render pixel-identical artwork with both rom names underlined together beneath the one title.
  Transcribed as two `<Game>` entries with identical labels (one per file), same shape as
  `sbishi`/`sbishika`'s, `scross`/`scrossa`'s, `sdi`/`sdib`'s, `sexyparo`/`sexyparoa`'s, and the
  Street Fighter III pairs' precedent.
- **simpsons**, **simpsons2p**, **simpsons2pj** — the pack supplies three separate image files for
  "The Simpsons" but all three render pixel-identical artwork with "simpsons" and "simpsons2p"
  underlined together beneath the one title (the third file, `simpsons2pj.jpg`, shows the same
  pair of names again despite being its own distinct rom). Transcribed as three `<Game>` entries
  with identical labels, one per file, same shape as `sbishi`/`sbishika`'s and the other
  duplicate-image precedents above.
- **silkworm** — `AxisTriggerRight`, `ButtonY`, and `ButtonB` all ="Tilt / Shoot Reverse": three
  separate controls redundantly mapped to the same action, kept as drawn rather than treated as a
  misread — same three-way shape as `maxforce`'s confirmed-correct triple "Shoot" redundancy.
- **skychal**, **skyraid** — on this pack's Xbox-controller template, every other card carries a
  second "Move" caption bracketing the D-pad's four direction icons alongside the stick's own
  "Move". These two cards' D-pad icons carry no such bracket or text at all, unlike every sibling
  card in the same batch — `ButtonDpad` omitted rather than assumed to be "Move" by analogy with
  the template's usual pattern. Both cards also leave `ButtonX` blank (only `ButtonA` labelled).
- **slapshtr** — `ButtonA`="B Button", `ButtonX`="A Button": the card prints the arcade's own
  button names on the Xbox face buttons, and they don't match the Xbox letter each line points
  to — the button drawn as `A` is captioned "B Button", the one drawn as `X` is captioned
  "A Button". Read by exact line position, not corrected to make the captions agree with the
  face-button letters they land on.
- **smgolf** — `ButtonA`="A", `ButtonX`="B": same shape as `slapshtr`'s "B Button"/"A Button" —
  the card's own button naming lands on the opposite Xbox face button from what the single-letter
  caption would suggest. Kept as drawn.
- **snapper** — `ButtonA`="Right", `ButtonX`="Left": direction words captioning face buttons
  rather than the D-pad or stick, verified by line position rather than assumed to be misplaced
  D-pad text. `ButtonB`="Shoot" on the same card is a normal action label, so the Right/Left
  pairing on A/X is the card's own control scheme, not a transcription slip.
- **solvalou** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: the D-pad column carries
  leader lines but no bracketed "Move" text, unlike every other card in this same batch
  (`snowbros`, `socbrawl`, `soccer`, `soccerss`/`soccerssa`, `sokonuke`, `sokyugrt`, `solarfox`,
  `solarq`, `soldam`, `soldivid`, `solfigtr`, `solomon`, `solrwarr`, `songjang`, `sonic`,
  `sonicbom`, `sonicfgt`, `sonicwi`, `sonicwi2`, `sonicwi3`, `sonson`, all of which pair "Move" on
  both stick and D-pad), same single-stick-only shape as `hidctch3`/`jdredd`/`jdreddp`/`jetwave`/
  `jongbou`/`jongbou2`/`maletmad`/`pbillian`/`piratesh`/`pwrflip` from earlier batches.
- **soccerss**, **soccerssa** — `ButtonX`="Shoot / Tackle / Long Pass": this label runs to the
  card's own right edge with no visible clipping mark or ellipsis. Transcribed exactly as visible
  rather than guessing at further text that may lie past the frame boundary; worth a second look
  at higher resolution if the source allows it.
- **sos** — `ButtonA`="Button": kept the card's literal generic caption verbatim, same pattern as
  `headonch`/`hiimpact`/`klax`/`phozon`/`mmagic`/`monymony`/`navarone`/`pblbeach`/`plgirls`/
  `popbounc`/`prosport`/`pspikes`/`puckpkmn`.
- **sotsugyo** — `ButtonB`="B", `ButtonA`="A": kept these literal single-letter captions verbatim,
  same pattern as `jajamaru`/`klax`/`nmg5`/`nouryoku`.
- **spacduel** — the card's own subtitle text under the title reads "spaceduel", not "spacduel",
  but the pack's image file is `spacduel.jpg`; transcribed with `romName="spacduel"` to match the
  pack's filename, same shape as `robocop2j`'s/`ryujin`'s filename-over-card-content precedent.
  Also `AxisTriggerRight`/`ButtonB` both ="Shield": redundant trigger/face-button mapping, kept as
  drawn — same shape as `gticlub`'s/`mag_time`'s confirmed-correct redundant pairings.
- **spacegun** — `AxisLeftStick`="Move Crosshair", `ButtonDpad` left unlabeled: same
  single-stick-only shape already established for `jdredd`/`ppsatan`'s "Move Crosshair" cards —
  this is a light-gun game, the D-pad column carries no bracketed text at all.
- **spaceskr** — `ButtonA`/`ButtonX` both ="Shoot": redundant mapping across two face buttons,
  kept as drawn — same shape as `gticlub`'s/`megablst`'s/`puyopuy2`'s confirmed-correct redundant
  pairings.
- **spacfury** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `solvalou`'s/`pwrflip`'s/`piratesh`'s. Also `AxisTriggerRight`/`ButtonX` both
  ="Accelerate": redundant trigger/face-button mapping, kept as drawn — same shape as
  `radikalb`'s/`roughrac`'s confirmed-correct redundant pairings.
- **spacwalk** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled, and no face-button label at
  all (`ButtonY`/`ButtonB`/`ButtonA`/`ButtonX` all blank) — only `ButtonStart`/`ButtonBack` join
  the stick label. Kept as drawn rather than assumed to be a miss.
- **sparkz** — `ButtonA`="Rotate", `ButtonX`="Rotate": both face buttons carry the identical
  literal caption with no direction distinguishing them, same shape as `pnyaa`'s. Checked and
  kept as printed.
- **spbactn**, **spbactnp** — the pack supplies two separate image files for "Super Pinball
  Action" but both render pixel-identical artwork with both rom names underlined together
  beneath the one title. Transcribed as two `<Game>` entries with identical labels (one per
  file), same shape as `sbishi`/`sbishika`'s and the other duplicate-image precedents. Neither
  card labels `AxisLeftStick`/`ButtonDpad` at all — only `ButtonLeftShoulder`="Left Flipper",
  `ButtonRightShoulder`="Right Flipper", and `ButtonA`="Launch" — same no-movement shape as
  `jumbogod`/`kattobas`/etc.
- **spclords** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `solvalou`'s/`spacfury`'s/etc. Also `AxisTriggerLeft`="Reverse" paired with
  `ButtonRightShoulder`="Rear View"/`AxisTriggerRight`="Accelerate" — an asymmetric
  shoulder/trigger layout (only one of the four LB/LT/RB/RT slots on the left side is labeled),
  checked against the vertical-alignment rule rather than assumed to be a missed LB label.
- **spcpokan** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. Only `ButtonA`="Hit" is
  drawn.
- **spcpostn** — `AxisTriggerRight`/`ButtonX` both ="Accelerate": redundant trigger/face-button
  mapping, kept as drawn — same shape as `radikalb`'s/`roughrac`'s/`spacfury`'s confirmed-correct
  redundant pairings.
- **spdball** — `AxisRightStick`="Outfielder": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `sdungeon`'s "Shoot"/`searchar`'s "Rotate Shoot Position". `ButtonDpad` left unlabeled while
  `AxisLeftStick`="Move" — single-stick-only shape, same as `spacfury`'s.
- **spdheat** — `AxisLeftStick`="Steering", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `spclords`'s/`spdball`'s, here with "Steering" wording in place of the pack's usual
  "Move" — same unusual-wording shape as `pkgnsh`'s/`pwrflip`'s.
- **speedbal** — `ButtonLeftShoulder`/`ButtonX` both ="Left Flipper" (and `ButtonRightShoulder`/
  `ButtonA` both ="Right Flipper"): kept as a redundant physical mapping rather than a misread,
  same shape as `mag_time`'s/`musicbal`'s/`panicr`'s confirmed-correct pinball flipper pairings.
- **speedfrk** — `ButtonY`="1st", `ButtonB`="4th", `ButtonA`="2nd", `ButtonX`="3rd": a
  non-sequential place-finish numbering (1, 4, 2, 3) across the face buttons, kept exactly as
  printed rather than "corrected" to Y/A/X/B running order — same non-sequential-but-verbatim
  shape as `magspeed`'s D-pad card numbering. `AxisLeftStick`="Steering", `ButtonDpad` left
  unlabeled: single-stick-only shape, same as `spdheat`'s.
- **speedrcr**, **speedrs**, **speedup** — on this pack's generic Xbox-controller template,
  `AxisTriggerLeft`="Brake" and `AxisTriggerRight`="Accelerate" (LT/RT row), while
  `ButtonLeftShoulder`/`ButtonRightShoulder` (LB/RB) are left unlabeled — pixel-zoomed the
  LB/LT and RB/RT icon pairs directly (cropped and enlarged via PIL) to confirm the text sits
  against the trigger row, not the shoulder row: LB/RB's dotted leader lines run off toward the
  decorative controller-silhouette outline rather than to any text. Same pattern checked and
  kept, not assumed, per the documented LB/LT-vs-RB/RT misread risk.
- **splat** — `AxisRightStick`="Shoot": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `sdungeon`'s/`searchar`'s/`spdball`'s. The rest of the card (stick, D-pad, all four face
  buttons) is genuinely unlabeled — only Start, this one control, and Insert Coin are drawn.
- **splndrbt**, **splndrbt2** — `AxisTriggerRight`="Accelerate" (RT row, pixel-zoomed against
  RB same as `speedrcr`'s group above) with no Brake label anywhere on the card (LB and LT both
  unlabeled) — same shape as `hangonjr`/`indyheat`/`lagunar`/`montecar`/`maxrpm`/`nitedrvr`'s
  "Accelerate only" racing cards. Also `ButtonX`="Accelerate" redundantly alongside the trigger,
  kept as drawn — same shape as `gticlub`'s confirmed-correct redundant pairings. Both cards
  (base game and its sequel) share the identical layout, checked individually.
- **spnchout** — `AxisTriggerLeft`="Left Punch", `AxisTriggerRight`="Right Punch": pixel-zoomed
  the LB/LT and RB/RT pairs (same check as `speedrcr`'s group above) to confirm the punch labels
  sit against the trigger row, with LB/RB unlabeled. Also `ButtonX`="Left Punch" and
  `ButtonA`="Right Punch" redundantly alongside the triggers, kept as drawn — same shape as
  `gticlub`'s confirmed-correct redundant pairings.
- **squaitsa** — `AxisRightStick`="Spin Racket Arm": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `sdungeon`'s/`searchar`'s/`spdball`'s/`splat`'s. Its sibling card `squash` (different
  publisher, same title "Squash") carries no label on this control at all — checked individually
  rather than assumed uniform across the two.
- **spyhunt** — `ButtonLeftShoulder`="Smoke" with `AxisTriggerLeft` left unlabeled, checked
  against the LB/LT vertical-alignment rule rather than assumed — "Smoke" sits at LB's row
  height, not LT's. `ButtonRightShoulder`="Shift Up" and `AxisTriggerRight`="Weapons Van"
  confirmed by the same row check on the opposite side.
- **sspeedr**, **sstingry** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: on this pack's
  generic Xbox-controller template, every sibling card transcribed in the same batch
  (`ssideki2`/`ssideki3`/`ssideki4`, `ssingles`, `sslam`, `ssmissin`, `ssoldier`, `ssonicbr`,
  `ssozumo`, `sspaceat`, `sspirits`, `sstriker`, `stactics`, `stadhero`, `stadhr96`, `stagger1`,
  `stakwin`, `stakwin2`) brackets a second "Move" caption across the D-pad's four stacked
  direction icons alongside the stick's own "Move". These two cards' D-pad icons carry no such
  bracket or text at all — `ButtonDpad` omitted rather than assumed to be "Move" by analogy with
  the template's usual pattern, same shape as `skychal`'s/`skyraid`'s established D-pad-omission
  precedent.
- **ssprint**, **ssrj** — `AxisLeftStick`="Steering", `AxisTriggerRight`="Accelerate", no Brake
  label anywhere on the card: same "Accelerate only" racing-card shape as `hangonjr`/`indyheat`/
  `lagunar`/`montecar`/`maxrpm`/`nitedrvr`. Also `ButtonDpad` left unlabeled (no "Steering"/"Move"
  bracket on the D-pad column either), and all four face buttons (`ButtonY`/`ButtonB`/`ButtonA`/
  `ButtonX`) blank — checked directly rather than assumed absent by analogy with either the
  Accelerate-only precedent or this batch's usual "Move"/"Move" pairing. `AxisTriggerRight`'s
  "Accelerate" text was confirmed by exact vertical alignment against the RT row, not the RB row
  above it, per the documented LB/LT-vs-RB/RT misread risk.
- **starblad**, **starwars** — `AxisLeftStick`="Move Crosshair", `ButtonDpad` left unlabeled:
  same single-stick-only shape as `jdredd`'s/`ppsatan`'s/`spacegun`'s established "Move
  Crosshair" cards.
- **starfir2**, **starfire** — `AxisRightStick`="Throttle": the plain circle read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `propcycl`'s/`mplanets`'s/`sdungeon`'s. `ButtonDpad` left unlabeled on both (only
  `AxisLeftStick`="Move") — single-stick-only shape, same as `spacfury`'s/`spdball`'s.
- **stargrds** — `AxisRightStick`="Shoot": same established plain-circle convention as
  `starfir2`'s/`starfire`'s "Throttle". `ButtonDpad` left unlabeled.
- **starlstr** — `ButtonA`="A", `ButtonX`="B": kept these literal single-letter captions
  verbatim, same pattern as `jajamaru`/`klax`/`nmg5`/`nouryoku`/`sotsugyo`.
- **startrek** — title kept as printed, "Start Trek" (extra 't' after "Star"), rather than
  corrected to the well-known "Star Trek". Pixel-zoomed the title text directly to confirm —
  the card's own subtitle line reads "startrek", matching the pack's filename, so this isn't
  the robocop2j/ryujin/spacduel filename-vs-card-text situation, just the title itself as
  printed. Same verbatim-typo handling as `shangha2`/`shangha3`/`shanghai`'s "Sahnghai".
- **startrgn** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. Also `ButtonA`="Start /
  Decide": kept as printed even though the card also carries its own separate, dedicated Start
  icon — a second, redundant "Start" wording on a face button rather than a misread of some
  other action name.
- **steeltal** — `AxisRightStick`="Altitude / Rotate": same established plain-circle convention
  as `starfir2`'s/`stargrds`'s. `ButtonDpad` left unlabeled (only `AxisLeftStick`="Move") —
  single-stick-only shape, same as `starfir2`'s/`starfire`'s.
- **stocker**, **stompin**, **strgchmp**, **stratab** — the D-pad column carries leader lines
  but no bracketed text on any of these four, while the analog stick alongside it does
  ("Steering"/"Stomp"/"Steering"/"Move / Bowl"): same single-stick-only shape as `spdheat`'s/
  `speedfrk`'s/`sspeedr`'s/`sstingry`'s, extending the precedent list rather than assuming the
  stick's own wording also applies to the D-pad.
- **stratgyx** — pixel-zoomed the LB/LT and RB/RT pairs to confirm row alignment:
  `ButtonLeftShoulder`="Rotate Left" (LB row, LT left blank) paired with
  `AxisTriggerRight`="Rotate Right" (RT row) — an asymmetric shoulder/trigger pairing, same shape
  as `imolagp`'s/`joyfulr`'s/`konamigt`'s. Also `ButtonRightShoulder`/`ButtonX` both ="Shoot":
  redundant mapping across a shoulder and a face button, kept as drawn — same shape as
  `gticlub`'s confirmed-correct redundant pairings.
- **strkfgtr** — pixel-zoomed the LB/LT and RB/RT pairs to confirm `AxisTriggerLeft`="Shoot" and
  `AxisTriggerRight`="Missiles" sit on the trigger row, with LB/RB left blank — same
  vertical-alignment check as `speedrcr`'s/`spnchout`'s group. `ButtonX`="Shoot" and
  `ButtonA`="Missiles" redundantly duplicate the trigger labels, kept as drawn — same shape as
  `gticlub`'s confirmed-correct redundant pairings. `ButtonDpad` left unlabeled despite
  `AxisLeftStick`="Move" — single-stick-only shape, same as `stocker`'s group above.
- **stunrun** — `AxisLeftStick`="Steering", `ButtonDpad` left unlabeled: single-stick-only shape,
  same as `stocker`'s/`stompin`'s/`strgchmp`'s/`stratab`'s/`strkfgtr`'s established precedent.
  Pixel-zoomed to confirm the D-pad column genuinely carries no bracketed text at all, unlike its
  sibling `strtheat` (transcribed in the same batch), which pairs "Steering" on both the stick and
  the D-pad.
- **superchs** — `AxisTriggerLeft`="Brake", `AxisTriggerRight`="Accelerate",
  `AxisLeftStick`="Steering", `ButtonDpad` left unlabeled: single-stick-only shape, same as
  `stunrun`'s above. Pixel-zoomed to confirm the D-pad column is genuinely blank rather than
  assumed to mirror the stick's "Steering", as most of this batch's siblings do.
- **superkds** — `AxisLeftStick`="Shoot" (not "Move"/"Steering"), `ButtonDpad` left unlabeled: an
  unusual wording for the stick, kept exactly as printed — same "unusual wording" shape as
  `pkgnsh`'s "Power"/`pwrflip`'s "Tilt"/`spdheat`'s "Steering" — combined with the
  single-stick-only D-pad omission already established for `stunrun`/`superchs` above.
- **superbug** — `AxisLeftStick`="Steering", `ButtonDpad`="Select Track": kept these as two
  genuinely different labels rather than assuming the D-pad repeats the stick's "Steering" the way
  most of this batch's siblings do (e.g. `strtheat`, `stuntair`, `sub`). Verified by re-reading the
  D-pad's own bracketed text directly rather than copying the stick's label across.
- **suikoenb** — `ButtonLeftShoulder`="Heavy Slash", `ButtonRightShoulder`="Heavy Kick":
  pixel-zoomed the LB/LT and RB/RT pairs (same check as `speedrcr`'s/`spnchout`'s group) to
  confirm the attack labels sit on the shoulder row, not the trigger row, with LT/RT left blank.
- **superwng** — `ButtonLeftShoulder`="Left Flipper", `ButtonRightShoulder`="Right Flipper"
  (redundantly paired with `ButtonA`="Right Flipper"/`ButtonX`="Left Flipper"): pixel-zoomed the
  LB/LT and RB/RT pairs to confirm the flipper labels sit on the shoulder row, not the trigger
  row — same vertical-alignment check as `suikoenb`'s above, and the same redundant-pairing shape
  as `mag_time`'s/`musicbal`'s/`panicr`'s/`speedbal`'s confirmed-correct pinball flipper pairings.
- **suratk** — title kept as printed, "Suprise Attack" (missing the 'r' after "Sup"), rather than
  corrected to "Surprise Attack". Same verbatim-typo handling as `shangha2`/`shangha3`/
  `shanghai`'s "Sahnghai" and `startrek`'s "Start Trek".
- **svolley**, **svolleyu** — the pack supplies two separate image files for "Super Volleyball"
  but both render pixel-identical artwork with both rom names underlined together beneath the
  one title. Transcribed as two `<Game>` entries with identical labels (one per file), same
  shape as `sbishi`/`sbishika`'s, `scross`/`scrossa`'s, `sdi`/`sdib`'s, and the other
  duplicate-image precedents above.
- **swa** — `AxisRightStick`="Throttle": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `propcycl`'s/`mplanets`'s/`sdungeon`'s/`searchar`'s. Also `AxisTriggerLeft`="Proton Torpedo"/
  `AxisTriggerRight`="Laser" (LB/RB left unlabeled) verified by exact LB/LT and RB/RT vertical
  alignment rather than assumed — both duplicate the face buttons' `ButtonA`="Proton Torpedo"/
  `ButtonX`="Laser", a redundant physical mapping same shape as `gticlub`'s confirmed-correct
  pairing.
- **sxevious** — the card's own subtitle text under the title reads "sxeviousj", not "sxevious",
  but the pack's image file is `sxevious.jpg`; transcribed with `romName="sxevious"` to match the
  pack's filename rather than the on-card text, same shape as `robocop2j`'s/`ryujin`'s/
  `spacduel`'s filename-over-card-content precedent.
- **syvalion**, **syvalionu** — the pack supplies two separate image files for "Syvalion" but
  both render pixel-identical artwork with both rom names underlined together beneath the one
  title. Transcribed as two `<Game>` entries with identical labels (one per file), same shape as
  `sbishi`/`sbishika`'s, `scross`/`scrossa`'s, `sdi`/`sdib`'s, and `svolley`/`svolleyu`'s
  precedent.
- **sws**, **sws92**, **sws93**, **sws95**, **sws96**, **sws97**, **sws98**, **sws99** — nine
  near-identical yearly Super World Stadium cards, each checked individually rather than assumed
  uniform across the family: `sws`/`sws92`/`sws93` label the stick `AxisLeftStick`="Move / Mod
  Pitch" and `ButtonX`="Run / Steal / Pick Off"; `sws95`/`sws96`/`sws97` drop "Mod Pitch" (stick
  is plain "Move") and swap the X label's word order to "Steal / Run / Pick Off"; `sws98`/`sws99`
  move the "Timeout" label off `ButtonB` (left blank) onto `ButtonY` instead, and shorten
  `ButtonX` to "Run / Pick Off" (no "Steal" at all). None of the nine label `ButtonDpad` — the
  D-pad column carries leader lines but no bracketed "Move" text on any of them, unlike most of
  this pack's other Namco Classics cards.
- **tankfrce**, **tankfrce4** — the pack supplies two separate image files for "Tank Force" but
  both render pixel-identical artwork with both rom names underlined together beneath the one
  title. Transcribed as two `<Game>` entries with identical labels (one per file), same shape as
  `sbishi`/`sbishika`'s, `scross`/`scrossa`'s, `sdi`/`sdib`'s, `svolley`/`svolleyu`'s, and
  `syvalion`/`syvalionu`'s precedent.
- **targeth** — `AxisLeftStick`="Move Crosshair", `ButtonDpad` left unlabeled: same
  single-stick-only shape as `jdredd`/`jdreddp`/`jetwave`/`jongbou`/`jongbou2`, here on a light-gun
  shooter whose D-pad icon is drawn but carries no bracketed text.
- **techbowl** — `ButtonA`="Button": kept the card's literal generic caption verbatim, same
  pattern as `headonch`/`hiimpact`/`klax`/`mmagic`/`monymony`/`navarone`/`pblbeach`/`plgirls`/
  `popbounc`/`prosport`/`pspikes`/`puckpkmn`/`sos`.
- **term2** — `AxisTriggerLeft`/`ButtonA` both ="Bomb" (and `AxisTriggerRight`/`ButtonX` both
  ="Shoot"): redundant trigger/face-button mapping, kept as drawn — same shape as
  `gticlub`'s/`mag_time`'s/`radikalb`'s/`rallybik`'s/`revx`'s/`ridhero`'s/`roughrac`'s/
  `spacduel`'s/`swa`'s confirmed-correct redundant pairings.
- **thegrid** — `AxisLeftStick`="Move", `ButtonDpad` left unlabeled: pixel-zoomed the D-pad
  icon column directly (cropped and enlarged) and confirmed its four leader lines dead-end with
  no bracketed text at all, unlike every other Midway/Arcade/Sega/Konami/Jaleco Classics card in
  this same batch, which all pair "Move" on both the stick and the D-pad — same single-stick-only
  shape as `skychal`'s/`skyraid`'s/`sspeedr`'s/`sstingry`'s established D-pad-omission precedent.
  Also pixel-zoomed the LB/LT and RB/RT pairs to confirm `AxisTriggerLeft`="Super Weapon" and
  `AxisTriggerRight`="Shoot" sit on the trigger row, with LB/RB left blank — same
  vertical-alignment check as `speedrcr`'s/`spnchout`'s group.
- **titlef** — `AxisTriggerLeft`/`AxisTriggerRight` both ="Dodge Left": pixel-zoomed both
  corners to confirm the text sits at the *trigger* row, not the shoulder row, and that both
  sides genuinely print the identical "Dodge Left" rather than a mirrored Left/Right pair — same
  shape as `hwchamp`'s (a related boxing card) confirmed LB/RB "Dodge Left" duplicate, just one
  row down. Recorded as a `<Note>` on the game in addition to the `<Label>`s, matching how
  `hwchamp` documents its own instance.
- **timetunl** — no `AxisLeftStick`/`ButtonDpad` label at all: unlike the pack's usual
  no-movement shape (`jumbogod`/`kattobas`/etc.), this is a driving/rail game whose other
  controls (`AxisTriggerLeft`="Brake / Reverse", `AxisTriggerRight`="Accelerate",
  `ButtonA`="(Hold) Change Track") are all fully labeled — the card simply has no steering
  control printed, plausible for a fixed-rail racer. Confirmed by a pixel-zoomed crop of the
  stick/D-pad column showing leader lines that dead-end with no text, same check as `thegrid`'s.
- **thrilld** — `ButtonRightShoulder`/`ButtonB` both ="Shift Up" and `ButtonLeftShoulder`/
  `ButtonX` both ="Shift Down": kept as a redundant physical mapping rather than a misread, same
  shape as `gticlub`'s confirmed-correct Shift Down/Up pairing.
- **timesold** — the plain circle below the D-pad reads `AxisRightStick`="Rotate Shoot
  Position", identical text to `searchar`'s card — read as `AxisRightStick` per the established
  convention (never `ButtonRightStick`).
- **tinstar** — same plain-circle-below-the-D-pad convention: `AxisRightStick`="Aim", not
  `ButtonRightStick`, same as `timesold`'s/`searchar`'s/`sdungeon`'s/`mplanets`'s/`propcycl`'s.
- **titlefu** — `AxisTriggerLeft`/`AxisTriggerRight` both ="Dodge Left": pixel-zoomed both
  corners to confirm the text sits at the *trigger* row, not the shoulder row (the original OCR
  pass misread it as LB/RB), and that both sides genuinely print the identical "Dodge Left"
  rather than a mirrored Left/Right pair — same shape as `titlef`'s (this card's own clone) and
  `hwchamp`'s confirmed duplicate. Recorded as a `<Note>` on the game to match `titlef`'s.
- **tjumpman** — `ButtonLeftShoulder`="Payout", `ButtonRightShoulder`="Bet 3": pixel-zoomed to
  confirm these sit at the *shoulder* row, not the trigger row — the opposite alignment from
  `titlefu`'s/`tmek`'s in this same batch, checked individually rather than assumed uniform
  across the template.
- **tkoboxng** — `ButtonA`="A", `ButtonX`="B": kept these literal single-letter captions
  verbatim (the card names its own buttons after the original NES Vs. System pad), same pattern
  as `jajamaru`/`klax`/`nmg5`/`nouryoku`/`sotsugyo`/`starlstr`.
- **tmek** — `AxisTriggerLeft`="Secondary Fire", `AxisTriggerRight`="Shoot": pixel-zoomed both
  corners to confirm the text sits at the trigger row, not the shoulder row (LB/RB are
  unlabeled) — the original OCR pass misread both as LB/RB.
- **tnk3**, **tnk3b** — two separate image files for "T.N.K III", but unlike this pack's usual
  duplicate-image shape (`sbishi`/`sbishika`, `scross`/`scrossa`, etc., where both files render
  identical artwork), these two cards genuinely disagree: `tnk3.jpg` (romName `tnk3`, simple
  Canon/Machine Gun only) also underlines "tnk3b (No Rotary)" as a second applicable rom name,
  while `tnk3b.jpg` (romName `tnk3b`) shows a full rotate scheme (Canon/Rotate Left on LB/LT,
  Machine Gun/Rotate Right on RB/RT, Rotate Turret on the right stick) and underlines "tnk3"
  and "tnk3b" together with no qualifier. Filename used as `romName` for each (matching the
  `robocop2j`/`ryujin`/`spacduel`/`sxevious` precedent), and the contradiction recorded as a
  `<Note>` on both games rather than resolved — same shape as `gwar`/`gwarb`'s and
  `ikari`/`ikari3`/`ikari3w`/`ikarijpb`'s cross-image contradictions.
- **todruaga** — the card's own subtitle text under the title reads "druaga", not "todruaga",
  but the pack's image file is `todruaga.jpg`; transcribed with `romName="todruaga"` to match
  the pack's filename rather than the on-card text, same shape as `robocop2j`'s/`ryujin`'s/
  `spacduel`'s/`sxevious`'s filename-over-card-content precedent.
- **tornbase** — `ButtonB`="Pitch Right", `ButtonA`="Pitch Right": both face buttons print the
  identical text, despite `ButtonLeftShoulder`="Outfield Left"/`ButtonRightShoulder`="Outfield
  Right" showing this card is otherwise happy to pair Left/Right captions. Pixel-zoomed to rule
  out a misread of "Pitch Left" on `ButtonB`; the card genuinely prints "Right" on both. Kept as
  drawn rather than corrected to the expected Left/Right pair — same shape as `hwchamp`'s/
  `titlef`'s/`titlefu`'s confirmed literal-duplicate button text.
- **trally** — `AxisTriggerLeft`/`ButtonA` both ="Brake" and `AxisTriggerRight`/`ButtonX` both
  ="Accelerate": redundant trigger/face-button mapping, kept as drawn — same shape as
  `radikalb`'s/`rallybik`'s/`ridhero`'s confirmed-correct redundant pairings. Also checked by
  exact row: "Brake"/"Accelerate" sit at the LT/RT height, not LB/RB, matching `toutrun`'s and
  `travrusa`'s sibling racing cards in this same batch.
- **transfrm** — `ButtonRightShoulder`="Start 2 Players": an unusual control (a shoulder button
  starting a second player, rather than the usual `ButtonStart`) not seen elsewhere in the pack
  so far. Checked against the row-alignment rule and kept exactly as drawn.
- **trebltop** — `ButtonY`="Pass", `ButtonB`="C", `ButtonA`="B", `ButtonX`="A": a quiz game's own
  answer lettering (A/B/C plus Pass) carried onto the Y/B/A/X slots, not a misread — same shape
  as `qos`'s/`quizvadr`'s pattern. Also `ButtonLeftShoulder`="Collect", `ButtonRightShoulder`=
  "Continue", `AxisTriggerRight`="Bonus" read by exact row alignment (`Collect` at the LB row,
  `Bonus` at the RT row), matching `qos`'s/`quizvadr`'s identical layout exactly.
- **tron** — `AxisRightStick`="Move Shoot Postiton": kept the card's own apparent typo
  ("Postiton") verbatim, same misspelling already seen on `gunfight`'s/`kroozr`'s cards; read as
  the plain circle below the D-pad per the established `AxisRightStick`-not-`ButtonRightStick`
  convention.
- **truckk** — `ButtonDpad`="Change Music" rather than the pack's usual "Move"/"Move" pair (the
  stick alone carries "Steering"): kept as drawn, a racing card where the D-pad is repurposed
  rather than left unlabeled. Also `ButtonLeftShoulder`/`ButtonA` both ="Shift Down" and
  `ButtonRightShoulder`/`ButtonX` both ="Shift Up": a redundant physical mapping, same shape as
  `racingj`'s/`racingj2`'s/`ridgera2`'s/`ridgerac`'s confirmed-correct Shift Down/Up pairing.
- **trvmstr** — `ButtonY`="D", `ButtonB`="C", `ButtonA`="B", `ButtonX`="A": a fighting-game-style
  four-button naming (A/B/C/D) carried onto this project's Y/B/A/X slots, not a misread — same
  exact mapping as `pgm3in1`'s confirmed-correct pattern.
- **tsclass** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. Also `ButtonA`="Button":
  kept the card's literal generic caption verbatim, same pattern as `headonch`/`hiimpact`/`klax`/
  `phozon`/`mmagic`/`monymony`/`navarone`/`pblbeach`/`plgirls`/`popbounc`/`prosport`/`pspikes`/
  `puckpkmn`/`sos`.
- **tsukande** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc. Only `ButtonA`="Grab" is
  drawn.
- **tsupenta** — no movement label at all: neither `AxisLeftStick` nor `ButtonDpad` carries any
  text, same no-movement shape as `jumbogod`/`kattobas`/`konami88`/etc./`tsukande`. Only
  `ButtonA`="Cast" is drawn.
- **twinadv** — `ButtonA`="B", `ButtonX`="A": each control's caption literally names the *other*
  physical button, same swapped-letter shape as `mchampdx`'s/`moremore`'s/`ncv1`'s/`ncv2`'s/
  `nmg5`'s/`nouryoku`'s/`pclubys`'s literal-caption pairs. Kept exactly as printed rather than
  corrected to the expected A-on-X, B-on-A pairing.
- **twinkle** — `ButtonA`="B", `ButtonX`="A": same swapped-letter shape as `twinadv`'s/`nmg5`'s/
  `nouryoku`'s/`sotsugyo`'s literal-caption pairs. Kept exactly as printed.
- **umk3** — `ButtonY`="Low Kick" and `ButtonA`="Low Kick": kept the redundant pairing exactly as
  printed rather than "fixing" one to the expected High Punch, same shape as `mk`'s/`mk2`'s/
  `mk3`'s confirmed-correct Y/A "Low Kick" redundancy noted above — this is a recurring template
  quirk across the Mortal Kombat family, not a misread.
- **vanguard** — `AxisRightStick`="Shoot Direction": the plain circle below the D-pad, read as
  `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `propcycl`'s/`sdungeon`'s/`searchar`'s. Paired with per-direction face-button labels
  (`ButtonY`="Shoot Up", `ButtonB`="Shoot Right", `ButtonA`="Shoot Down", `ButtonX`="Shoot Left")
  rather than the pack's usual single game action — kept as drawn.
- **vaportrx** — `ButtonDpadLeft`="Left View", `ButtonDpadRight`="Right View" (Up and Down left
  unlabeled): applied the pack's confirmed Up/Left/Right/Down top-to-bottom D-pad icon-stacking
  order (established by the Pop'n Music batch, see `popn1`–`popn8`) rather than a naive
  Up/Down/Left/Right reading — the 2nd and 3rd icons in the stack carry "Left View"/"Right View"
  respectively, consistent with that order.
- **vendetta**, **vendetta2pw**, **vendettaz** — the pack supplies three separate image files for
  "Vendetta" but all three render pixel-identical artwork, with only "vendetta" and "vendetta2pw"
  underlined together beneath the one title (`vendettaz` isn't named on the card at all).
  Transcribed as three `<Game>` entries with identical labels, one per file, same shape as
  `sbishi`/`sbishika`'s and `simpsons`/`simpsons2p`/`simpsons2pj`'s precedent of a clone reusing a
  card that doesn't even list its own rom name.
- **vformula** — `ButtonDpadUp`="VR1 / RED", `ButtonDpadLeft`="VR3 / YELLOW",
  `ButtonDpadRight`="VR4 / GREEN", `ButtonDpadDown`="VR2 / BLUE": non-sequential VR numbering (1,
  3, 4, 2), kept exactly as printed rather than reordered — same shape as `magspeed`'s non-
  sequential card numbering. Applied the pack's confirmed Up/Left/Right/Down top-to-bottom
  D-pad icon-stacking order (see `popn1`–`popn8`) rather than a naive Up/Down/Left/Right reading.
- **victory**, **victoryc** — checked as a pair rather than assumed identical: `victory`'s D-pad
  column carries no leader line or text at all (only `AxisLeftStick`="Move" labels movement, same
  single-stick-only shape as `hidctch3`/`jdredd`/etc.), while `victoryc`'s D-pad does carry the
  usual bracketed "Move" alongside the stick's own "Move". Re-read both images a second time to
  confirm this genuine difference before transcribing, rather than copying one card's shape onto
  the other because they share a title.
- **viostorm** — title kept as printed, "Voilent Storm" (the actual game is "Violent Storm"),
  rather than corrected — same handling as `shangha2`/`shangha3`/`shanghai`'s "Sahnghai" typo kept
  verbatim in the title text itself.
- **virtpool** — `ButtonX`="Slop": an unusual-looking pool term (calling shots without naming a
  pocket), kept verbatim rather than assumed to be a misread of "Stop". `ButtonY`/`ButtonA` both
  carry "(Hold) ..." qualifiers already, so the card's punctuation was double-checked before
  accepting "Slop" as-is.
- **vr** — `ButtonDpadUp`="VR1 / RED", `ButtonDpadLeft`="VR3 / YELLOW",
  `ButtonDpadRight`="VR4 / GREEN", `ButtonDpadDown`="VR2 / BLUE": same non-sequential VR
  numbering (1, 3, 4, 2) and Up/Left/Right/Down top-to-bottom D-pad reading as `vformula`'s.
  Also: `ButtonLeftShoulder`/`ButtonB` both ="Shift Down" and `ButtonRightShoulder`/`ButtonX`
  both ="Shift Up" — a redundant physical mapping kept as drawn, same shape as `gticlub`'s
  confirmed-correct pairing.
- **vscaptfl** — `AxisRightStick`="Right Flag Up / Down": the plain circle below the D-pad, read
  as `AxisRightStick` per the established convention (never `ButtonRightStick`), same as
  `vanguard`'s/`sdungeon`'s/`searchar`'s. Also `ButtonA`="Descide": kept the card's own apparent
  typo (likely "Decide") verbatim rather than silently correcting it, same handling as
  `gunfight`'s "Postiton".
- **wantsega** — `ButtonA`="B Button", `ButtonX`="A Button": each control's caption literally
  names the *other* physical button, same swapped-letter shape as `mchampdx`/`moremore`/`ncv1`/
  `ncv2`/`nmg5`/`nouryoku`/`slapshtr`. Kept exactly as printed rather than corrected to match the
  face button it's drawn on.
- **westgun2** — `ButtonRightStick`="Aim": this is a distinct small circle at the bottom-center
  of the whole diagram, separate from the larger ringed right-analog-stick icon further up —
  not the "plain circle below the D-pad" that this pack's established convention always reads as
  `AxisRightStick` (see `mplanets`/`propcycl`/`sdungeon`/`searchar`/`vscaptfl` etc.). Read as the
  stick-click button per the generic vocabulary rather than folded into that convention, since its
  position doesn't match the documented pattern. First time this control has carried any text
  across the pack so far — worth a second look given there's no prior precedent to check against.
- **windheat** — `ButtonLeftShoulder`/`ButtonA` both ="Shift Down" and `ButtonRightShoulder`/
  `ButtonB` both ="Shift Up": a redundant physical mapping kept as drawn, same shape as `gticlub`'s
  confirmed-correct pairing, but here it's A/B carrying the duplicate rather than B/X.
- **winrun**, **winrun91**, **winrungp** — `ButtonLeftShoulder`/`ButtonB` both ="Shift Down" and
  `ButtonRightShoulder`/`ButtonX` both ="Shift Up": the same redundant pairing as `gticlub`'s
  confirmed-correct B/X shape.
- **winrungp** — title kept as printed, "Winning Run Sazuka Grand Prix" (the real-world game is
  "Winning Run: Suzuka Grand Prix"), rather than corrected — same handling as `shangha2`'s/
  `viostorm`'s printed typos kept verbatim.
- **winterht** — `ButtonA`="B Button", `ButtonX`="A Button": same swapped-letter shape as
  `wantsega`/`mchampdx`/`moremore`/`ncv1`/`ncv2`/`nmg5`/`nouryoku`/`slapshtr`. Kept exactly as
  printed rather than corrected to match the face button it's drawn on.
- **wingwar** — `ButtonDpadUp`="VR1 / RED", `ButtonDpadLeft`="VR3 / YELLOW",
  `ButtonDpadRight`="VR4 / GREEN", `ButtonDpadDown`="VR2 / BLUE": same non-sequential VR
  numbering and Up/Left/Right/Down top-to-bottom D-pad reading as `vr`'s/`vformula`'s, on this
  pack's identical Sega Model 1 flight-stick control set. Pixel-zoomed the LB/LT pair before
  transcribing and found "Machine Gun" aligns to LT (not LB) and "Missiles" aligns to RT
  (not RB) — both shoulder buttons (LB/RB) are actually unlabeled.
- **wow** — title kept as printed, "Waizard of Wor" (the real game is "Wizard of Wor"), rather
  than corrected — same handling as `shangha2`'s/`viostorm`'s/`winrungp`'s printed typos kept
  verbatim.
- **wschamp** — `ButtonDpad` left unlabeled (only `AxisLeftStick`="Move Crosshair" labels
  movement): the D-pad column carries leader lines but no bracketed text at all, same
  single-stick-only shape as `skychal`'s/`skyraid`'s/`thegrid`'s established D-pad-omission
  precedent.
- **wwallyj**, **wwallyja3p** — two separate image files for "Wally no Sagase!" that render
  pixel-identical artwork with both rom names underlined together beneath the one title.
  Transcribed as two `<Game>` entries with identical labels (one per file), same shape as
  `sbishi`/`sbishika`'s and the other duplicate-image precedents. Only `AxisLeftStick`="Move
  Cursor" and `ButtonA`="Select" carry any text at all — unlike every other card in this batch,
  `ButtonStart`, `ButtonBack` (Insert Coin), and `ButtonDpad` are all genuinely blank, checked by
  a second read of the image before accepting the near-total absence.
- **xmen**, **xmen6p** — the pack supplies two separate image files for "X-Men" but both render
  pixel-identical artwork with both rom names underlined together beneath the one title.
  Transcribed as two `<Game>` entries with identical labels (one per file), same shape as
  `sbishi`/`sbishika`'s and the other duplicate-image precedents.
- **xybots** — `AxisTriggerRight`/`ButtonA` both ="Shoot": redundant trigger/face-button mapping,
  kept as drawn — same shape as `gticlub`'s/`radikalb`'s confirmed-correct redundant pairings.
  Also the small circle at the bottom of the card, explicitly marked "R", is read as
  `ButtonRightStick`="Rotate Camera" rather than `AxisRightStick`, per the pack's established
  convention that an explicitly "R"-marked stick-click circle can carry its own text — this
  card's `ButtonDpad` carries no bracketed "Move" text at all, unlike most cards in the pack.
- **zeroteams** — `ButtonX`="Attcak": kept the card's own apparent typo (should read "Attack")
  verbatim, same handling as `gunfight`'s/`kroozr`'s/`paperboy`'s/`primrag2`'s misspellings.
  Confirmed as the card's own error rather than a misread: sibling cards `zeroteam` and
  `zerotm2k` (same "Zero Team" family, same batch) both spell it correctly.
- **zintrckb** — `ButtonA`="B Button", `ButtonX`="A Button": each control's caption literally
  names the *other* physical button, same swapped-letter shape as `mchampdx`/`moremore`/`ncv1`/
  `ncv2`/`nmg5`/`nouryoku`/`winterht`. Kept exactly as printed.
- **zwackery** — the small circle marked "R" at the bottom of the diagram carries its own text,
  "Attack Position": read as `ButtonRightStick` per the pack's established convention that an
  explicitly "R"-marked stick-click circle can carry its own label, same shape as `xybots`'s
  "Rotate Camera". This is the last image in the pack to show this control labelled.
- **zzyzzyxx** — `ButtonA`="Button": kept the card's literal generic caption verbatim rather than
  invented, same pattern as `headonch`/`hiimpact`/`klax`/`phozon`/`mmagic`/`monymony`/`navarone`/
  `pblbeach`/`plgirls`/`popbounc`/`prosport`/`pspikes`/`puckpkmn`/`sos`.
