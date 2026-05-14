using DynamicControls.InputMapping;
using DynamicControls.Plugins.RetroArch;
using NSubstitute;
using static DynamicControls.Core.TestHelpers.InputMapping.InputMappingFixtures;

namespace DynamicControls.Core.Tests.Plugins.RetroArch;

/// <summary>
/// Unit tests for <see cref="RetroArchSwapApplier"/>. The applier takes a slot→coreId swap dict
/// and an <see cref="InputMappingConfig"/> baseline and produces a new config where each affected
/// slot's source generic is removed from the baseline and the target slot's <c>Name</c> appears at
/// the source generic instead (so the rendering layer can re-label remapped buttons). Both .cfg
/// and .rmp produce dicts in this shape — same applier handles both. The fixture uses Sony face
/// buttons (Cross=South=ButtonA, Circle=East=ButtonB, Triangle=North=ButtonY, Square=West=ButtonX)
/// to keep slot/coreId examples concrete.
/// </summary>
public class RetroArchSwapApplierTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly RetroArchSwapApplier _underTest;

    public RetroArchSwapApplierTests()
    {
        _underTest = new RetroArchSwapApplier(_logger);
    }

    private static InputMappingConfig SonyPad(string? controller = null, AnalogToDigitalMode? analogToDigital = null) =>
        MappingConfig(
            controller: controller,
            analogToDigital: analogToDigital,
            mappings:
            [
                ("Cross", "ButtonA"),    // south face — RetroPad slot "b"
                ("Circle", "ButtonB"),   // east face  — slot "a"
                ("Triangle", "ButtonY"), // north face — slot "x"
                ("Square", "ButtonX"),   // west face  — slot "y"
            ]);

    // ---- early-out and identity ----

    [Fact]
    public void Apply_NoSwaps_ReturnsBaseConfigReferenceUnchanged()
    {
        // given an empty swap dict
        InputMappingConfig baseConfig = SonyPad();

        // when Apply runs
        var result = _underTest.Apply(baseConfig, []);

        // then the same reference is returned — no allocation, no transformation
        result.ShouldBeSameAs(baseConfig);
    }

    [Fact]
    public void Apply_CarriesBaselineControllerAndAnalogToDigital()
    {
        // given a baseline carrying controller selection and A2D mode, plus a no-op swap
        InputMappingConfig baseConfig = SonyPad(controller: "Pad", analogToDigital: AnalogToDigitalMode.Left);

        // when Apply runs with one swap so the early-out doesn't fire
        var result = _underTest.Apply(baseConfig, new Dictionary<string, int> { ["a"] = 0 });

        // then Controller and AnalogToDigital pass through unchanged
        result.Controller.ShouldBe("Pad");
        result.AnalogToDigital.ShouldBe(AnalogToDigitalMode.Left);
    }

    // ---- swap transformations ----

    [Fact]
    public void Apply_TwoWayFaceSwap_MovesBothLabelsToOpposingPositions()
    {
        // given the canonical A↔B remap recorded by RetroArch as both directions:
        //   slot "a" (east, ButtonB) → coreId 0 (south slot "b")
        //   slot "b" (south, ButtonA) → coreId 8 (east slot "a")
        var swaps = new Dictionary<string, int>
        {
            ["a"] = 0,
            ["b"] = 8,
        };

        // when Apply runs
        var result = _underTest.Apply(SonyPad(), swaps);

        // then Cross and Circle swap positions:
        //   Cross (was at ButtonA) now also at ButtonB
        //   Circle (was at ButtonB) now also at ButtonA
        //   Both originals are removed since they are sources of swaps
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Triangle", "ButtonY"), // untouched
            ("Square", "ButtonX"),   // untouched
            ("Cross", "ButtonB"),    // addition for "a" → 0
            ("Circle", "ButtonA"),   // addition for "b" → 8
        ], ignoreOrder: true);
    }

    [Fact]
    public void Apply_ThreeWayCycle_RotatesAllThreeLabels_UsesOriginalSnapshotForTargetLookup()
    {
        // given a three-way rotation: Circle (slot "a"/ButtonB) → ButtonY, Triangle (slot "x"/ButtonY)
        // → ButtonX, Square (slot "y"/ButtonX) → ButtonB — each slot's coreId points at the next
        // slot in the cycle.  If Apply consulted live state instead of the original snapshot, each
        // step would overwrite what the previous one wrote and the result would be wrong.
        var swaps = new Dictionary<string, int>
        {
            ["a"] = 1,  // east  (ButtonB / Circle)   → coreId 1 (west  slot "y" / ButtonX)
            ["y"] = 9,  // west  (ButtonX / Square)   → coreId 9 (north slot "x" / ButtonY)
            ["x"] = 8,  // north (ButtonY / Triangle) → coreId 8 (east  slot "a" / ButtonB)
        };

        // when Apply runs
        var result = _underTest.Apply(SonyPad(), swaps);

        // then all three labels have rotated; Cross (ButtonA) is untouched, and every target lookup
        // used the original base — Square lands at ButtonB, Triangle at ButtonX, Circle at ButtonY
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),    // untouched — not part of the cycle
            ("Square", "ButtonB"),   // Square (was ButtonX) takes Circle's old slot
            ("Triangle", "ButtonX"), // Triangle (was ButtonY) takes Square's old slot
            ("Circle", "ButtonY"),   // Circle (was ButtonB) takes Triangle's old slot
        ], ignoreOrder: true);
    }

    [Fact]
    public void Apply_OneWaySwap_RemovesSourceAndAddsTargetLabelAtSourcePosition()
    {
        // given only one direction of the swap recorded — slot "a" → coreId 0 (south)
        var swaps = new Dictionary<string, int> { ["a"] = 0 };

        // when Apply runs
        var result = _underTest.Apply(SonyPad(), swaps);

        // then ButtonB (source slot's generic) is removed; Cross (target's Name) is added at ButtonB.
        // Cross now appears at both ButtonA (original) and ButtonB (added) — Circle is gone.
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),    // original, not in removes
            ("Triangle", "ButtonY"), // untouched
            ("Square", "ButtonX"),   // untouched
            ("Cross", "ButtonB"),    // added; Circle (was at ButtonB) is gone
        ], ignoreOrder: true);
    }

    [Fact]
    public void Apply_DisabledSlot_RemovesSourceAndAddsNothing()
    {
        // given a swap that disables slot "a" (coreId -1, not present in IdToSlot)
        var swaps = new Dictionary<string, int> { ["a"] = -1 };

        // when Apply runs
        var result = _underTest.Apply(SonyPad(), swaps);

        // then ButtonB (source) is removed and no addition happens — Circle vanishes
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),
            ("Triangle", "ButtonY"),
            ("Square", "ButtonX"),
        ], ignoreOrder: true);
    }

    // ---- robustness ----

    [Fact]
    public void Apply_UnknownSlotName_IsSkippedEntirely()
    {
        // given a swap keyed on a slot name that isn't in RetroPadToGeneric
        var swaps = new Dictionary<string, int> { ["zzz"] = 0 };

        // when Apply runs
        var result = _underTest.Apply(SonyPad(), swaps);

        // then the entire swap entry is ignored — no removes, no additions, base preserved
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),
            ("Circle", "ButtonB"),
            ("Triangle", "ButtonY"),
            ("Square", "ButtonX"),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Apply_UnknownCoreId_RemovesSourceButAddsNothing()
    {
        // given a swap whose coreId doesn't map to any RetroPad slot
        var swaps = new Dictionary<string, int> { ["a"] = 999 };

        // when Apply runs
        var result = _underTest.Apply(SonyPad(), swaps);

        // then ButtonB (source) is removed but no addition can be made — same shape as a disabled
        // slot, since both fail the IdToSlot lookup
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),
            ("Triangle", "ButtonY"),
            ("Square", "ButtonX"),
        ], ignoreOrder: true);
    }

    [Fact]
    public void Apply_SourceSlotGenericNotInBase_NoRemoveButStillAddsAtTarget()
    {
        // given a baseline that lacks the source slot's generic (e.g. l2 = AxisTriggerLeft) but
        // does have the target slot's generic
        InputMappingConfig baseConfig = MappingConfig(mappings:
        [
            ("Cross", "ButtonA"), // target generic for slot "b" / coreId 0
        ]);

        // l2 → 0 means "trigger now selects south's coreId"
        var swaps = new Dictionary<string, int> { ["l2"] = 0 };

        // when Apply runs
        var result = _underTest.Apply(baseConfig, swaps);

        // then no remove happens (base has no AxisTriggerLeft to remove) but Cross is still
        // added at AxisTriggerLeft — the addition path uses the original base for the target name
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),         // untouched
            ("Cross", "AxisTriggerLeft"), // added: target's Name at source's generic
        ], ignoreOrder: true);
    }

    [Fact]
    public void Apply_TargetGenericNotInBase_RemovesSourceButAddsNothing()
    {
        // given a baseline lacking the target slot's generic — there is no targetEntry, so no
        // Name to attach to the addition
        InputMappingConfig baseConfig = MappingConfig(mappings:
        [
            ("Circle", "ButtonB"), // source generic for slot "a"
        ]);

        // "a" → 0 wants to move Cross's name to ButtonB, but baseline has no Cross at ButtonA
        var swaps = new Dictionary<string, int> { ["a"] = 0 };

        // when Apply runs
        var result = _underTest.Apply(baseConfig, swaps);

        // then ButtonB is removed (source) but no addition is made; Circle vanishes
        result.Mappings.ShouldBeEmpty();
    }

    [Fact]
    public void Apply_MultipleEntriesWithSameInputInBase_FirstWinsForTargetLookup()
    {
        // given a baseline where two Names share the same generic Input — TryAdd keeps the first
        // entry encountered when building the originalByGeneric snapshot
        InputMappingConfig baseConfig = MappingConfig(mappings:
        [
            ("First", "ButtonA"),
            ("Second", "ButtonA"),    // same Input as above — ignored for target lookup
            ("Circle", "ButtonB"),
        ]);

        // "a" → 0 looks up the target's name at ButtonA — should pick "First"
        var swaps = new Dictionary<string, int> { ["a"] = 0 };

        // when Apply runs
        var result = _underTest.Apply(baseConfig, swaps);

        // then the addition uses "First" (the first-seen name at ButtonA), not "Second"
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldContain(("First", "ButtonB"));
        result.Mappings.Select(m => (m.Name, m.Input)).ShouldNotContain(("Second", "ButtonB"));
    }

    [Fact]
    public void Apply_DoesNotMutateBaseConfig()
    {
        // given a baseline reference
        InputMappingConfig baseConfig = SonyPad();
        int originalCount = baseConfig.Mappings.Count;

        // when Apply runs with a real swap
        _underTest.Apply(baseConfig, new Dictionary<string, int> { ["a"] = 0 });

        // then the baseline's Mappings list is untouched — Apply returns a new config
        baseConfig.Mappings.Count.ShouldBe(originalCount);
        baseConfig.Mappings.Select(m => (m.Name, m.Input)).ShouldBe(
        [
            ("Cross", "ButtonA"),
            ("Circle", "ButtonB"),
            ("Triangle", "ButtonY"),
            ("Square", "ButtonX"),
        ]);
    }
}
