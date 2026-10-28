using DynamicControls.Plugins.Mame;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MameCfgLoader"/>: waiting out MAME's write, walking
/// system/input/port/newseq, keeping each port's cfg name, and logging a port that translates to
/// nothing. What a port's sequences reach once read is <see cref="MamePortBinding"/>'s, tested in
/// <see cref="MamePortBindingTests"/>. Filesystem and joycode loader are both substituted so each
/// test supplies a literal cfg XML and a fixed joycode dictionary.
/// </summary>
public class MameCfgLoaderTests
{
    private const string CfgPath = @"C:\mame\cfg\galaga.cfg";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly IJoycodeMappingLoader _joycodeMappingLoader = Substitute.For<IJoycodeMappingLoader>();
    private readonly IDelay _delay = Substitute.For<IDelay>();
    private readonly MameCfgLoader _underTest;

    public MameCfgLoaderTests()
    {
        var joycodeMapping = new JoycodeMapping(new Dictionary<string, List<string>>
        {
            ["JOYCODE_1_BUTTON1"] = ["ButtonA"],
            ["JOYCODE_1_BUTTON2"] = ["ButtonB"],
            ["JOYCODE_1_BUTTON3"] = ["ButtonC"],
            ["JOYCODE_1_HATUP"] = ["Up"],
            ["JOYCODE_1_HATDOWN"] = ["Down"],
            ["JOYCODE_1_START"] = ["Start"],
            ["JOYCODE_1_COIN"] = ["Coin"],
        });
        _joycodeMappingLoader.Load().Returns(joycodeMapping);
        _underTest = new MameCfgLoader(_logger, _fs, _joycodeMappingLoader, _delay);
    }

    private static Dictionary<string, IReadOnlyList<string>> Inputs(IReadOnlyList<MamePortReach>? result)
    {
        result.ShouldNotBeNull();
        return result.ToDictionary(r => r.Name, r => r.Inputs);
    }

