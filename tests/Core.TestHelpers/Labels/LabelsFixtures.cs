using DynamicControls.Labels;

namespace DynamicControls.Core.TestHelpers.Labels;

/// <summary>
/// Factory helpers for <see cref="ResolvedLabels"/>. Use via
/// <c>using static DynamicControls.Core.TestHelpers.Labels.LabelsFixtures;</c>.
/// </summary>
public static class LabelsFixtures
{
    public static ResolvedLabels LabelsOf(bool isGameSpecific = false, params (string Input, string Text)[] entries)
    {
        return new(
            LabelText: entries.ToDictionary(e => e.Input, e => e.Text),
            IsGameSpecific: isGameSpecific);
    }

    public static ResolvedLabels EmptyLabels() =>
        new(LabelText: new Dictionary<string, string>());
}
