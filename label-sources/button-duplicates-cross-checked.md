# Button-duplicate triage, cross-checked against controls.xml

98 flagged cases: 23 confirmed error, 7 partial evidence of an error, 6 corroborated, 62 fully inconclusive (rom or all flagged ports missing from the DB).

## Confirmed errors (23)

controls.xml independently gives these ports DIFFERENT functions -- the artwork's shared label is very likely a mislabel of one of them.

- **astdelux** `BUTTON1`, `BUTTON2` artwork='Move' vs controls.xml: BUTTON1='FIRE', BUTTON2='THRUST'
- **asteroid** `BUTTON1`, `BUTTON2` artwork='Move' vs controls.xml: BUTTON1='FIRE', BUTTON2='THRUST'
- **bjtwin** `BUTTON1`, `BUTTON2` artwork='Jump' vs controls.xml: BUTTON1='Jump', BUTTON2='Flap'
- **cachat** `BUTTON1`, `BUTTON2` artwork='Rotate / (Hold) Move' vs controls.xml: BUTTON1='-90deg', BUTTON2='+90deg'
- **ecofghtr** `BUTTON1`, `BUTTON3` artwork='Rotate Shoot Position' vs controls.xml: BUTTON1='Rotate CCW', BUTTON3='Rotate CW'
- **finalizr** `BUTTON1`, `BUTTON2` artwork='Shoot' vs controls.xml: BUTTON1='Fire', BUTTON2='Shield'
- **gt2k** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' vs controls.xml: BUTTON1='Face Left', BUTTON2='Face Right'
- **gt3d** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' vs controls.xml: BUTTON1='Face Left', BUTTON2='Face Right'
- **gt97** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' vs controls.xml: BUTTON1='Face Left', BUTTON2='Face Right'
- **gt98** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' vs controls.xml: BUTTON1='Face Left', BUTTON2='Face Right'
- **gt99** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' vs controls.xml: BUTTON1='Face Left', BUTTON2='Face Right'
- **gtg2** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' vs controls.xml: BUTTON1='Face Left', BUTTON2='Face Right'
- **kinst** `BUTTON3`, `BUTTON6` artwork='Fierce Kick' vs controls.xml: BUTTON3='Fierce (High Attack)', BUTTON6='Fierce (Low Attack)'
- **kinst2** `BUTTON3`, `BUTTON6` artwork='Fierce Kick' vs controls.xml: BUTTON3='Fierce (High Attack)', BUTTON6='Fierce (Low Attack)'
- **mk** `BUTTON1`, `BUTTON5` artwork='Low Kick' vs controls.xml: BUTTON1='High Punch', BUTTON5='Low Kick'
- **mk2** `BUTTON1`, `BUTTON5` artwork='Low Kick' vs controls.xml: BUTTON1='High Punch', BUTTON5='Low Kick'
- **mk3** `BUTTON1`, `BUTTON5` artwork='Low Kick' vs controls.xml: BUTTON1='High Punch', BUTTON5='Low Kick'
- **puyopuy2** `BUTTON1`, `BUTTON2` artwork='Spin Piece' vs controls.xml: BUTTON1='Rotate Right', BUTTON2='Rotate Left'
- **spaceskr** `BUTTON1`, `BUTTON2` artwork='Shoot' vs controls.xml: BUTTON1='Fire', BUTTON2='Fire/Bomb'
- **sparkz** `BUTTON1`, `BUTTON2` artwork='Rotate' vs controls.xml: BUTTON1='Rotate Left', BUTTON2='Rotate Right'
- **umk3** `BUTTON1`, `BUTTON5` artwork='Low Kick' vs controls.xml: BUTTON1='Run', BUTTON5='Low Kick'
- **vindctr2** `BUTTON2`, `BUTTON4` artwork='Rotate' vs controls.xml: BUTTON2='rotate left', BUTTON4='rotate right'
- **vindictr** `BUTTON2`, `BUTTON4` artwork='Rotate' vs controls.xml: BUTTON2='Rotate left', BUTTON4='Rotate right'

## Partial evidence of an error (7)

