using DynamicControls;
using DynamicControls.Rendering;
using NSubstitute;
using Unbroken.LaunchBox.Plugins.Data;

namespace DynamicControls.LaunchBox.Tests;

public class OnGameLaunchHandlerTests
{
    private readonly IControllerOverlayService _overlayService = Substitute.For<IControllerOverlayService>();
    private readonly IRetroArchCoreResolver _coreResolver = Substitute.For<IRetroArchCoreResolver>();
    private readonly IDynamicControlsViewModel _viewModel = Substitute.For<IDynamicControlsViewModel>();

    private OnGameLaunchHandler CreateHandler() =>
        new(_overlayService, _coreResolver, _viewModel);

    [Fact]
    public void OnBeforeGameLaunching_NullGame_DoesNothing()
    {
        // given a null game
        IGame? game = null;
        var handler = CreateHandler();

        // when the launch is handled
        handler.OnBeforeGameLaunching(game, Substitute.For<IEmulator>());

        // then no overlay resolution or view-model update occurs
        _overlayService.ReceivedCalls().ShouldBeEmpty();
        _viewModel.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void OnBeforeGameLaunching_NullApplicationPath_DoesNothing()
    {
        // given a game with no application path
        var game = Game(applicationPath: null, platform: "SNES");
        var handler = CreateHandler();

        // when the launch is handled
        handler.OnBeforeGameLaunching(game, Substitute.For<IEmulator>());

        // then no overlay resolution or view-model update occurs
        _overlayService.ReceivedCalls().ShouldBeEmpty();
        _viewModel.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void OnBeforeGameLaunching_EmptyPlatform_DoesNothing()
    {
        // given a game with an empty platform
        var game = Game(applicationPath: @"C:\Roms\smw.sfc", platform: "");
        var handler = CreateHandler();

        // when the launch is handled
        handler.OnBeforeGameLaunching(game, Substitute.For<IEmulator>());

        // then no overlay resolution or view-model update occurs
        _overlayService.ReceivedCalls().ShouldBeEmpty();
        _viewModel.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void OnBeforeGameLaunching_ValidGame_BuildsGameInfoAndUpdatesViewModel()
    {
        // given a valid game, an emulator with a libretro core, and an overlay result
        var game = Game(
            applicationPath: @"C:\Roms\SNES\smw.sfc",
            platform: "SNES",
            cloneOf: "smwclone");
        var emulator = Substitute.For<IEmulator>();
        emulator.ApplicationPath.Returns(@"C:\RetroArch\retroarch.exe");
        _coreResolver.Resolve(emulator, "SNES").Returns("snes9x_libretro");

        var overlay = new ControllerOverlayModel
        {
            ImagePath = @"C:\overlay.png",
            CanvasWidth = 1920,
            CanvasHeight = 1080,
            InputLabels = [],
            RenderedImages = [],
        };
        GameInfo? capturedGameInfo = null;
        _overlayService.Resolve(Arg.Any<GameInfo>())
            .Returns(call => { capturedGameInfo = call.Arg<GameInfo>(); return overlay; });

        var handler = CreateHandler();

        // when the launch is handled
        handler.OnBeforeGameLaunching(game, emulator);

        // then the overlay service is called with a GameInfo built from the game and emulator
        var info = capturedGameInfo.ShouldNotBeNull();
        info.Platform.ShouldBe("SNES");
        info.RomName.ShouldBe("smw");
        info.CloneOf.ShouldBe("smwclone");
        info.EmulatorPath.ShouldBe(@"C:\RetroArch\retroarch.exe");
        info.RomDirectory.ShouldBe(@"C:\Roms\SNES");
        info.RetroArchCore.ShouldBe("snes9x_libretro");

        // and the resolved overlay is copied onto the view model
        _viewModel.Received().ControlsImagePath = overlay.ImagePath;
        _viewModel.Received().CanvasWidth = overlay.CanvasWidth;
        _viewModel.Received().CanvasHeight = overlay.CanvasHeight;
        _viewModel.Received().InputLabels = overlay.InputLabels;
        _viewModel.Received().RenderedImages = overlay.RenderedImages;
    }

    [Fact]
    public void OnBeforeGameLaunching_NullEmulator_PassesNullEmulatorPathAndCore()
    {
        // given a valid game but no emulator
        var game = Game(applicationPath: @"C:\Roms\smw.sfc", platform: "SNES");
        GameInfo? capturedGameInfo = null;
        _overlayService.Resolve(Arg.Any<GameInfo>())
            .Returns(call => { capturedGameInfo = call.Arg<GameInfo>(); return new ControllerOverlayModel(); });

        var handler = CreateHandler();

        // when the launch is handled with a null emulator
        handler.OnBeforeGameLaunching(game, null);

        // then the resolver is invoked with a null emulator and the GameInfo has no emulator path
        _coreResolver.Received(1).Resolve(null, "SNES");
        var info = capturedGameInfo.ShouldNotBeNull();
        info.EmulatorPath.ShouldBeNull();
    }

    private static IGame Game(string? applicationPath, string platform, string? cloneOf = null)
    {
        var game = Substitute.For<IGame>();
        game.ApplicationPath.Returns(applicationPath);
        game.Platform.Returns(platform);
        game.CloneOf.Returns(cloneOf);
        game.LaunchBoxDbId.Returns((int?)null);
        return game;
    }
}
