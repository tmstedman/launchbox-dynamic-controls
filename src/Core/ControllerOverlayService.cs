using DynamicControls.InputMapping;
using DynamicControls.Labels;
using DynamicControls.Rendering;
using DynamicControls.Static;
using DynamicControls.Templates;

namespace DynamicControls;

/// <summary>
/// Resolves the controller overlay for a game launch. Returns a <see cref="ControllerOverlayModel"/>
/// holding the base image path, canvas size, positioned labels, and rendered button images for
/// the launching game. When no default template is configured (or any step fails), an empty
/// model is returned so callers always get a valid output.
/// </summary>
public interface IControllerOverlayService
{
    /// <summary>
    /// Resolves the overlay model for <paramref name="game"/>. Tries a static image first;
    /// otherwise runs the mapping → labels → template → render pipeline.
    /// </summary>
    ControllerOverlayModel Resolve(GameInfo game);
}

/// <summary>
/// Production implementation: orchestrates the five collaborator services. Exceptions raised by
/// any collaborator are caught, logged, and converted to an empty model so the pause screen
/// doesn't show stale data after a misconfigured template.
/// </summary>
public class ControllerOverlayService(
    ILogger logger,
    IInputLabelsService inputLabelsService,
    ITemplateService templateService,
    IStaticImageResolver staticImageResolver,
    IInputMappingService inputMappingService,
    IInputRenderingService inputRenderingService,
    string? defaultTemplate) : IControllerOverlayService
{
    private readonly ILogger _logger = logger;
    private readonly IInputLabelsService _inputLabelsService = inputLabelsService;
    private readonly ITemplateService _templateService = templateService;
    private readonly IStaticImageResolver _staticImageResolver = staticImageResolver;
    private readonly IInputMappingService _inputMappingService = inputMappingService;
    private readonly IInputRenderingService _inputRenderingService = inputRenderingService;
    private readonly string? _defaultTemplate = defaultTemplate;

    /// <inheritdoc />
    public ControllerOverlayModel Resolve(GameInfo game)
    {
        if (_defaultTemplate == null) return new ControllerOverlayModel();

        try
        {
            _logger.Debug($"Platform: {game.Platform}, ROM: {game.RomName}" + (!string.IsNullOrEmpty(game.CloneOf) ? $", CloneOf: {game.CloneOf}" : ""));

            string? staticImage = _staticImageResolver.Find(game);
            if (staticImage != null)
            {
                _logger.Debug($"Using static image: {staticImage}");
                return new ControllerOverlayModel { ImagePath = staticImage };
            }

            ResolvedMapping mapping = _inputMappingService.Load(game);
            ResolvedLabels labels = _inputLabelsService.Load(game, mapping);
            Template template = _templateService.Load(_defaultTemplate);

            RenderResult rendered = _inputRenderingService.Render(template, mapping, labels);

            return new ControllerOverlayModel
            {
                ImagePath = template.BaseImage?.Path,
                CanvasWidth = template.BaseImage?.Width ?? RenderingDefaults.CanvasWidth,
                CanvasHeight = template.BaseImage?.Height ?? RenderingDefaults.CanvasHeight,
                InputLabels = [.. rendered.Labels],
                RenderedImages = [.. rendered.Images.Distinct()]
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"EXCEPTION: {ex.Message}");
            _logger.Error($"STACK TRACE: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                _logger.Error($"INNER EXCEPTION: {ex.InnerException.Message}");
                _logger.Error($"INNER STACK TRACE: {ex.InnerException.StackTrace}");
            }
            return new ControllerOverlayModel();
        }
    }
}
