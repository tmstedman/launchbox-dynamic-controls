using System.Xml.Linq;
using DynamicControls.Plugins.Mame;

namespace DynamicControls.Core.IntegrationTests.ShippedData;

/// <summary>
/// Pins what the shipped <c>JoycodeMapping.xml</c> declares about axes, read from the real file
/// rather than a fixture copy: which switches are an axis's two ends, and which codes are halves
/// a nudge can borrow. Both used to follow from joycode names in code, so these are the cases
/// that moving them into data must not change. No pipeline runs here, which is why this is not
/// an end-to-end test: the file itself is what is under test.
/// </summary>
public class ShippedJoycodeMappingTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Shipped");
    private static readonly string FilePath = Path.Combine(Root, "Defaults", "Emulators", "MAME", "JoycodeMapping.xml");

    private static JoycodeMapping Load() =>
        new JoycodeMappingLoader(new ThrowingLogger(), new LayeredFileSystem(Root, new SystemFileSystem())).Load();

    private static IEnumerable<XElement> Mappings() => XDocument.Load(FilePath).Root!.Elements("Mapping");

    [Fact]
    public void EveryStickSwitch_IsOneEndOfItsAxis_AndNothingElseIs()
    {
        JoycodeMapping mapping = Load();
        string[] axes = ["XAXIS", "YAXIS", "RXAXIS", "RYAXIS", "ZAXIS", "RZAXIS"];
        Dictionary<string, (string Dec, string Inc)> ends = new()
        {
            ["XAXIS"] = ("LEFT", "RIGHT"),
            ["YAXIS"] = ("UP", "DOWN"),
            ["RXAXIS"] = ("NEG", "POS"),
            ["RYAXIS"] = ("NEG", "POS"),
            ["ZAXIS"] = ("NEG", "POS"),
            ["RZAXIS"] = ("NEG", "POS"),
        };

        foreach (string axis in axes)
        {
            mapping.EndOf($"JOYCODE_1_{axis}_{ends[axis].Dec}_SWITCH").ShouldBe(new AxisEnd($"JOYCODE_1_{axis}", "dec"));
            mapping.EndOf($"JOYCODE_1_{axis}_{ends[axis].Inc}_SWITCH").ShouldBe(new AxisEnd($"JOYCODE_1_{axis}", "inc"));
        }

        // nothing else is an end: hats and non-switch directions never paired, and a slider switch
        // no longer pairs with a POS_SWITCH this file doesn't declare (the old name rule did)
        Mappings().Select(m => (string)m.Attribute("joycode")!).Distinct()
            .Count(code => mapping.EndOf(code) != null).ShouldBe(axes.Length * 2);
    }

    [Fact]
    public void OnlyBareAndReversedAxes_HaveHalves()
    {
        // a switch is one end of an axis, not a half a nudge can borrow off the port's binding
        string[] withHalves = [.. Mappings().Where(m => m.Attribute("half") != null)
            .Select(m => (string)m.Attribute("joycode")!).Distinct().OrderBy(c => c)];

        string[] axes = ["XAXIS", "YAXIS", "RXAXIS", "RYAXIS", "ZAXIS", "RZAXIS"];
        withHalves.ShouldBe(axes.SelectMany(a => new[] { $"JOYCODE_1_{a}", $"JOYCODE_1_{a}_REVERSE" }).OrderBy(c => c));
    }

    /// <summary>Fails the test on any error the shipped file makes the loader log.</summary>
    private sealed class ThrowingLogger : ILogger
    {
        public bool IsDebugEnabled { get; set; }
        public void ClearLog() { }
        public void Debug(string message) { }
        public void Info(string message) { }
        public void Error(string message) => throw new InvalidOperationException($"Shipped JoycodeMapping.xml logged an error: {message}");
    }
}
