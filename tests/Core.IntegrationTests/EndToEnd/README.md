# End-to-end tests

Tests that exercise the production pipeline against the real `Fixtures/` tree under the
test output directory — no substitutions, real filesystem, real factories.

Use this tier when:
- You're pinning observable behavior of the full pipeline for a representative game.
- You're catching regressions that only surface when every component is wired against
  real data (e.g. file-path resolution, fixture-format quirks).
- A single test paying the full setup cost is justified by the coverage it provides.

Namespace: `DynamicControls.Core.IntegrationTests.EndToEnd`.
