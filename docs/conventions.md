# Code Conventions

Short rules to keep contributions consistent. The pattern matters more than the prose, so each section ends with a one-liner that summarises the rule.

## Type shapes: raw config vs. resolved domain

The codebase walks data through three layers, each with its own conventions.

### 1. Raw config DTOs

Types that exist as XML deserialisation targets. They mirror the on-disk schema field-for-field.

- Mutable: `public T Property { get; set; }`
- Collection fields use mutable `List<T>` / `Dictionary<K, V>` with default empty initialisers
- Naming suffix: `Config` or `Node` (e.g. `TemplateLayoutConfig`, `InputNode`, `LabelEntry`)
- One-call-site loaders own them — they're populated and never mutated again
- Default values use field initialisers (e.g. `= new()`) because deserialisers need a target to populate

Examples: `TemplateLayoutConfig`, `InputLabelsConfig`, `InputMappingConfig`, every `*Node` type in `Templates/TemplateLayoutConfig.cs`.

These look "dated" by modern .NET standards. That's intentional — the deserialiser needs setters and no-arg constructors. Don't fight this layer.

> **Rule**: XML/disk DTOs are mutable, suffix `Config` or `Node`, and never escape the loader that produces them.

### 2. Resolved domain types

The in-memory model the rest of the codebase reads from. Always built once, by a service that takes raw configs as input, and never mutated afterward.

- Immutable: positional records, with `IReadOnlyList<T>` / `IReadOnlyDictionary<K, V>` collections
- Naming prefix `Resolved` or descriptive noun (e.g. `ResolvedLayout`, `ResolvedMapping`, `ResolvedLabels`, `Template`)
- Use `with` expressions to derive modified copies, never property setters
- Derived/index fields (lookups computed from other fields) live as separate dictionary fields on the same record, populated by the builder — don't expose them as methods on the record itself

Examples: `Template`, `ResolvedLayout`, `ResolvedLabels`, the `LayoutElement` hierarchy (`InputDefinition`, `InputGroup`, `OneOf`).

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

Example: `TemplateConfigurer.Configure` builds a `List<LayoutElement>` locally, computes the descendants index via an injected `IInputDescendantsBuilder`, and returns one `ResolvedLayout` with everything settled.

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

## Pull requests

(To be expanded when the repository goes public.)

For now: small, focused changes; tests pass locally before submitting; one logical change per PR.
