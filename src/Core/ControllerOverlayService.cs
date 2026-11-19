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
    /// otherwise runs the mapping → labels → template → render pipeline. The model is the
    /// game's default overlay, carrying one more in <see cref="ControllerOverlayModel.ControlSets"/>
    /// for each control set its labels declare.
    /// </summary>
    ControllerOverlayModel Resolve(GameInfo game);
}

/// <summary>
/// Production implementation: orchestrates the five collaborator services. Exceptions raised by
/// any collaborator are caught, logged, and converted to an empty model so the pause screen
/// doesn't show stale data after a misconfigured template. One raised while drawing a control
/// set empties only that set.
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
        if (_defaultTemplate == null)
        {
            _logger.Error("No DefaultTemplate configured — nothing will be rendered. Check <DefaultTemplate> in GlobalConfig.xml.");
            return new ControllerOverlayModel();
        }

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

            // A control set changes only what the labels say, so it is drawn with the default's
            // mapping and template rather than resolving either again.
            ControllerOverlayModel Frame(string? setName) => new()
            {
                ImagePath = template.BaseImage?.Path,
                CanvasWidth = template.BaseImage?.Width ?? RenderingDefaults.CanvasWidth,
                CanvasHeight = template.BaseImage?.Height ?? RenderingDefaults.CanvasHeight,
                SetName = setName,
            };

            ControllerOverlayModel Draw(ResolvedLabels drawn)
            {
                RenderResult rendered = _inputRenderingService.Render(template, mapping, drawn);
                ControllerOverlayModel model = Frame(drawn.SetName);
                model.InputLabels = [.. rendered.Labels];
                model.RenderedImages = [.. rendered.Images.Distinct()];
                return model;
            }

            // A set that fails is logged and left empty under its name rather than costing the
            // default or the other sets, and the sets keep their order. So is the default of a
            // game with sets, which the pause screen shows only when no set drew; without sets, a
            // default that fails empties the overlay as any other failure does.
            ControllerOverlayModel DrawOrLeaveEmpty(ResolvedLabels drawn)
            {
                try
                {
                    return Draw(drawn);
                }
                catch (Exception ex)
                {
                    string what = drawn.SetName is { } name ? $"Control set \"{name}\"" : "The default";
                    _logger.Error($"{what} of {game.RomName} could not be drawn; it is left empty");
                    LogException(ex);
                    return Frame(drawn.SetName);
                }
            }

            ControllerOverlayModel overlay = labels.ControlSets.Count == 0 ? Draw(labels) : DrawOrLeaveEmpty(labels);
            overlay.ControlSets = [.. labels.ControlSets.Select(DrawOrLeaveEmpty)];
            return overlay;
        }
        catch (Exception ex)
        {
            LogException(ex);
            return new ControllerOverlayModel();
        }
    }

    private void LogException(Exception ex)
    {
        _logger.Error($"EXCEPTION: {ex.Message}");
        _logger.Error($"STACK TRACE: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            _logger.Error($"INNER EXCEPTION: {ex.InnerException.Message}");
            _logger.Error($"INNER STACK TRACE: {ex.InnerException.StackTrace}");
        }
    }
}
