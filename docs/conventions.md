# Code Conventions

Short rules to keep contributions consistent. The pattern matters more than the prose, so each section ends with a one-liner that summarises the rule.

## Type shapes: raw config vs. resolved domain

The codebase walks data through three layers, each with its own conventions.

### 1. Raw config DTOs

Types that exist as XML deserialisation targets. They mirror the on-disk schema field-for-field.

- Mutable: `public T Property { get; set; }`
- Collection fields use mutable `List<T>` / `Dictionary<K, V>` with default empty initialisers
- Naming suffix: `Config` or `Node` (e.g. `LayoutConfig`, `InputNode`, `LabelEntry`)
- One-call-site loaders own them — they're populated and never mutated again
- Default values use field initialisers (e.g. `= new()`) because deserialisers need a target to populate

Examples: `LayoutConfig`, `InputLabelsConfig`, `InputMappingConfig`, every `*Node` type in `Templates/LayoutConfig.cs`.

These look "dated" by modern .NET standards. That's intentional — the deserialiser needs setters and no-arg constructors. Don't fight this layer.

> **Rule**: XML/disk DTOs are mutable, suffix `Config` or `Node`, and never escape the loader that produces them.

### 2. Resolved domain types

The in-memory model the rest of the codebase reads from. Always built once, by a service that takes raw configs as input, and never mutated afterward.

- Immutable: positional records, with `IReadOnlyList<T>` / `IReadOnlyDictionary<K, V>` collections
- Naming prefix `Resolved` or descriptive noun (e.g. `ResolvedLayout`, `ResolvedMapping`, `ResolvedLabels`, `Template`)
- Use `with` expressions to derive modified copies, never property setters
- Derived/index fields (lookups computed from other fields) live as separate dictionary fields on the same record, populated by the builder — don't expose them as methods on the record itself

Examples: `Template`, `ResolvedLayout`, `ResolvedLabels`, the `ILayoutElement` hierarchy (`InputDefinition`, `InputGroup`, `OneOf`).

```csharp
// Good: positional record, read-only collections
public record ResolvedLabels(
    IReadOnlyDictionary<string, string> LabelText,
    bool IsGameSpecific = false);

// Construction site builds locally, then constructs the immutable value
var labelText = new Dictionary<string, string>();
foreach (...) labelText[name] = value;
return new ResolvedLabels(LabelText: labelText);

// Derivation uses `with`, never mutation
return TranslateToGeneric(...) with { IsGameSpecific = true };
```

> **Rule**: Resolved domain types are positional records, read-only collections, no setters. Use `with` to derive modified copies.

### 3. Builders and services

The functions that turn raw configs into resolved domain types.

- Build locally with mutable `List<T>` / `Dictionary<,>` — assign to the immutable target at the end
- A single `Build` or `Configure` method per resolved type. No multi-step "now sync the derived field" dance.
- Compute derived indexes during the build, not on-demand after
- Inject collaborator builders via DI rather than calling static helpers — keeps tests targetable

Example: `LayoutResolver.Resolve` builds a `List<ILayoutElement>` locally, computes the descendants index via an injected `IInputDescendantsBuilder`, and returns one `ResolvedLayout` with everything settled.

> **Rule**: builders work locally with mutable collections, hand back one immutable result. No "rebuild" / "sync" pattern.

## Test conventions

### Project structure

- `tests/Core.Tests/` — unit tests, no fixture files, no I/O against disk
- `tests/Core.IntegrationTests/` — integration tests above the unit level: subsystem tests (verify one subsystem with its real internal wiring; I/O mocked so test data lives inline) and end-to-end tests (real templates, real Fixtures/ tree, full production pipeline)

A test exercising one class with mocked collaborators belongs in `Core.Tests`. A test wiring several production classes together — whether with the filesystem substituted or with the real fixture tree — belongs in `Core.IntegrationTests`. Coverage on the two projects is reported independently so the unit signal stays separable from the integration signal.

### Naming

- One test class per production class: `ClassUnderTestTests.cs`
- Test method names: `Method_Scenario_ExpectedBehavior` (e.g. `Load_FileMissing_ReturnsNull`)
- Use `given` / `when` / `then` comments to mark sections within a test, without arrange/act/assert ceremony

### Helpers

- Factory helpers for constructing domain types live under `TestHelpers/` and are `using static` imported to keep call sites short
- One-test-class helpers stay private static methods inside that class
- Anything used by ≥ 2 test classes is promoted to `TestHelpers/`

## File and namespace layout

Folder names track namespace names. The single exception is the test-project root (`tests/Core.Tests/` maps to `DynamicControls.Core.Tests` rather than `Core.Tests`).

Each Core sub-namespace gets its own folder:

```
src/Core/Templates/   → DynamicControls.Templates
src/Core/Labels/      → DynamicControls.Labels
src/Core/Rendering/   → DynamicControls.Rendering
src/Core/InputMapping/ → DynamicControls.InputMapping
```

A type's file name matches its primary type. Multiple records in one file are fine when they're sub-types of that primary type (see `FilteredLayout.cs`).

## Records: positional vs. init-property

Default to positional records (primary constructor) for resolved domain types. Use init-property records (`{ get; init; }`) only when:

- The record has more than ~6 fields and most have sensible defaults — positional gets noisy
- A subset of fields is genuinely required vs. optional and you want callers to use object-initializer syntax for clarity