At least one flagged port has no DB label at all, but the port(s) that DO have one disagree with each other or with the artwork's shared text -- worth a look even though not fully confirmed both ways.

- **bloxeed** `BUTTON1`, `BUTTON2` artwork='Spin Piece' (controls.xml: {'BUTTON1': 'Rotate', 'BUTTON2': None})
- **llander** `BUTTON1`, `BUTTON2` artwork='Move' (controls.xml: {'BUTTON1': 'Abort', 'BUTTON2': None})
- **megablst** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: {'BUTTON1': 'Fire', 'BUTTON2': None})
- **mplanets** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: {'BUTTON1': 'Fire', 'BUTTON2': None})
- **pacland** `BUTTON1`, `BUTTON2` artwork='Move' (controls.xml: {'BUTTON1': 'Jump', 'BUTTON2': None})
- **roadriot** `BUTTON2`, `BUTTON3` artwork='Shoot' (controls.xml: {'BUTTON2': 'Fire', 'BUTTON3': None})
- **stdragon** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: {'BUTTON1': 'Fire', 'BUTTON2': None})

## Corroborated (6)

controls.xml agrees these ports genuinely share one function -- likely a real hardware duplicate (e.g. pinball dual flippers), not an error.

- **airbustr** `BUTTON1`, `BUTTON2` artwork='Shoot / (Hold) Charge' vs controls.xml: BUTTON1='Fire', BUTTON2='Fire'
- **ccastles** `BUTTON1`, `BUTTON2` artwork='Jump' vs controls.xml: BUTTON1='Jump', BUTTON2=None
- **galastrm** `BUTTON1`, `BUTTON2` artwork='Shoot' vs controls.xml: BUTTON1='Fire', BUTTON2='Fire'
- **mk** `BUTTON2`, `BUTTON6` artwork='Block' vs controls.xml: BUTTON2='Block', BUTTON6=None
- **mk2** `BUTTON2`, `BUTTON6` artwork='Block' vs controls.xml: BUTTON2='Block', BUTTON6=None
- **sharrier** `BUTTON1`, `BUTTON2` artwork='Shoot' vs controls.xml: BUTTON1='Shot', BUTTON2='Shot'

## Fully inconclusive (62)

Rom not in controls.xml, or none of the flagged ports have a label there.

