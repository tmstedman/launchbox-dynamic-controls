using DynamicControls.Rendering;
using DynamicControls.Templates;

namespace DynamicControls.Core.Tests.Rendering;

/// <summary>
/// Unit tests for <see cref="VisibilityFlags"/>. Covers every arm of <see cref="VisibilityFlags.IsVisible"/>
/// and all branch combinations of the <c>|</c> operator's short-circuiting <c>||</c> conditions.
/// </summary>
public class VisibilityFlagsTests
{
    // ---- IsVisible ----

    #pragma warning disable format
    [Theory]
    // condition                        hasLabel isMapped gameSpec expected
    [InlineData(ShowIfCondition.Always, false,   false,   false,   true)]  // always visible regardless of flags
    [InlineData(ShowIfCondition.Label,  true,    false,   false,   true)]  // Label: HasLabel=T → visible
    [InlineData(ShowIfCondition.Label,  false,   true,    false,   false)] // Label: HasLabel=F → hidden
    [InlineData(ShowIfCondition.Mapped, false,   true,    false,   true)]  // Mapped: IsMapped=T → visible
    [InlineData(ShowIfCondition.Mapped, true,    false,   false,   false)] // Mapped: IsMapped=F → hidden
    [InlineData(ShowIfCondition.Auto,   true,    false,   true,    true)]  // Auto + gameSpecific: HasLabel=T → visible
    [InlineData(ShowIfCondition.Auto,   false,   true,    true,    false)] // Auto + gameSpecific: HasLabel=F → hidden
    [InlineData(ShowIfCondition.Auto,   false,   true,    false,   true)]  // Auto + !gameSpecific: IsMapped=T → visible
    [InlineData(ShowIfCondition.Auto,   true,    false,   false,   false)] // Auto + !gameSpecific: IsMapped=F → hidden
    #pragma warning restore format
    public void IsVisible_ReturnsExpectedResult(
        ShowIfCondition condition, bool hasLabel, bool isMapped, bool isGameSpecific, bool expected)
    {
        new VisibilityFlags(hasLabel, isMapped)
            .IsVisible(condition, isGameSpecific)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsVisible_UnknownCondition_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() =>
            VisibilityFlags.None.IsVisible((ShowIfCondition)99, isGameSpecific: false));
    }

    // ---- operator | ----

    [Fact]
    public void Or_LeftHasFlags_ShortCircuits()
    {
        // a.HasLabel=T and a.IsMapped=T both short-circuit without evaluating b
        (new VisibilityFlags(true, true) | new VisibilityFlags(false, false))
            .ShouldBe(new VisibilityFlags(true, true));
    }

    [Fact]
    public void Or_OnlyRightHasFlags_PropagatesFromRight()
    {
        // a.HasLabel=F and a.IsMapped=F — b's values are needed and propagate through
        (new VisibilityFlags(false, false) | new VisibilityFlags(true, true))
            .ShouldBe(new VisibilityFlags(true, true));
    }

    [Fact]
    public void Or_BothNone_ReturnsNone()
    {
        // all false — both || conditions take the false branch on both sides
        (VisibilityFlags.None | VisibilityFlags.None).ShouldBe(VisibilityFlags.None);
    }
}
