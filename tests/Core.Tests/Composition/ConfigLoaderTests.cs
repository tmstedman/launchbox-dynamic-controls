using System.Text;
using DynamicControls.Composition;
using DynamicControls.Config;
using NSubstitute;

namespace DynamicControls.Core.Tests.Composition;

public class ConfigLoaderTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IFileSystem _fs = TestFs.Create();
    private const string RootDir = @"C:\plugin";
    private static readonly string DefaultsPath = Path.Combine(RootDir, "Defaults", "GlobalConfig.xml");
    private static readonly string UserPath = Path.Combine(RootDir, "User", "GlobalConfig.xml");

    private LayeredFileSystem Lfs => new(RootDir, _fs);

    [Fact]
    public void Load_DefaultsFileMissing_ReturnsDefaultConfig()
    {
        // given neither config file exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then a default GlobalConfig is returned and OpenRead is never called
        config.ShouldNotBeNull();
        config.DefaultTemplate.ShouldBeNull();
        config.Debug.ShouldBeFalse();
        config.EnableMame.ShouldBeFalse();
        config.EnableRetroArch.ShouldBeFalse();
        _fs.DidNotReceive().OpenRead(Arg.Any<string>());
    }

    [Fact]
    public void Load_ValidDefaultsXml_DeserializesAllFields()
    {
        // given a defaults file with all fields populated
        SetupDefaultsFile("""
            <Config>
              <DefaultTemplate>xbox</DefaultTemplate>
              <Debug>true</Debug>
              <EnableMame>true</EnableMame>
              <EnableRetroArch>true</EnableRetroArch>
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then every field is deserialized from the defaults XML
        config.DefaultTemplate.ShouldBe("xbox");
        config.Debug.ShouldBeTrue();
        config.EnableMame.ShouldBeTrue();
        config.EnableRetroArch.ShouldBeTrue();
    }

    [Fact]
    public void Load_DefaultsMissingFields_UsesClrDefaultsForOmittedValues()
    {
        // given a defaults file that only sets DefaultTemplate
        SetupDefaultsFile("""
            <Config>
              <DefaultTemplate>ps4</DefaultTemplate>
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then the set field is read and the others fall back to their CLR defaults
        config.DefaultTemplate.ShouldBe("ps4");
        config.Debug.ShouldBeFalse();
        config.EnableMame.ShouldBeFalse();
        config.EnableRetroArch.ShouldBeFalse();
    }

    [Fact]
    public void Load_UnknownElement_LogsError()
    {
        // given a config file with an element not present on GlobalConfig
        SetupDefaultsFile("""
            <Config>
              <DefaultTemplate>xbox</DefaultTemplate>
              <Bogus>ignored</Bogus>ß
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then the known field is still read and the unknown element is logged
        config.DefaultTemplate.ShouldBe("xbox");
        _logger.Received(1).Error(Arg.Is<string>(m => m.Contains("Bogus")));
    }

    [Fact]
    public void Load_BuildsPathsFromRootDir()
    {
        // given neither file exists
        _fs.FileExists(Arg.Any<string>()).Returns(false);

        // when loading the config
        ConfigLoader.Load(Lfs, _logger);

        // then FileExists is queried at both expected paths
        _fs.Received().FileExists(DefaultsPath);
        _fs.Received().FileExists(UserPath);
    }

    // ---- merge behaviour ----

    [Fact]
    public void Load_UserFileNotPresent_ReturnsDefaultsOnly()
    {
        // given a full defaults file and no user file
        SetupDefaultsFile("""
            <Config>
              <DefaultTemplate>xbox</DefaultTemplate>
              <EnableRetroArch>true</EnableRetroArch>
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then defaults are returned unchanged
        config.DefaultTemplate.ShouldBe("xbox");
        config.EnableRetroArch.ShouldBeTrue();
    }

    [Fact]
    public void Load_UserOverridesOneField_LeavesOtherFieldsFromDefaults()
    {
        // given defaults with EnableRetroArch=true, and user file that only sets DefaultTemplate
        SetupDefaultsFile("""
            <Config>
              <DefaultTemplate>xbox</DefaultTemplate>
              <EnableRetroArch>true</EnableRetroArch>
            </Config>
            """);
        SetupUserFile("""
            <Config>
              <DefaultTemplate>ps4</DefaultTemplate>
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then the user-specified field overrides the default, and the other default is preserved
        config.DefaultTemplate.ShouldBe("ps4");
        config.EnableRetroArch.ShouldBeTrue();
    }

    [Fact]
    public void Load_UserOverridesAllFields()
    {
        // given defaults with all fields set
        SetupDefaultsFile("""
            <Config>
              <DefaultTemplate>xbox</DefaultTemplate>
              <Debug>false</Debug>
              <EnableMame>false</EnableMame>
              <EnableRetroArch>false</EnableRetroArch>
            </Config>
            """);
        SetupUserFile("""
            <Config>
              <DefaultTemplate>ps4</DefaultTemplate>
              <Debug>true</Debug>
              <EnableMame>true</EnableMame>
              <EnableRetroArch>true</EnableRetroArch>
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then all user fields override the defaults
        config.DefaultTemplate.ShouldBe("ps4");
        config.Debug.ShouldBeTrue();
        config.EnableMame.ShouldBeTrue();
        config.EnableRetroArch.ShouldBeTrue();
    }

    [Fact]
    public void Load_UserAbsentBoolField_DoesNotForceFalseOverTrue()
    {
        // given defaults with EnableRetroArch=true, user file that omits it
        SetupDefaultsFile("""
            <Config>
              <EnableRetroArch>true</EnableRetroArch>
            </Config>
            """);
        SetupUserFile("""
            <Config>
              <DefaultTemplate>custom</DefaultTemplate>
            </Config>
            """);

        // when loading the config
        GlobalConfig config = ConfigLoader.Load(Lfs, _logger);

        // then the absent bool field is NOT overridden to false
        config.EnableRetroArch.ShouldBeTrue();
        config.DefaultTemplate.ShouldBe("custom");
    }

    private void SetupDefaultsFile(string xml)
    {
        _fs.FileExists(DefaultsPath).Returns(true);
        _fs.OpenRead(DefaultsPath).Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }

    private void SetupUserFile(string xml)
    {
        _fs.FileExists(UserPath).Returns(true);
        _fs.OpenRead(UserPath).Returns(_ => new MemoryStream(Encoding.UTF8.GetBytes(xml)));
    }
}
