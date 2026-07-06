Labels — user overrides
========================

Files here add or override button labels shown on the overlay.

File naming: {Platform}.xml
  One file per platform. Platform name must match LaunchBox's value exactly.

Example:
  User/Labels/Sega Genesis.xml

  <Labels>
      <Defaults>
          <Start>Pause</Start>
      </Defaults>

      <Game launchBoxId="1234" romName="Sonic the Hedgehog (USA)">
          <A>Jump</A>
          <B>Spin Dash</B>
      </Game>
  </Labels>

The <Defaults> block sets labels applied to every game on that platform. If a game
entry defines the same button, the game's value wins.

The launchBoxId attribute is the LaunchBox Games Database ID and is the primary lookup key —
the plugin finds the entry regardless of your ROM's filename. The romName attribute is
a fallback for games without a database ID.

Element names are the button names printed on the original hardware (A, B, C for
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