- **androidp** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: None)
- **anpanman** `BUTTON1`, `BUTTON2` artwork='(Tap) Run' (controls.xml: None)
- **backfirt** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: None)
- **bakubaku** `BUTTON1`, `BUTTON2` artwork='Spin Piece' (controls.xml: None)
- **bjtwinp** `BUTTON1`, `BUTTON2` artwork='Jump' (controls.xml: None)
- **buriki** `BUTTON1`, `BUTTON2` artwork='Move' (controls.xml: None)
- **ccastlesj** `BUTTON1`, `BUTTON2` artwork='Jump' (controls.xml: None)
- **coolridr** `BUTTON1`, `BUTTON2` artwork='Skip Music Track' (controls.xml: None)
- **cubeqst** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: None)
- **dcclub** `BUTTON3`, `BUTTON4` artwork='Move' (controls.xml: None)
- **galspnbl** `BUTTON1`, `BUTTON3` artwork='Left Flipper / Launch' (controls.xml: None)
- **galspnbl** `BUTTON2`, `BUTTON4` artwork='Right Flipper / Launch' (controls.xml: None)
- **gcpinbal** `BUTTON1`, `BUTTON3` artwork='Left Flipper' (controls.xml: None)
- **gcpinbal** `BUTTON2`, `BUTTON4` artwork='Right Flipper' (controls.xml: None)
- **gcpinbal** `BUTTON7`, `BUTTON8` artwork='Tilt' (controls.xml: None)
- **grdforce** `BUTTON1`, `BUTTON3` artwork='Turn Turret' (controls.xml: None)
- **gt2kt500** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gt3dt231** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gt97t243** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gt98t303** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gt99t400** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gtclassc** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gtdiamond** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gtfore01** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtfore02** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtfore03** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtfore04** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtfore04a** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtfore05** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtfore06** `BUTTON1`, `BUTTON3` artwork='Move Left & Right' (controls.xml: None)
- **gtroyal** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **gtsupreme** `BUTTON1`, `BUTTON2` artwork='Move Left & Right' (controls.xml: None)
- **letsdnce** `BUTTON1`, `BUTTON2`, `BUTTON3`, `BUTTON4` artwork='Move' (controls.xml: None)
- **mausuke** `BUTTON1`, `BUTTON2` artwork='Spin Piece' (controls.xml: None)
- **mp_bio** `BUTTON2`, `BUTTON3` artwork='Shoot / (Hold) Charge' (controls.xml: None)
- **mp_col3** `BUTTON1`, `BUTTON3` artwork='Spin Piece' (controls.xml: None)
- **orunners** `BUTTON4`, `BUTTON5` artwork='Skip Music Track' (controls.xml: {'BUTTON4': None, 'BUTTON5': None})
- **orunnersu** `BUTTON4`, `BUTTON5` artwork='Skip Music Track' (controls.xml: None)
- **panicr** `BUTTON3`, `BUTTON4` artwork='Tilt' (controls.xml: None)
- **pc_duckh** `BUTTON1`, `BUTTON3` artwork='B' (controls.xml: None)
- **pc_wgnmn** `BUTTON1`, `BUTTON3` artwork='B' (controls.xml: None)
- **peekaboo** `BUTTON2`, `BUTTON3` artwork='Move' (controls.xml: None)
- **pnyaa** `BUTTON1`, `BUTTON2` artwork='Rotate' (controls.xml: None)
- **puyosun** `BUTTON1`, `BUTTON2` artwork='Spin Piece' (controls.xml: None)
- **puzzloop** `BUTTON1`, `BUTTON2` artwork='Shoot' (controls.xml: None)
- **pwrflip** `BUTTON1`, `BUTTON3` artwork='Left Flipper' (controls.xml: None)
- **pwrflip** `BUTTON2`, `BUTTON4` artwork='Right Flipper' (controls.xml: None)
- **pwrflip** `BUTTON7`, `BUTTON8` artwork='Tilt' (controls.xml: None)
- **roadedge** `BUTTON1`, `BUTTON2`, `BUTTON3`, `BUTTON4` artwork='Change Music' (controls.xml: None)
- **sgmast** `BUTTON3`, `BUTTON4` artwork='Move' (controls.xml: None)
- **showdown** `BUTTON1`, `BUTTON7` artwork='Shoot' (controls.xml: None)
- **sonicpop** `BUTTON1`, `BUTTON2` artwork='(Tap) Run' (controls.xml: None)
- **spacezap** `BUTTON2`, `BUTTON3`, `BUTTON4`, `BUTTON5` artwork='Move' (controls.xml: {'BUTTON2': None, 'BUTTON3': None, 'BUTTON4': None, 'BUTTON5': None})
- **stompin** `BUTTON2`, `BUTTON3`, `BUTTON4`, `BUTTON5`, `BUTTON6`, `BUTTON7`, `BUTTON8` artwork='Stomp' (controls.xml: None)
- **tetris2** `BUTTON1`, `BUTTON2` artwork='Spin Piece' (controls.xml: None)
- **tetrisse** `BUTTON1`, `BUTTON2` artwork='Spin Piece' (controls.xml: None)
- **truckk** `BUTTON5`, `BUTTON6` artwork='Change Music' (controls.xml: None)
- **ts2** `BUTTON1`, `BUTTON4` artwork='Roll' (controls.xml: None)
- **vaportrx** `BUTTON2`, `BUTTON4` artwork='Shoot' (controls.xml: None)
- **wcvol95** `BUTTON1`, `BUTTON2` artwork='Hit Ball' (controls.xml: None)
- **wcvol95x** `BUTTON1`, `BUTTON2` artwork='Hit Ball' (controls.xml: None)
- **xrally** `BUTTON1`, `BUTTON2`, `BUTTON3`, `BUTTON4` artwork='Change Music' (controls.xml: None)