    private void StubXml(string xml)
    {
        _fs.FileExists(CfgPath).Returns(true);
        _fs.OpenRead(CfgPath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Load_FileMissing_ReturnsNullAndDoesNotParse()
    {
        // given no cfg file at the given path, on every retry attempt
        _fs.FileExists(CfgPath).Returns(false);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then null is returned, no XML is loaded, and the joycode mapping is not even fetched --
        // all retries are exhausted first, with a delay between each
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
        _joycodeMappingLoader.DidNotReceive().Load();
        _fs.Received(3).FileExists(CfgPath);
        _delay.Received(2).Sleep(Arg.Any<int>());
    }

    [Fact]
    public void Load_FileAppearsOnRetry_ReturnsOverrides()
    {
        // given the file is momentarily missing (MAME still mid-write on exit from a prior
        // launch) but shows up by the second check
        _fs.FileExists(CfgPath).Returns(false, true);
        _fs.OpenRead(CfgPath).Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """)));

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then the retry closes the race and the file is parsed normally
        Inputs(result).ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonA"]));
        _delay.Received(1).Sleep(Arg.Any<int>());
    }

    [Fact]
    public void Load_NoMatchingPorts_ReturnsEmptyDictionary()
    {
        // given a cfg whose root has no <system>/<input>/<port> children
        StubXml("<mameconfig><system name='galaga'><input /></system></mameconfig>");

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then a non-null but empty dictionary is returned (distinguishes "file present, nothing to map"
        // from "file missing"; the caller's cascade treats both as no-op via Count == 0)
        result.ShouldNotBeNull();
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Load_ParsesPlayer1Buttons_KeepsP1Prefix()
    {
        // given a cfg with two player-1 button ports, each with a standard sequence
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port type='P1_BUTTON2'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then each port is keyed by its raw "P1_" type, value is the translated input
        Inputs(result).ShouldBeDictionaryOf(
            ("P1_BUTTON1", ["ButtonA"]),
            ("P1_BUTTON2", ["ButtonB"]));
    }

    [Fact]
    public void Load_CabinetPorts_KeepTheirOwnNames()
    {
        // given a cfg with cabinet/system ports (START1, COIN1) — these use a digit suffix, not "P1_"
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='START1'>
                    <newseq type='standard'>JOYCODE_1_START</newseq>
                  </port>
                  <port type='COIN1'>
                    <newseq type='standard'>JOYCODE_1_COIN</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then each keeps the name the cfg gave it -- the vocabulary is MAME's own, unrewritten
        Inputs(result).ShouldBeDictionaryOf(
            ("START1", ["Start"]),
            ("COIN1", ["Coin"]));
    }

    [Fact]
    public void Load_SecondStartSlotBoundToPlayerOne_KeepsItsDigit()
    {
        // the 20pacgal shape: a cabinet with two game-select buttons for ONE player spends two
        // start slots on them, both wired to player 1's own pad. The digit is a slot number, not
        // proof of whose controller drives it, so START3 has to survive to carry its own label --
        // dropping everything above "1" discarded the second button before it could be labelled
        StubXml("""
            <mameconfig>
              <system name='20pacgal'>
                <input>
                  <port type='START1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port type='START3'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then both survive under their own cfg names, so Labels.xml can address them apart
        Inputs(result).ShouldBeDictionaryOf(
            ("START1", ["ButtonA"]),
            ("START3", ["ButtonB"]));
    }

    [Fact]
    public void Load_GenuineSecondPlayerStart_ProducesNothing()
    {
        // a real player-2 start button -- passing the port through is harmless because
        // JOYCODE_2_* is never in the vocabulary, the same safeguard that makes P2_* safe
        StubXml("""
            <mameconfig>
              <system name='20pacgal'>
                <input>
                  <port type='START2'>
                    <newseq type='standard'>JOYCODE_2_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        result.ShouldBeEmpty();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("unknown JOYCODE") && s.Contains("START2")));
    }

    [Fact]
    public void Load_UnconventionalPortTypes_BoundToPlayerOne_AreKept()
    {
        // the pc_1942 / funcube shape: MAME parks a player-one control in a slot whose name
        // suggests otherwise -- a PlayChoice-10's game-menu button on SERVICE, a touch button on
        // P3. Both are bound to player one's own pad, so both have to survive to be labelled.
        // Dropping them on the port's NAME is what used to lose them; reachability is the
        // joycode's business, checked at translation (see the two tests below).
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P3_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                  <port type='SERVICE'>
                    <newseq type='standard'>JOYCODE_1_BUTTON3</newseq>
                  </port>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then each keeps its own port name, so Labels.xml can address them separately
        Inputs(result).ShouldBeDictionaryOf(
            ("P3_BUTTON1", ["ButtonB"]),
            ("SERVICE", ["ButtonC"]),
            ("P1_BUTTON1", ["ButtonA"]));
    }

    [Fact]
    public void Load_UnconventionalPortType_BoundToAnotherPlayer_ProducesNothing()
    {
        // the counterweight to the test above: now that no port is filtered by name, the joycode
        // is the only thing keeping another player's controls out -- a genuine player-3 button
        // translates to nothing and is dropped at the translation step
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P3_BUTTON1'>
                    <newseq type='standard'>JOYCODE_3_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        result.ShouldBeEmpty();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("unknown JOYCODE") && s.Contains("P3_BUTTON1")));
    }

    [Fact]
    public void Load_P2Port_KeepsP2Prefix_NoLongerDropped()
    {
        // the hwchamp shape: a single-player game borrows a P2 input slot for a second axis or
        // button, but the JOYCODE is still player 1's own controller (#16) -- P2_* now passes
        // through unchanged instead of being dropped, distinct from any real P1_* port
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P2_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        Inputs(result).ShouldBeDictionaryOf(("P2_BUTTON1", ["ButtonA"]));
    }

    [Fact]
    public void Load_GenuineP2Port_BoundToAPlayer2Joycode_ProducesNothing()
    {
        // a real second player's port -- JOYCODE_2_* is never in the vocabulary (this plugin only
        // ever recognizes player 1's controller), so a genuine P2 binding still resolves to
        // nothing even though the P2_* port type itself is no longer dropped outright
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P2_BUTTON1'>
                    <newseq type='standard'>JOYCODE_2_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        result.ShouldBeEmpty();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("unknown JOYCODE") && s.Contains("P2_BUTTON1")));
    }

    [Fact]
    public void Load_UnknownJoycode_IsSkippedAndLogged()
    {
        // given a port whose sequence references a JOYCODE not present in the mapping
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON99</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then no entry is recorded and the unknown JOYCODE is logged for diagnosis
        result.ShouldBeEmpty();
        _logger.Received().Debug(Arg.Is<string>(s => s.Contains("unknown JOYCODE") && s.Contains("BUTTON1")));
    }

    [Fact]
    public void Load_PortWithNoSequenceAtAll_IsSkipped()
    {
        // given a port with no <newseq> children at all, alongside one that has a standard sequence
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1' />
                  <port type='P1_BUTTON3'>
                    <newseq type='standard'>JOYCODE_1_BUTTON3</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then only the port with a sequence contributes; no error is logged for the skip
        Inputs(result).ShouldBeDictionaryOf(("P1_BUTTON3", ["ButtonC"]));
    }

    [Fact]
    public void Load_AllSequencesNone_IsSkipped()
    {
        // given a port whose standard, increment and decrement are all unbound -- a true analogue
        // control with no assignment at all, digital or otherwise
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>NONE</newseq>
                    <newseq type='increment'>NONE</newseq>
                    <newseq type='decrement'>NONE</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then nothing is recorded
        result.ShouldBeEmpty();
    }

    [Fact]
    public void Load_SequenceWithEmptyContent_IsTreatedAsUnbound()
    {
        // an empty <newseq> rather than the usual NONE -- it still declares the sequence, so the
        // port counts as nudgeable, but binds nothing and must not reach anything through it
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_DIAL'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                    <newseq type='increment'></newseq>
                    <newseq type='decrement'>   </newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then the nudge names are still stated, so a baseline for them is dropped rather than
        // left pointing at an axis this cfg never bound
        Inputs(result).ShouldBeDictionaryOf(
            ("P1_DIAL", ["ButtonA"]),
            ("P1_DIAL.inc", []),
            ("P1_DIAL.dec", []));
    }

    [Fact]
    public void Load_SamePortTypeTwice_TheLaterKeepsTheName_InTheEarliersPlace_AndTheEarlierIsQualified()
    {
        // the dingo shape: MAME writes one <port> per tag and bit a type sits on, so a type can
        // appear twice. The later keeps the bare name and the first one's place, as before; the earlier
        // is kept too, under its type, tag and mask, after everything else
        StubXml("""
            <mameconfig>
              <system name='dingo'>
                <input>
                  <port tag=':IN0' type='P1_BUTTON1' mask='16'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port tag=':IN0' type='P1_BUTTON2' mask='32'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                  <port tag=':IN1' type='P1_BUTTON1' mask='16'>
                    <newseq type='standard'>JOYCODE_1_BUTTON3</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        var result = _underTest.Load(CfgPath);

        result.ShouldNotBeNull();
        result.Select(r => (r.Name, string.Join(" ", r.Inputs))).ShouldBe(
        [
            ("P1_BUTTON1", "ButtonC"),
            ("P1_BUTTON2", "ButtonB"),
            ("P1_BUTTON1:IN0:16", "ButtonA"),
        ]);
    }

    [Fact]
    public void Load_OneTypeOnOneTagManyTimes_EachEarlierIsQualifiedByItsMask()
    {
        // escounts: four OTHER ports on one tag, told apart only by mask. The last keeps the bare
        // name; each earlier one is qualified, in the order it was displaced
        StubXml("""
            <mameconfig>
              <system name='escounts'>
                <input>
                  <port tag=':STROBE1' type='OTHER' mask='1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port tag=':STROBE1' type='OTHER' mask='2'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                  <port tag=':STROBE1' type='OTHER' mask='4'>
                    <newseq type='standard'>JOYCODE_1_BUTTON3</newseq>
                  </port>
                  <port tag=':STROBE1' type='OTHER' mask='16'>
                    <newseq type='standard'>JOYCODE_1_START</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        var result = _underTest.Load(CfgPath);

        result.ShouldNotBeNull();
        result.Select(r => (r.Name, string.Join(" ", r.Inputs))).ShouldBe(
        [
            ("OTHER", "Start"),
            ("OTHER:STROBE1:1", "ButtonA"),
            ("OTHER:STROBE1:2", "ButtonB"),
            ("OTHER:STROBE1:4", "ButtonC"),
        ]);
    }

    [Fact]
    public void Load_RepeatedPortWithNudges_ItsNudgesAreQualifiedWithIt()
    {
        // a nudge is named after its port, so a qualified port's nudges carry the qualified name
        StubXml("""
            <mameconfig>
              <system name='twodials'>
                <input>
                  <port tag=':DIAL0' type='P1_DIAL' mask='255'>
                    <newseq type='increment'>JOYCODE_1_BUTTON1</newseq>
                    <newseq type='decrement'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                  <port tag=':DIAL1' type='P1_DIAL' mask='255'>
                    <newseq type='increment'>JOYCODE_1_BUTTON3</newseq>
                    <newseq type='decrement'>JOYCODE_1_START</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        Inputs(_underTest.Load(CfgPath)).ShouldBeDictionaryOf(
            ("P1_DIAL", []),
            ("P1_DIAL.inc", ["ButtonC"]),
            ("P1_DIAL.dec", ["Start"]),
            ("P1_DIAL:DIAL0:255", []),
            ("P1_DIAL:DIAL0:255.inc", ["ButtonA"]),
            ("P1_DIAL:DIAL0:255.dec", ["ButtonB"]));
    }

    [Fact]
    public void Load_RepeatedPortWithNudgesTheLaterLacks_IsQualifiedWholeWithItsNudges()
    {
        // only the earlier port has nudges: it is displaced as a port, so its nudges go with it,
        // and the bare nudge names stay stated but empty so no baseline guess returns for them
        StubXml("""
            <mameconfig>
              <system name='twodials'>
                <input>
                  <port tag=':DIAL0' type='P1_DIAL' mask='255'>
                    <newseq type='increment'>JOYCODE_1_BUTTON1</newseq>
                    <newseq type='decrement'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                  <port tag=':DIAL1' type='P1_DIAL' mask='255'>
                    <newseq type='standard'>JOYCODE_1_START</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        Inputs(_underTest.Load(CfgPath)).ShouldBeDictionaryOf(
            ("P1_DIAL", ["Start"]),
            ("P1_DIAL.inc", []),
            ("P1_DIAL.dec", []),
            ("P1_DIAL:DIAL0:255", []),
            ("P1_DIAL:DIAL0:255.inc", ["ButtonA"]),
            ("P1_DIAL:DIAL0:255.dec", ["ButtonB"]));
    }

    [Fact]
    public void Load_RepeatedPortsWithNoTagOrMask_AreQualifiedByPosition()
    {
        // nothing on the port tells them apart, so the earlier is named by its place among its
        // type -- never the bare type, which belongs to the later
        StubXml("""
            <mameconfig>
              <system name='bare'>
                <input>
                  <port type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON1</newseq></port>
                  <port type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON3</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        Inputs(_underTest.Load(CfgPath)).ShouldBeDictionaryOf(
            ("P1_BUTTON1", ["ButtonC"]),
            ("P1_BUTTON1#1", ["ButtonA"]));
    }

    [Fact]
    public void Load_PositionCountsEveryPortOfTheType_IncludingOnesThatReachNothing()
    {
        // the position is read off the cfg, so a port bound to another player still counts: the
        // earlier player-1 port is the type's second, whatever the loader made of the first
        StubXml("""
            <mameconfig>
              <system name='bare'>
                <input>
                  <port type='P1_BUTTON1'><newseq type='standard'>JOYCODE_2_BUTTON1</newseq></port>
                  <port type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON1</newseq></port>
                  <port type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON3</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        Inputs(_underTest.Load(CfgPath)).ShouldBeDictionaryOf(
            ("P1_BUTTON1", ["ButtonC"]),
            ("P1_BUTTON1#2", ["ButtonA"]));
    }

    [Fact]
    public void Load_RepeatedPortsSharingATagWithNoMask_AreQualifiedApartByPosition()
    {
        // tag alone names both earlier ports the same, so the second of them adds its position
        StubXml("""
            <mameconfig>
              <system name='nomask'>
                <input>
                  <port tag=':IN0' type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON1</newseq></port>
                  <port tag=':IN0' type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON2</newseq></port>
                  <port tag=':IN0' type='P1_BUTTON1'><newseq type='standard'>JOYCODE_1_BUTTON3</newseq></port>
                </input>
              </system>
            </mameconfig>
            """);

        Inputs(_underTest.Load(CfgPath)).ShouldBeDictionaryOf(
            ("P1_BUTTON1", ["ButtonC"]),
            ("P1_BUTTON1:IN0", ["ButtonA"]),
            ("P1_BUTTON1:IN0#2", ["ButtonB"]));
    }

    [Fact]
    public void Load_RepeatedTypeWhoseEarlierPortReachesNothing_LeavesNothingToQualify()
    {
        // the earlier port is another player's, so it never took the name and nothing is displaced
        StubXml("""
            <mameconfig>
              <system name='twoslots'>
                <input>
                  <port tag=':IN0' type='P1_BUTTON1' mask='1'>
                    <newseq type='standard'>JOYCODE_2_BUTTON1</newseq>
                  </port>
                  <port tag=':IN1' type='P1_BUTTON1' mask='1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        Inputs(_underTest.Load(CfgPath)).ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonA"]));
    }

    [Fact]
    public void Load_PortWithoutTypeAttribute_IsSkipped()
    {
        // given a port element with no type attribute (triggers the null branch of Attributes["type"]?.Value)
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port type='P1_BUTTON2'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then the typeless port is skipped and only the typed port contributes
        Inputs(result).ShouldBeDictionaryOf(("P1_BUTTON2", ["ButtonB"]));
    }

    [Fact]
    public void Load_SeqNodeWithoutTypeAttribute_IsSkipped()
    {
        // given a newseq element with no type attribute (triggers the null branch of Attributes["type"]?.Value)
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port type='P1_BUTTON2'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then the typeless newseq is not treated as standard and BUTTON1 is skipped
        Inputs(result).ShouldBeDictionaryOf(("P1_BUTTON2", ["ButtonB"]));
    }

    [Fact]
    public void Load_TriggersJoycodeMappingLoad_OnceAcrossManyPorts()
    {
        // given a cfg with multiple ports
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                  <port type='P1_BUTTON2'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        _underTest.Load(CfgPath);

        // then the joycode mapping is fetched exactly once for the whole file
        // (not per-port — the mapping is shared across all translations)
        _joycodeMappingLoader.Received(1).Load();
    }
}
