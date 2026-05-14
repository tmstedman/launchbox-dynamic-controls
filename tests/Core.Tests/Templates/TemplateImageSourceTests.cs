using DynamicControls.Templates;
using NSubstitute;

namespace DynamicControls.Core.Tests.Templates;

/// <summary>
/// Unit tests for <see cref="TemplateImageSource"/>. The class is a thin pass-through that pins a
/// template name onto each <see cref="ITemplateImageResolver.ResolveImagePath"/> call so callers
/// only have to supply src / platform / controller. Tests verify the template name is forwarded
/// and that the resolver's result is returned unchanged.
/// </summary>
public class TemplateImageSourceTests
{
    private readonly ITemplateImageResolver _resolver = Substitute.For<ITemplateImageResolver>();

    [Fact]
    public void Resolve_ForwardsTemplateNameAndAllArguments_ReturnsResolverResult()
    {
        // given a resolver that returns a known ResolvedImagePaths for the expected arguments
        var expected = new ResolvedImagePaths(Generic: "Templates/x/ButtonA.png", Styled: "Templates/x/Sega32X/ButtonA.png");
        _resolver.ResolveImagePath("x", "ButtonA.png", "Sega32X", "Genesis6").Returns(expected);
        ITemplateImageSource underTest = new TemplateImageSource(_resolver, "x");

        // when Resolve is called with the same src/platform/controller
        ResolvedImagePaths result = underTest.Resolve("ButtonA.png", "Sega32X", "Genesis6");

        // then the resolver receives the pinned template name and the result flows through unchanged
        result.ShouldBe(expected);
    }

    [Fact]
    public void Resolve_DefaultsControllerToNull()
    {
        // given a resolver stubbed for the two-argument overload (controller omitted)
        var expected = new ResolvedImagePaths(Generic: "Templates/x/ButtonA.png", Styled: null);
        _resolver.ResolveImagePath("x", "ButtonA.png", "Sega32X", null).Returns(expected);
        ITemplateImageSource underTest = new TemplateImageSource(_resolver, "x");

        // when Resolve is called without specifying controller
        ResolvedImagePaths result = underTest.Resolve("ButtonA.png", "Sega32X");

        // then controller defaults to null and the resolver still hits the expected overload
        result.ShouldBe(expected);
    }
}
