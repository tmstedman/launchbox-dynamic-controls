namespace DynamicControls.Templates;

/// <summary>
/// ITemplateImageSource implementation scoped to a specific template. Wraps TemplateImageResolver
/// with the template name fixed, so callers only supply src, platform, and controller.
/// Created by TemplateService at build time and carried on Template.Images.
/// </summary>
internal sealed class TemplateImageSource : ITemplateImageSource
{
    private readonly ITemplateImageResolver _resolver;
    private readonly string _templateName;

    internal TemplateImageSource(ITemplateImageResolver resolver, string templateName)
    {
        _resolver = resolver;
        _templateName = templateName;
    }

    public ResolvedImagePaths Resolve(string src, string? platform, string? controller = null) =>
        _resolver.ResolveImagePath(_templateName, src, platform, controller);
}
