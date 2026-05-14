namespace DynamicControls.Templates;

/// <summary>
/// Locates template image files on disk. Owns both base-image discovery (BaseImage.png/jpg in
/// the template folder) and per-image path resolution with platform/controller priority.
/// </summary>
public interface ITemplateImageResolver
{
    /// <summary>
    /// Finds the base controller image for <paramref name="templateName"/> and reads its pixel
    /// dimensions. Returns null when no <c>BaseImage.png</c> exists in the template folder.
    /// </summary>
    BaseImage? FindBaseImage(string templateName);

    /// <summary>
    /// Resolves <paramref name="src"/> to its generic and styled paths under
    /// <paramref name="templateName"/>. The styled path is the most-specific existing file in
    /// Controller → Platform priority. The generic path is the template-local file, or the
    /// shared-root fallback if that doesn't exist; a placeholder template-local path is
    /// returned when neither exists.
    /// </summary>
    /// <param name="templateName">Template folder name under <c>Templates/</c>.</param>
    /// <param name="src">Image filename to resolve (e.g. <c>ButtonA.png</c>).</param>
    /// <param name="platform">Platform name used to locate the styled subfolder. Null skips styled lookup.</param>
    /// <param name="controller">Controller name for the most-specific styled subfolder. Null skips controller-level lookup.</param>
    ResolvedImagePaths ResolveImagePath(string templateName, string src, string? platform, string? controller = null);
}

/// <summary>
/// Production implementation: per-call file-existence checks are cached by
/// <c>(templateName, src, platform, controller)</c>, so each unique probe runs at most once
/// per process.
/// </summary>
public class TemplateImageResolver(ILogger logger, IFileSystem fs, IImageHeader imageHeader, string rootDir) : ITemplateImageResolver
{
    private record ImageCacheKey(string TemplateName, string Src, string? Platform, string? Controller);

    private readonly ILogger _logger = logger;
    private readonly IFileSystem _fs = fs;
    private readonly IImageHeader _imageHeader = imageHeader;
    private readonly string _templatesDir = Path.Combine(rootDir, "Templates");
    private readonly Dictionary<ImageCacheKey, ResolvedImagePaths> _cache = [];

    /// <inheritdoc />
    public BaseImage? FindBaseImage(string templateName)
    {
        string templateDir = Path.Combine(_templatesDir, templateName);
        string imagePng = Path.Combine(templateDir, "BaseImage.png");
        string? path = _fs.FileExists(imagePng) ? imagePng : null;

        if (path == null)
        {
            _logger.Debug("No base image found, using default dimensions 1600x1000");
            return null;
        }

        using Stream stream = _fs.OpenRead(path);
        (int width, int height) = _imageHeader.ReadDimensions(stream);
        _logger.Debug($"Image loaded: {path} ({width}x{height})");
        return new BaseImage(path, width, height);
    }

    /// <inheritdoc />
    public ResolvedImagePaths ResolveImagePath(
        string templateName,
        string src,
        string? platform,
        string? controller = null)
    {
        var key = new ImageCacheKey(templateName, src, platform, controller);
        if (_cache.TryGetValue(key, out ResolvedImagePaths? cached))
            return cached;

        string templateDir = Path.Combine(_templatesDir, templateName);
        string? styledPath = null;

        if (platform != null)
        {
            string safePlatform = platform.SafeFileName();
            if (controller != null)
            {
                string controllerCandidate = Path.Combine(templateDir, safePlatform, controller.SafeFileName(), src);
                if (_fs.FileExists(controllerCandidate)) styledPath = controllerCandidate;
            }
            if (styledPath == null)
            {
                string platformCandidate = Path.Combine(templateDir, safePlatform, src);
                if (_fs.FileExists(platformCandidate)) styledPath = platformCandidate;
            }
        }

        string templateLocalPath = Path.Combine(templateDir, src);
        string sharedFallbackPath = Path.Combine(_templatesDir, src);
        bool templateLocalExists = _fs.FileExists(templateLocalPath);
        bool sharedExists = !templateLocalExists && _fs.FileExists(sharedFallbackPath);

        string genericPath = templateLocalExists ? templateLocalPath
            : sharedExists ? sharedFallbackPath
            : templateLocalPath;

        ResolvedImagePaths result = new(genericPath, styledPath);
        _cache[key] = result;
        return result;
    }
}