The codebase leans positional everywhere except a few cases like the deeper `LayoutElement` subtypes (`InputImageDefinition`, `OverlayDefinition`) where 8-9 fields with mixed defaults work better in positional form too — see those for the pattern when you do reach for positional with defaulted parameters.

## Nullability

Nullable reference types are enabled. A null-tolerant field declares `string?` explicitly. Don't use `= null!` as a placeholder for "I'll set this later" — that's a mutable-state escape hatch. Either make the field nullable for real or have the constructor take it.

The exception is raw config DTOs (layer 1 above) where `string Name { get; set; } = null!;` is the standard deserialisation pattern — the deserialiser will populate it before consumers see it.

## Logging and error reporting

- Inject `ILogger` rather than calling a static logger
- Log levels: `Debug` for routine flow, `Error` for recoverable parse problems (skip the bad element, log it, continue), exceptions only for genuinely exceptional situations the caller should handle
- Error messages should name the element and the file it came from (e.g. `"Skipping <Input>: missing 'name' attribute"`) — they appear in users' log files without surrounding context

## Tests cover production code, not the other way round

Don't change a production type's API just to make it easier to test. If a test wants to verify internal state, add the test helper externally (extension methods on the resolved type, or test-only `InternalsVisibleTo` for the test project). Production types stay focused on what callers need.

## Keeping documentation in step

Most of this project's documentation drift has had one cause: a change landed in the code and in
*one* doc, and the other places describing the same thing were never swept. Every instance found so
far was this — the labels consolidation updated the README but not `docs/architecture.md` or
`docs/config-layering.md`; replacing `System.IO.Abstractions` updated the README but not `CLAUDE.md` or
the CI comments; the `<Unmap>` element was described only in the shipped `assets/` guide.

The cause is fan-out, not carelessness. A single concept is pitched at up to five audiences — users
(`README.md`), contributors (`docs/`), agents (`CLAUDE.md`), people browsing the installed data
folder (`assets/**/README.txt`), and whoever next edits the build (`.github/workflows/ci.yml`, `Directory.Build.props`)
— and nobody holds that map in their head while making a change. So here it is.

**Find the row for what you changed and check every file in it.** Not every file will need an edit;
the point is to have looked.

| If you change… | Check these |
|---|---|
| The `Labels/{Platform}.xml` format — `<Game>`, `<Defaults>`, lookup keys | `README.md` (Labels) · `docs/architecture.md` §3 · `docs/config-layering.md` · `CLAUDE.md` (Fixture structure + Labels pipeline) · `assets/User/Labels/README.txt` · `assets/README.txt` |
| `Controllers/{Platform}.xml` — variants, `inheritFrom`, `analogToDigital`, `default` | `README.md` (Input mappings) · `docs/templates.md` · `docs/architecture.md` (Add a new platform) · `CLAUDE.md` · `assets/User/Controllers/README.txt` · `assets/README.txt` |
| Per-game `InputMappings/` — `<GameMapping>`, `<Mapping>`, `<Unmap>` | `README.md` (Game-specific overrides) · `docs/architecture.md` §2 · `CLAUDE.md` (Input mapping) · `assets/User/InputMappings/README.txt` · `assets/README.txt` |
| `Layout.xml` — any element, attribute or `showIf` mode | `docs/layout-xml-schema.md` **(canonical)** · `docs/templates.md` · `docs/architecture.md` §4–5 · `CLAUDE.md` (Layout rendering notes) |
| How template images are resolved, or platform/variant artwork | `docs/templates.md` **(canonical)** · `docs/layout-xml-schema.md` (Image resolution) · `docs/architecture.md` §4–5 · `CLAUDE.md` · `README.md` (Platform button images) |
| `GlobalConfig.xml` settings | `README.md` (settings table) · `docs/config-layering.md` · `docs/architecture.md` · `CLAUDE.md` · `assets/User/README.txt` |
| The `Defaults\`/`User\` layering rules or a file's merge strategy | `docs/config-layering.md` **(canonical)** · `README.md` (Configuration) · `docs/architecture.md` (Config layering) · `CLAUDE.md` (Infrastructure conventions) · `assets/README.txt` · `assets/User/README.txt` |
| MAME or RetroArch integration | `README.md` (overrides + Known limitations) · `docs/architecture.md` (Plugin architecture) · `CLAUDE.md` · `assets/User/Emulators/**/README.txt` |
| `TargetFramework`, or the minimum supported LaunchBox version | `README.md` (Requirements + Development) · `CLAUDE.md` (Language) · `Directory.Build.props` (the `LangVersion` comment) |
| What the release zips contain | `README.md` (Installation + Updating) · `docs/config-layering.md` (Packaging impact) · `.github/workflows/ci.yml` (packaging comments) |

Two habits keep the map short:

- **Prefer a pointer to a restatement.** Where a row names a canonical doc, the others should link to
  it rather than duplicate its rules. A duplicated rule is a rule that will disagree with itself.
- **Adding a new user-facing XML element or attribute means documenting it.** The README is where
  users discover a feature exists; nothing else reaches them. `<Unmap>` shipped working and
  undiscoverable for months because it was only ever written up in the installed data folder.

## Pull requests

(To be expanded when the repository goes public.)

For now: small, focused changes; tests pass locally before submitting; one logical change per PR.
Run the table above before opening the PR — reviewers can spot wrong code, but nobody reviews the
doc you didn't think to open.
