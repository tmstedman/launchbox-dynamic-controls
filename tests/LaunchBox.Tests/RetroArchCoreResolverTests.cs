using NSubstitute;
using Unbroken.LaunchBox.Plugins.Data;

namespace DynamicControls.LaunchBox.Tests;

public class RetroArchCoreResolverTests
{
    private readonly RetroArchCoreResolver _resolver = new();

    [Fact]
    public void Resolve_NullEmulator_ReturnsNull()
    {
        // given a null emulator

        // when resolving the core
        var result = _resolver.Resolve(null, "SNES");

        // then the result is null
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_EmptyCommandLine_ReturnsNull()
    {
        // given an emulator with an empty command line
        var emulator = Emulator("");

        // when resolving the core
        var result = _resolver.Resolve(emulator, "SNES");

        // then the result is null
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_NoLibretroFlag_ReturnsNull()
    {
        // given a command line with no libretro flag
        var emulator = Emulator("retroarch.exe --config myconfig.cfg");

        // when resolving the core
        var result = _resolver.Resolve(emulator, "SNES");

        // then the result is null
        result.ShouldBeNull();
    }

    [Fact]
    public void Resolve_QuotedPath_ReturnsCoreName()
    {
        // given a command line with a quoted libretro path
        var emulator = Emulator(@"-L ""cores\genesis_plus_gx_libretro.dll""");

        // when resolving the core
        var result = _resolver.Resolve(emulator, "SNES");

        // then the core name is extracted
        result.ShouldBe("genesis_plus_gx_libretro");
    }

    [Fact]
    public void Resolve_UnquotedPath_ReturnsCoreName()
    {
        // given a command line with an unquoted libretro path
        var emulator = Emulator("-L cores/genesis_plus_gx_libretro.dll");

        // when resolving the core
        var result = _resolver.Resolve(emulator, "SNES");

        // then the core name is extracted
        result.ShouldBe("genesis_plus_gx_libretro");
    }

    [Fact]
    public void Resolve_LibretroLongFlag_ReturnsCoreName()
    {
        // given a command line using the --libretro long flag
        var emulator = Emulator(@"--libretro ""cores\genesis_plus_gx_libretro.dll""");

        // when resolving the core
        var result = _resolver.Resolve(emulator, "SNES");

        // then the core name is extracted
        result.ShouldBe("genesis_plus_gx_libretro");
    }

    [Fact]
    public void Resolve_FlagCaseInsensitive_ReturnsCoreName()
    {
        // given a command line using a lowercase -l flag
        var emulator = Emulator(@"-l ""cores\genesis_plus_gx_libretro.dll""");

        // when resolving the core
        var result = _resolver.Resolve(emulator, "SNES");

        // then the core name is extracted
        result.ShouldBe("genesis_plus_gx_libretro");
    }

    [Fact]
    public void Resolve_PlatformCommandLineWins()
    {
        // given an emulator with a platform-specific command line for SNES
        var emulator = Emulator(
            globalCommandLine: @"-L ""cores\genesis_plus_gx_libretro.dll""",
            platforms: [Platform("SNES", @"-L ""cores\snes9x_libretro.dll""")]);

        // when resolving the core for SNES
        var result = _resolver.Resolve(emulator, "SNES");

        // then the platform-specific core wins over the global one
        result.ShouldBe("snes9x_libretro");
    }

    [Fact]
    public void Resolve_UnmatchedPlatformFallsBackToGlobal()
    {
        // given an emulator with a platform-specific command line for NES only
        var emulator = Emulator(
            globalCommandLine: @"-L ""cores\genesis_plus_gx_libretro.dll""",
            platforms: [Platform("NES", @"-L ""cores\fceumm_libretro.dll""")]);

        // when resolving the core for SNES
        var result = _resolver.Resolve(emulator, "SNES");

        // then the global command line is used
        result.ShouldBe("genesis_plus_gx_libretro");
    }

    private static IEmulator Emulator(string globalCommandLine, IEmulatorPlatform[]? platforms = null)
    {
        var emulator = Substitute.For<IEmulator>();
        emulator.CommandLine.Returns(globalCommandLine);
        emulator.GetAllEmulatorPlatforms().Returns(platforms ?? []);
        return emulator;
    }

    private static IEmulatorPlatform Platform(string platform, string commandLine)
    {
        var emulatorPlatform = Substitute.For<IEmulatorPlatform>();
        emulatorPlatform.Platform.Returns(platform);
        emulatorPlatform.CommandLine.Returns(commandLine);
        return emulatorPlatform;
    }
}
