Labels — user overrides
========================

Files here add or override button labels shown on the overlay.

File naming: {Platform}.xml
  One file per platform. Platform name must match LaunchBox's value exactly.

Example:
  User/Labels/Sega Genesis.xml

  <Labels>
      <Defaults>
          <Input name="Start">Pause</Input>
      </Defaults>

      <Game launchBoxId="1234" romName="Sonic the Hedgehog (USA)">
          <Input name="A">Jump</Input>
          <Input name="B">Spin Dash</Input>
      </Game>
  </Labels>

The <Defaults> block sets labels applied to every game on that platform. If a game
entry defines the same button, the game's value wins.

The launchBoxId attribute is the LaunchBox Games Database ID and is the primary lookup key —
the plugin finds the entry regardless of your ROM's filename. The romName attribute is
a fallback for games without a database ID.

A space-separated name describes an action performed by pressing several buttons at
once, such as <Input name="BUTTON1 BUTTON2">Power Move</Input>. These are recorded
for the future but are not displayed yet — the plugin notes them in the log and
carries on.

The name attribute is the button as printed on the original hardware (A, B, C for
Sega Genesis; A, B, X, Y, L, R for Super Nintendo; etc.), not the names of buttons
on your Xbox or PlayStation controller. They must match the Mapping/@name values
defined in Defaults\Controllers\{Platform}.xml for that platform — unrecognised
names are silently ignored.

Merging with Defaults\Labels\{Platform}.xml
-------------------------------------------
Your User file is merged entry-by-entry with the shipped Defaults file:
- A User <Game> entry overrides the matching Defaults entry entirely (matched first
  by id, then by name).
- User <Defaults> buttons override matching Defaults buttons; unmentioned Defaults
  buttons are kept.

Contributing
------------
If you create labels for a game, please consider submitting them so they ship as
defaults for everyone. Open a pull request or issue at:
https://github.com/tmstedman/launchbox-dynamic-controls
