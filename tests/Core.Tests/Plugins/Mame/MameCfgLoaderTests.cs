using DynamicControls.Plugins.Mame;
using NSubstitute;

namespace DynamicControls.Core.Tests.Plugins.Mame;

/// <summary>
/// Unit tests for <see cref="MameCfgLoader"/>. The loader reads a MAME .cfg file and produces a
/// dictionary of {input name -> generic inputs} by walking system/input/port/newseq, translating
/// whichever of standard/increment/decrement are bound (standard-first, deduped) via an injected
/// <see cref="IJoycodeMappingLoader"/>. Filesystem and joycode loader are both substituted so each
/// test supplies a literal cfg XML and a fixed joycode dictionary.
/// </summary>
public class MameCfgLoaderTests
{
    private const string CfgPath = @"C:\mame\cfg\galaga.cfg";

    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private readonly IJoycodeMappingLoader _joycodeMappingLoader = Substitute.For<IJoycodeMappingLoader>();
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
        _underTest = new MameCfgLoader(_logger, _fs, _joycodeMappingLoader);
    }

    private void StubXml(string xml)
    {
        _fs.FileExists(CfgPath).Returns(true);
        _fs.OpenRead(CfgPath).Returns(new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    [Fact]
    public void Load_FileMissing_ReturnsNullAndDoesNotParse()
    {
        // given no cfg file at the given path
        _fs.FileExists(CfgPath).Returns(false);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then null is returned, no XML is loaded, and the joycode mapping is not even fetched
        result.ShouldBeNull();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
        _joycodeMappingLoader.DidNotReceive().Load();
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
        result.ShouldBeDictionaryOf(
            ("P1_BUTTON1", ["ButtonA"]),
            ("P1_BUTTON2", ["ButtonB"]));
    }

    [Fact]
    public void Load_NormalizesCabinetPorts_StripTrailingOne()
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

        // then the trailing "1" is dropped to produce canonical START/COIN keys
        result.ShouldBeDictionaryOf(
            ("START", ["Start"]),
            ("COIN", ["Coin"]));
    }

    [Fact]
    public void Load_IgnoresPlayer3PlusAndUnknownPortTypes()
    {
        // given a cfg with P3 / unrecognized port types alongside one valid P1 port
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

        // then only the P1 port survives — P3+ never normalizes, SERVICE is unrecognized
        result.ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonA"]));
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

        result.ShouldBeDictionaryOf(("P2_BUTTON1", ["ButtonA"]));
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
    public void Load_OrChainedJoycode_StoresAllTranslatedInputsInOrder()
    {
        // given a port whose sequence chains two JOYCODEs with "OR" (MAME's alternative-binding syntax)
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON2 OR JOYCODE_1_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then both translated inputs are recorded in source order (renderer marks every physical button)
        result.ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonB", "ButtonA"]));
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
        result.ShouldBeDictionaryOf(("P1_BUTTON3", ["ButtonC"]));
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
    public void Load_StandardNone_IncrementAndDecrementBound_UsesBoth()
    {
        // the adillor/spacwalk shape: a true analogue control (a trackball or paddle axis) with no
        // analogue hardware assigned, driven instead by two ordinary buttons nudging it each way
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>NONE</newseq>
                    <newseq type='increment'>JOYCODE_1_BUTTON2</newseq>
                    <newseq type='decrement'>JOYCODE_1_BUTTON3</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        // then both the increment and decrement buttons are recorded, standard-first order (moot
        // here since standard is unbound) then increment, then decrement
        result.ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonB", "ButtonC"]));
    }

    [Fact]
    public void Load_StandardAndIncrementBothBound_StandardOrderedFirst()
    {
        // a port with real analogue hardware assigned AND a digital fallback for one direction --
        // both are recognized, analogue first since it's the more direct binding
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                    <newseq type='increment'>JOYCODE_1_BUTTON2</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        result.ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonA", "ButtonB"]));
    }

    [Fact]
    public void Load_SameGenericReachableFromStandardAndIncrement_DedupsToOneEntry()
    {
        // increment happens to translate to a generic the standard sequence already reached --
        // shouldn't appear twice
        StubXml("""
            <mameconfig>
              <system name='galaga'>
                <input>
                  <port type='P1_BUTTON1'>
                    <newseq type='standard'>JOYCODE_1_BUTTON1</newseq>
                    <newseq type='increment'>JOYCODE_1_BUTTON1</newseq>
                  </port>
                </input>
              </system>
            </mameconfig>
            """);

        // when the loader runs
        var result = _underTest.Load(CfgPath);

        result.ShouldBeDictionaryOf(("P1_BUTTON1", ["ButtonA"]));
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
        result.ShouldBeDictionaryOf(("P1_BUTTON2", ["ButtonB"]));
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
        result.ShouldBeDictionaryOf(("P1_BUTTON2", ["ButtonB"]));
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
