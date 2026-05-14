namespace DynamicControls.Static;

/// <summary>
/// Looks up pre-rendered controller images stored under the <c>Static/</c> directory. Used by
/// <see cref="ControllerOverlayService"/> to short-circuit the rendering pipeline when a
/// game-specific overlay image already exists on disk.
/// </summary>
public interface IStaticImageResolver
{
    /// <summary>
    /// Probes <c>Static/{platform}/{romName}.jpg</c> then <c>.png</c> and returns the full path
    /// to whichever exists. Returns null when neither file is present.
    /// </summary>
    string? Find(GameInfo game);
}

/// <summary>
/// Production implementation: filesystem probes run lazily on each call (no caching); the
/// platform segment is sanitized via <c>SafeFileName</c> before being joined into the path.
/// </summary>
public class StaticImageResolver(ILogger logger, LayeredFileSystem lfs) : IStaticImageResolver
{
    private readonly ILogger _logger = logger;
    private readonly LayeredFileSystem _lfs = lfs;
    private readonly string _staticDir = Path.Combine(lfs.UserDir, "Static");

    /// <inheritdoc />
    public string? Find(GameInfo game)
    {
        string safePlatform = game.Platform.SafeFileName();

        string[] candidates =
        [
            Path.Combine(_staticDir, safePlatform, game.RomName + ".png"),
            Path.Combine(_staticDir, safePlatform, game.RomName + ".jpg")
        ];

        string? found = candidates.FirstOrDefault(_lfs.FileExists);
        if (found != null)
            _logger.Debug($"Static image found: {found}");
        else
            _logger.Debug($"No static image found for {game.Platform}/{game.RomName}");
        return found;
    }
}
