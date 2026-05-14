using DynamicControls.InputMapping;
using FsCheck;
using FsCheck.Xunit;
using static DynamicControls.InputMapping.AnalogToDigitalMirror;
using static DynamicControls.InputMapping.AnalogToDigitalMode;

namespace DynamicControls.Core.Tests.InputMapping;

public class AnalogToDigitalMirrorTests
{
    [Fact]
    public void Mirror_EmptyMap_DoesNothing()
    {
        // given an empty button-to-input map
        var map = new Dictionary<string, List<string>>();

        // when mirroring
        Mirror(map, Left);

        // then the map remains empty
        map.ShouldBeEmpty();
    }

    [Fact]
    public void Mirror_NoDpadInputs_LeavesListsUnchanged()
    {
        // given a map containing only non-dpad inputs
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformA"] = ["ButtonA", "ButtonB"],
            ["PlatformX"] = ["ButtonStart"],
        };

        // when mirroring
        Mirror(map, Left);

        // then no entries are added to any list
        map["PlatformA"].ShouldBe(["ButtonA", "ButtonB"]);
        map["PlatformX"].ShouldBe(["ButtonStart"]);
    }

    [Theory]
    [InlineData(Left, "ButtonDpad", "AxisLeftStick")]
    [InlineData(Left, "ButtonDpadUp", "AxisLeftStickUp")]
    [InlineData(Left, "ButtonDpadDown", "AxisLeftStickDown")]
    [InlineData(Left, "ButtonDpadLeft", "AxisLeftStickLeft")]
    [InlineData(Left, "ButtonDpadRight", "AxisLeftStickRight")]
    [InlineData(Right, "ButtonDpad", "AxisRightStick")]
    [InlineData(Right, "ButtonDpadUp", "AxisRightStickUp")]
    [InlineData(Right, "ButtonDpadDown", "AxisRightStickDown")]
    [InlineData(Right, "ButtonDpadLeft", "AxisRightStickLeft")]
    [InlineData(Right, "ButtonDpadRight", "AxisRightStickRight")]
    public void Mirror_DpadInput_AppendsMatchingStickGeneric(
        AnalogToDigitalMode mode,
        string dpadGeneric,
        string expectedStickGeneric)
    {
        // given a list containing a single Dpad generic
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformBtn"] = [dpadGeneric],
        };

        // when mirroring in the given mode
        Mirror(map, mode);

        // then the matching stick generic is appended after the dpad generic
        map["PlatformBtn"].ShouldBe([dpadGeneric, expectedStickGeneric]);
    }

    [Fact]
    public void Mirror_DpadAndNonDpadInSameList_OnlyDpadIsMirrored()
    {
        // given a list mixing a dpad input and an unrelated button
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformBtn"] = ["ButtonA", "ButtonDpadUp"],
        };

        // when mirroring
        Mirror(map, Left);

        // then only the dpad input gets its stick equivalent appended; ButtonA is left alone
        map["PlatformBtn"].ShouldBe(["ButtonA", "ButtonDpadUp", "AxisLeftStickUp"]);
    }

    [Fact]
    public void Mirror_StickAlreadyPresent_NotDuplicated()
    {
        // given a list already containing the stick generic the dpad would mirror to
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformBtn"] = ["ButtonDpadUp", "AxisLeftStickUp"],
        };

        // when mirroring
        Mirror(map, Left);

        // then the existing stick entry is not duplicated
        map["PlatformBtn"].ShouldBe(["ButtonDpadUp", "AxisLeftStickUp"]);
    }

    [Theory]
    [InlineData(Left, "AxisRightStickUp", "AxisLeftStickUp")]
    [InlineData(Right, "AxisLeftStickUp", "AxisRightStickUp")]
    public void Mirror_DoesNotProduceOppositeStickEntries(
        AnalogToDigitalMode mode, string oppositeStick, string expectedAppended)
    {
        // given a dpad input alongside a pre-existing opposite-stick entry
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformBtn"] = ["ButtonDpadUp", oppositeStick],
        };

        // when mirroring in the given mode
        Mirror(map, mode);

        // then only the matching-stick equivalent is appended; the opposite-stick entry is left untouched
        map["PlatformBtn"].ShouldBe(["ButtonDpadUp", oppositeStick, expectedAppended]);
    }

    [Fact]
    public void Mirror_IsIdempotent()
    {
        // given a list with a dpad input
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformBtn"] = ["ButtonDpadLeft"],
        };

        // when mirroring twice
        Mirror(map, Right);
        Mirror(map, Right);

        // then the second pass adds nothing further
        map["PlatformBtn"].ShouldBe(["ButtonDpadLeft", "AxisRightStickLeft"]);
    }

    [Fact]
    public void Mirror_MultipleButtons_EachListMirroredIndependently()
    {
        // given a map with multiple platform buttons containing dpad inputs
        var map = new Dictionary<string, List<string>>
        {
            ["PlatformBtn1"] = ["ButtonDpadUp"],
            ["PlatformBtn2"] = ["ButtonDpadDown"],
            ["PlatformBtn3"] = ["ButtonA"],
        };

        // when mirroring
        Mirror(map, Left);

        // then each dpad-containing list is mirrored independently and unrelated lists are untouched
        map["PlatformBtn1"].ShouldBe(["ButtonDpadUp", "AxisLeftStickUp"]);
        map["PlatformBtn2"].ShouldBe(["ButtonDpadDown", "AxisLeftStickDown"]);
        map["PlatformBtn3"].ShouldBe(["ButtonA"]);
    }

    // ---- Default branch (unknown AnalogToDigitalMode value) ----

    [Fact]
    public void Mirror_UnknownMode_Throws()
    {
        // given an AnalogToDigitalMode value outside the defined cases (e.g. a future addition
        // or an invalid cast) — the switch default guards against silent misbehaviour
        var map = new Dictionary<string, List<string>>();

        // when Mirror is called with the unknown mode
        // then an ArgumentOutOfRangeException is thrown
        Should.Throw<ArgumentOutOfRangeException>(() => Mirror(map, (AnalogToDigitalMode)99));
    }

    // Properties — hold for arbitrary input lists:

    // Applying Mirror twice produces the same result as applying it once.
    [Property]
    public bool Mirror_AppliedTwiceEqualsAppliedOnce(NonNull<string>[] generics, bool useLeft)
    {
        var input = generics.Select(g => g.Get).ToList();
        var map1 = new Dictionary<string, List<string>> { ["btn"] = [.. input] };
        var map2 = new Dictionary<string, List<string>> { ["btn"] = [.. input] };
        var mode = useLeft ? Left : Right;
        Mirror(map1, mode);
        Mirror(map2, mode);
        Mirror(map2, mode);
        return map1["btn"].SequenceEqual(map2["btn"]);
    }

    // Mirror never introduces duplicates into a list that started without any.
    [Property]
    public bool Mirror_NeverIntroducesDuplicates(NonNull<string>[] generics, bool useLeft)
    {
        var input = generics.Select(g => g.Get).Distinct().ToList();
        var map = new Dictionary<string, List<string>> { ["btn"] = input };
        Mirror(map, useLeft ? Left : Right);
        return map["btn"].Count == map["btn"].Distinct().Count();
    }
}
