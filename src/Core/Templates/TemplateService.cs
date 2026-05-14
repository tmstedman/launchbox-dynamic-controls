namespace DynamicControls.Templates;

/// <summary>
/// Loads controller templates by name. Templates are resolved once and reused across launches;
/// callers can request the same template repeatedly without re-parsing. Templates with no
/// matching Layout.xml still return a Template — with an empty layout — so consumers don't
/// need to special-case missing files.
/// </summary>
public interface ITemplateService
{
    /// <summary>
    /// Returns the fully resolved Template for <paramref name="templateName"/>.
    /// </summary>
    /// <param name="templateName">Template folder name under Templates/.</param>
    Template Load(string templateName);
}

/// <summary>
/// Production implementation: coordinates TemplateLoader (XML parsing), LayoutResolver (tree
/// construction and descendants index), and TemplateImageResolver (base image discovery), and
/// caches resolved Templates in a process-wide dictionary. The cache is never invalidated —
/// template-folder edits require restarting LaunchBox.
/// </summary>
public class TemplateService(
    ITemplateLoader loader,
    ITemplateImageResolver imageResolver,
    ILayoutResolver layoutResolver) : ITemplateService
{
    private readonly ITemplateLoader _loader = loader;
    private readonly ITemplateImageResolver _imageResolver = imageResolver;
    private readonly ILayoutResolver _layoutResolver = layoutResolver;
    private readonly Dictionary<string, Template> _cache = [];

    /// <inheritdoc />
    public Template Load(string templateName)
    {
        if (_cache.TryGetValue(templateName, out Template? cached))
            return cached;

        LayoutConfig layoutConfig = _loader.LoadLayout(templateName) ?? new LayoutConfig();
        ITemplateImageSource imageSource = new TemplateImageSource(_imageResolver, templateName);
        ResolvedLayout resolvedLayout = _layoutResolver.Resolve(layoutConfig, imageSource);
        BaseImage? baseImage = _imageResolver.FindBaseImage(templateName);

        var template = new Template(
            Name: templateName,
            BaseImage: baseImage,
            Layout: resolvedLayout,
            ImageSource: imageSource);

        _cache[templateName] = template;
        return template;
    }
}
