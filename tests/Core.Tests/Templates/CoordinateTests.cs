using DynamicControls.Templates;
using FsCheck;
using FsCheck.Xunit;

namespace DynamicControls.Core.Tests.Templates;

public class CoordinateTests
{
    // NormalFloat excludes NaN and Infinity, which would break == comparisons on doubles.

    [Property]
    public bool Absolute_IsNotRelativeAndPreservesValue(NormalFloat v)
    {
        var c = Coordinate.Absolute(v.Get);
        return !c.IsRelative && c.Value == v.Get;
    }

    [Property]
    public bool Relative_IsRelativeAndPreservesValue(NormalFloat v)
    {
        var c = Coordinate.Relative(v.Get);
        return c.IsRelative && c.Value == v.Get;
    }

    [Property]
    public bool Resolve_Absolute_IgnoresOrigin(NormalFloat v, NormalFloat origin) =>
        Coordinate.Absolute(v.Get).Resolve(origin.Get) == v.Get;

    [Property]
    public bool Resolve_Relative_AddsToOrigin(NormalFloat v, NormalFloat origin) =>
        Coordinate.Relative(v.Get).Resolve(origin.Get) == origin.Get + v.Get;
}
