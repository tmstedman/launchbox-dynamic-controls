using DynamicControls.Config;

namespace DynamicControls.InputMapping;

/// <summary>
/// A transformation applied on top of the source-selected (or platform-default) input mapping.
/// Transforms shuffle which generic input each platform button drives — they do not pick the
/// controller or define their own complete mapping. MAME cfg button swaps are the canonical
/// example: the controller and natural baseline are unchanged, but BUTTON1/BUTTON2 may be
/// swapped onto different platform buttons.
///
/// Transforms are tried in registration order; the first to return a non-null result wins. The
/// baseline passed in is treated as the natural state for the "physically-present button" check
/// performed by the renderer.
/// </summary>
public interface IInputMappingTransform
{
    /// <summary>
    /// Returns true when this transform should participate in the chain for the current
    /// configuration. Evaluated once at composition time — disabled transforms are filtered out
    /// before the service ever sees them.
    /// </summary>
    bool IsEnabled(GlobalConfig config);

    /// <summary>
    /// Returns a new InputMappingConfig produced by transforming <paramref name="baseline"/>, or
    /// null if this transform has nothing to contribute for the given game. Implementations must
    /// preserve <see cref="InputMappingConfig.Controller"/> from the baseline — transforms do not
    /// change which controller is in play.
    /// </summary>
    InputMappingConfig? Transform(GameInfo game, InputMappingConfig baseline);
}
