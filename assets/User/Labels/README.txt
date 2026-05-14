Labels — user overrides
========================

Files here add or override button labels shown on the overlay.

File naming: {Platform}/{Game}.xml  or  {Platform}/_DefaultLabels.xml
  Platform and game names must match LaunchBox's values exactly.
  _DefaultLabels.xml entries apply to every game on that platform and are
  merged into game-specific files, with game-specific entries taking precedence.

Example — per-game labels:
  User/Labels/Sega Genesis/Sonic the Hedgehog (USA).xml

  <InputLabels>
      <A>Jump</A>
      <B>Spin Dash</B>
      <Start>Pause</Start>
  </InputLabels>

Example — platform defaults:
  User/Labels/Sega Genesis/_DefaultLabels.xml

  <InputLabels>
      <Start>Pause</Start>
  </InputLabels>

Element names are the button names printed on the original hardware (A, B, C for
Sega Genesis; A, B, X, Y, L, R for Super Nintendo; etc.), not the names of buttons
on your Xbox or PlayStation controller. They must match the Mapping/@name values
defined in Defaults\Controllers\{Platform}.xml for that platform — unrecognised
names are silently ignored.

Contributing
------------
If you create labels for a game, please consider submitting them so they ship as
defaults for everyone. Open a pull request or issue at:
https://github.com/tmstedman/launchbox-dynamic-controls
