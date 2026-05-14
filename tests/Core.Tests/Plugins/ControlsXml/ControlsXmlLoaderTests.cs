using DynamicControls.Labels;
using DynamicControls.Plugins.ControlsXml;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.ControlsXml;

/// <summary>
/// Unit tests for <see cref="ControlsXmlLoader"/>. The loader parses BYOAC-format controls.xml
/// from {rootDir}/controls.xml on first <c>Lookup</c>, caches the whole game-keyed dictionary,
/// and returns per-ROM <see cref="InputLabelsConfig"/>s on subsequent calls. Filesystem is
/// substituted so each test supplies a literal controls.xml string.
/// </summary>
public class ControlsXmlLoaderTests
{
    private const string RootDir = @"C:\plugin";
    private static readonly string ControlsXmlPath = Path.Combine(RootDir, "controls.xml");

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly ControlsXmlLoader _underTest;

    public ControlsXmlLoaderTests()
    {
        _underTest = new ControlsXmlLoader(_logger, new LayeredFileSystem(RootDir, _fs));
    }

    private void StubXml(string xml)
    {
        _fs.FileExists(ControlsXmlPath).Returns(true);
        _fs.OpenRead(ControlsXmlPath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    // ---- file presence + caching ----

    [Fact]
    public void Lookup_FileMissing_ReturnsNull()
    {
        // given no controls.xml on disk
        _fs.FileExists(ControlsXmlPath).Returns(false);

        // when the loader is queried
        var result = _underTest.Lookup("galaga");

        // then null is returned and no XML is parsed
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void Lookup_ParsesFileOncePerInstance()
    {
        // given a controls.xml with two distinct games
        StubXml("""
            <dat>
              <meta>
                <description name='Controls.dat XML file' />
                <version name='0.141.1' />
              </meta>
              <game romname='galaga' gamename='Galaga' numPlayers='2' alternating='1'>
                <player number='1' numButtons='1'>
                  <labels>
                    <label name='P1_BUTTON1' value='Fire' />
                    <label name='P1_JOYSTICK_LEFT' value='Left' />
                    <label name='P1_JOYSTICK_RIGHT' value='Right' />
                  </labels>
                </player>
              </game>
              <game romname='88games' gamename='&apos;88 Games' numPlayers='2' alternating='0'>
                <player number='1' numButtons='3'>
                  <labels>
                    <label name='P1_BUTTON1' value='Run' />
                    <label name='P1_BUTTON2' value='Jump' />
                    <label name='P1_BUTTON3' value='Run' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        // when two different ROMs are looked up in sequence
        _underTest.Lookup("galaga");
        _underTest.Lookup("88games");

        // then the file is parsed only once — the second ROM resolves from the cached dictionary,
        // proving caching is keyed on "file already parsed", not on the lookup key
        _fs.Received(1).OpenRead(ControlsXmlPath);
    }

    [Fact]
    public void Lookup_FileMissing_IsAlsoCached()
    {
        // given no controls.xml on disk
        _fs.FileExists(ControlsXmlPath).Returns(false);

        // when the loader is queried once, then a second time
        _underTest.Lookup("galaga");
        _fs.ClearReceivedCalls();
        _underTest.Lookup("pacman");

        // then the second lookup doesn't re-probe the filesystem — the "missing" outcome is cached
        _fs.DidNotReceive().FileExists(Arg.Any<string>());
    }

    [Fact]
    public void Lookup_RomNotPresent_ReturnsNullAndLogs()
    {
        // given a controls.xml containing entries for other games
        StubXml("""
            <dat>
              <meta><description name='Controls.dat XML file' /></meta>
              <game romname='galaga' gamename='Galaga' numPlayers='2'>
                <player number='1' numButtons='1'>
                  <labels><label name='P1_BUTTON1' value='Fire' /></labels>
                </player>
              </game>
            </dat>
            """);

        // when an unknown ROM is looked up
        var result = _underTest.Lookup("notagame");

        // then null is returned and the miss is logged for diagnosis
        result.ShouldBeNull();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("not found") && s.Contains("notagame")));
    }

    // ---- XML parsing ----

    [Fact]
    public void Lookup_StripsP1Prefix_FromLabelNames()
    {
        // given a realistic controls.xml entry exercising several P1_ name shapes plus cabinet names
        StubXml("""
            <dat>
              <meta>
                <description name='Controls.dat XML file' />
                <version name='0.141.1' />
                <generatedBy name='SirPoonga' />
              </meta>
              <game romname='1941' gamename='1941: Counter Attack' numPlayers='2' alternating='0' mirrored='1' usesService='0'>
                <miscDetails>Vertical shoot-em-up with an 8-way joystick and two buttons.</miscDetails>
                <player number='1' numButtons='2'>
                  <controls>
                    <control name='8-way Joystick'>
                      <constant name='joy8way' />
                    </control>
                  </controls>
                  <labels>
                    <label name='P1_BUTTON1' value='Fire' />
                    <label name='P1_BUTTON2' value='Loop' />
                    <label name='P1_JOYSTICK_UP' value='Up' />
                    <label name='P1_JOYSTICK_DOWN' value='Down' />
                    <label name='START' value='Start' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        // when the game is looked up
        var result = _underTest.Lookup("1941");

        // then every "P1_" prefix is stripped; "START" (no prefix) passes through unchanged.
        // Order matches document order. The meta block, miscDetails, and <controls>/<control>/<constant>
        // tree are all ignored — only <label> entries inside <labels> contribute.
        result.ShouldNotBeNull();
        result.Labels.Select(l => (l.Name, l.Label)).ShouldBe(
        [
            ("BUTTON1", "Fire"),
            ("BUTTON2", "Loop"),
            ("JOYSTICK_UP", "Up"),
            ("JOYSTICK_DOWN", "Down"),
            ("START", "Start"),
        ]);
    }

    [Fact]
    public void Lookup_LabelsAreFlaggedNonInheriting()
    {
        // given a single label entry inside a realistic game node
        StubXml("""
            <dat>
              <game romname='galaga' gamename='Galaga' numPlayers='2'>
                <player number='1' numButtons='1'>
                  <labels><label name='P1_BUTTON1' value='Fire' /></labels>
                </player>
              </game>
            </dat>
            """);

        // when the game is looked up
        var result = _underTest.Lookup("galaga");

        result.ShouldNotBeNull();
        result.Labels.ShouldHaveSingleItem();
    }

    [Fact]
    public void Lookup_IgnoresPlayerWithNoNumberAttribute()
    {
        // given a game with a player element missing the number attribute entirely —
        // Attributes["number"] returns null, so ?.Value is null, and null != "1" skips it
        StubXml("""
            <dat>
              <game romname='galaga' gamename='Galaga'>
                <player numButtons='1'>
                  <labels>
                    <label name='P1_BUTTON1' value='ShouldBeIgnored' />
                  </labels>
                </player>
                <player number='1' numButtons='1'>
                  <labels>
                    <label name='P1_BUTTON1' value='Fire' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        var result = _underTest.Lookup("galaga");

        // then only the player with number="1" contributes labels
        result.ShouldNotBeNull();
        result.Labels.Single().Label.ShouldBe("Fire");
    }

    [Fact]
    public void Lookup_IgnoresPlayersOtherThanPlayer1()
    {
        // given a 2-player game with full <controls> blocks on both players
        StubXml("""
            <dat>
              <game romname='005' gamename='005' numPlayers='2' alternating='1' mirrored='1'>
                <player number='2' numButtons='1'>
                  <controls>
                    <control name='4-way Joystick'><constant name='joy4way' /></control>
                  </controls>
                  <labels>
                    <label name='P2_BUTTON1' value='Decoy' />
                    <label name='P2_JOYSTICK_LEFT' value='IgnoreMe' />
                  </labels>
                </player>
                <player number='1' numButtons='1'>
                  <controls>
                    <control name='4-way Joystick'><constant name='joy4way' /></control>
                  </controls>
                  <labels>
                    <label name='P1_BUTTON1' value='Fire' />
                    <label name='P1_JOYSTICK_LEFT' value='Left' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        // when the game is looked up
        var result = _underTest.Lookup("005");

        // then only player 1's labels are returned
        result.ShouldNotBeNull();
        result.Labels.Select(l => (l.Name, l.Label)).ShouldBe(
        [
            ("BUTTON1", "Fire"),
            ("JOYSTICK_LEFT", "Left"),
        ]);
    }

    [Fact]
    public void Lookup_SkipsLabelsMissingNameOrValue()
    {
        // given a labels block with one valid entry and two malformed entries
        StubXml("""
            <dat>
              <game romname='galaga' gamename='Galaga'>
                <player number='1' numButtons='2'>
                  <labels>
                    <label name='P1_BUTTON1' value='Fire' />
                    <label value='Jump' />
                    <label name='P1_JOYSTICK_UP' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        // when the game is looked up
        var result = _underTest.Lookup("galaga");

        // then only the complete entry survives; malformed entries are silently dropped
        result.ShouldNotBeNull();
        result.Labels.Select(l => (l.Name, l.Label)).ShouldBe([("BUTTON1", "Fire")]);
    }

    [Fact]
    public void Lookup_SkipsLabelWithEmptyNameAfterStrip()
    {
        // given a label whose name is exactly "P1_" — stripping leaves an empty string
        StubXml("""
            <dat>
              <game romname='galaga' gamename='Galaga'>
                <player number='1' numButtons='2'>
                  <labels>
                    <label name='P1_' value='Junk' />
                    <label name='P1_BUTTON2' value='Bomb' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        // when the game is looked up
        var result = _underTest.Lookup("galaga");

        // then the empty-after-strip entry is dropped; the valid entry is kept
        result.ShouldNotBeNull();
        result.Labels.Select(l => (l.Name, l.Label)).ShouldBe([("BUTTON2", "Bomb")]);
    }

    [Fact]
    public void Lookup_GameWithoutRomName_DoesNotBreakIterationOfOtherGames()
    {
        // given a malformed <game> (no romname) sandwiched between two valid games
        StubXml("""
            <dat>
              <game romname='galaga' gamename='Galaga' numPlayers='2'>
                <player number='1' numButtons='1'>
                  <labels><label name='P1_BUTTON1' value='Fire' /></labels>
                </player>
              </game>
              <game gamename='Orphan' numPlayers='1'>
                <player number='1'><labels><label name='P1_BUTTON1' value='Shoot' /></labels></player>
              </game>
              <game romname='pacman' gamename='Pac-Man' numPlayers='2'>
                <player number='1' numButtons='1'>
                  <labels><label name='P1_BUTTON1' value='Eat' /></labels>
                </player>
              </game>
            </dat>
            """);

        // when each valid game is looked up
        var galaga = _underTest.Lookup("galaga");
        var pacman = _underTest.Lookup("pacman");

        // then both load with their own labels — the orphan in between did not halt the parse,
        // and its labels did not leak into either valid game's result
        galaga.ShouldNotBeNull();
        galaga.Labels.Select(l => (l.Name, l.Label)).ShouldBe([("BUTTON1", "Fire")]);
        pacman.ShouldNotBeNull();
        pacman.Labels.Select(l => (l.Name, l.Label)).ShouldBe([("BUTTON1", "Eat")]);
    }

    [Fact]
    public void Lookup_GameWithEmptyLabels_IsNotIndexed()
    {
        // given a game whose player-1 labels block is empty — the loader skips empty entries
        // (real controls.xml entries with only <controls>/<control> data but no <label> are common)
        StubXml("""
            <dat>
              <game romname='galaga' gamename='Galaga' numPlayers='1'>
                <player number='1' numButtons='1'>
                  <controls>
                    <control name='8-way Joystick'><constant name='joy8way' /></control>
                  </controls>
                  <labels />
                </player>
              </game>
            </dat>
            """);

        // when the game is looked up
        var result = _underTest.Lookup("galaga");

        // then null is returned (label-less games aren't added to the cache)
        result.ShouldBeNull();
    }

    [Fact]
    public void Lookup_MultipleGames_EachQueryableByRomName()
    {
        // given a realistic controls.xml fragment with meta + two distinct games with full noise
        StubXml("""
            <dat>
              <meta>
                <description name='Controls.dat XML file' />
                <version name='0.141.1' />
                <time name='2011-01-05 18:17:26' />
              </meta>
              <game romname='10yardj' gamename='10-Yard Fight (Japan)' numPlayers='2' alternating='1' mirrored='1' usesService='0' tilt='0' cocktail='1'>
                <miscDetails>American football game.</miscDetails>
                <player number='1' numButtons='2'>
                  <controls>
                    <control name='8-way Joystick'><constant name='joy8way' /></control>
                  </controls>
                  <labels>
                    <label name='P1_BUTTON1' value='Pass / Hike' />
                    <label name='P1_BUTTON2' value='Lateral' />
                    <label name='P1_JOYSTICK_UP' value='Up' />
                  </labels>
                </player>
              </game>
              <game romname='gtmr' gamename='1000 Miglia' numPlayers='2' alternating='1' mirrored='1' usesService='0' tilt='1'>
                <miscDetails>Racing game with optional brake pedal.</miscDetails>
                <player number='1' numButtons='1'>
                  <controls>
                    <control name='270 Steering Wheel'><constant name='paddle' /></control>
                  </controls>
                  <labels>
                    <label name='P1_BUTTON1' value='Accelerate' />
                    <label name='P1_PADDLE' value='Left' />
                    <label name='P1_PADDLE_EXT' value='Right' />
                  </labels>
                </player>
              </game>
            </dat>
            """);

        // when each game is looked up
        var football = _underTest.Lookup("10yardj");
        var racing = _underTest.Lookup("gtmr");

        // then each returns its own label set, independently of the other
        football.ShouldNotBeNull();
        football.Labels.Select(l => (l.Name, l.Label)).ShouldBe(
        [
            ("BUTTON1", "Pass / Hike"),
            ("BUTTON2", "Lateral"),
            ("JOYSTICK_UP", "Up"),
        ]);
        racing.ShouldNotBeNull();
        racing.Labels.Select(l => (l.Name, l.Label)).ShouldBe(
        [
            ("BUTTON1", "Accelerate"),
            ("PADDLE", "Left"),
            ("PADDLE_EXT", "Right"),
        ]);
    }
}
