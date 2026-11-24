using System.Xml.Linq;
using DynamicControls.Core.IntegrationTests.Subsystem;
using DynamicControls.InputMapping;

namespace DynamicControls.Core.IntegrationTests.ShippedData;

/// <summary>
/// Pins what every shipped <c>Controllers.xml</c> must say, read from the real files rather than
/// fixture copies: that each platform loads without an error and names one default, which since
/// the default moved onto the root element no shipped file may leave unstated or misspelt, and
/// that none ships a <c>&lt;Game&gt;</c> entry, which would replace every user's own emulator
/// configuration for that game.
/// </summary>
public class ShippedControllersTests
{
    private static readonly string Root = Path.Combine(AppContext.BaseDirectory, "Shipped");
    private static readonly string PlatformsDir = Path.Combine(Root, "Defaults", "Platforms");

    public static TheoryData<string> Platforms()
    {
        var platforms = new TheoryData<string>();
        foreach (string dir in Directory.GetDirectories(PlatformsDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            if (File.Exists(Path.Combine(dir, "Controllers.xml")))
                platforms.Add(Path.GetFileName(dir));
        }
        return platforms;
    }

    [Theory]
    [MemberData(nameof(Platforms))]
    public void EveryPlatform_LoadsWithoutAnError_AndNamesOneDefault(string platform)
    {
        var logger = new ErrorRecordingLogger();

        PlatformControllersConfig? result =
            new InputMappingLoader(logger, new LayeredFileSystem(Root, new SystemFileSystem())).LoadPlatformMapping(platform);

        logger.Errors.ShouldBeEmpty();
        result.ShouldNotBeNull();
        result.Controllers.Count(c => c.IsDefault).ShouldBe(1);
    }

    [Fact]
    public void NoShippedFile_HasAGameEntry()
    {
        Directory.GetFiles(PlatformsDir, "Controllers.xml", SearchOption.AllDirectories)
            .Where(f => XDocument.Load(f).Root!.Elements("Game").Any())
            .ShouldBeEmpty();
    }
}
