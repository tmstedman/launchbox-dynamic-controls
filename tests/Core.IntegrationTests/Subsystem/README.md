# Subsystem tests

Tests that verify a subsystem's functionality with its real internal wiring intact —
LayoutFilter + VisibilityEvaluator + the renderers behaving as one unit, the mapping pipeline
composing as one unit, and so on. Because the slice is smaller than an end-to-end test, each
suite can cover a wider range of scenarios — visibility-flag edge cases, classifier branches,
collapse interactions — at real-wiring fidelity.

I/O is mocked so the test data lives inline alongside the assertion. A developer reading the
test should see the inputs (template structure, mapping, labels) and the expected output in
the same file, without chasing fixtures across multiple folders. The substitution is a
readability choice; the verification is the point.

Use this tier when:
- A subsystem has enough internal interaction that mocking every collaborator in a unit test
  drains the signal — you want the real `LayoutFilter` choosing alternatives, the real
  `VisibilityEvaluator` flowing into the real `InputImageRenderer`, etc.
- You need to cover a scenario range that an end-to-end test can't reach economically, because
  each E2E case requires a new on-disk fixture set.
- You want focused diagnostics — a failure should point at the wiring shape, not at a pixel
  that doesn't match.

Namespace: `DynamicControls.Core.IntegrationTests.Subsystem`.
