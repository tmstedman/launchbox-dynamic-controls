# Code Conventions

Short rules to keep contributions consistent. The pattern matters more than the prose, so each section ends with a one-liner that summarises the rule.

## Type shapes: raw config vs. resolved domain

The codebase walks data through three layers, each with its own conventions.

### 1. Raw config DTOs

Types that exist as XML deserialisation targets. They mirror the on-disk schema field-for-field.

- Mutable: `public T Property { get; set; }`
- Collection fields use mutable `List<T>` / `Dictionary<K, V>` with default empty initialisers
- Naming suffix — set by which file the DTO comes from:
  - **`*Node`** — a node of the `Layout.xml` parse tree. That is the one config file with a real tree, so it takes AST vocabulary: `TemplateLoader` is the parser, `LayoutDocument` is the tree it returns, `LayoutResolver` binds it. Every element in the file is a `*Node` — `InputNode`, `GroupNode`, `StackNode`, `OneOfNode`, `RenderNode`, `OverlayNode`, `LabelNode`, `HeadNode`, `StyleNode` — whether or not it is drawn.
  - **`*Config`** — a DTO for one of the settings files, which are flat or list-shaped rather than trees: `GlobalConfig`, `PlatformControllersConfig`, `ControllerConfig`, `InputMappingConfig`, `InputLabelsConfig`.
  - **`*Entry`** — a name/value leaf: `MappingEntry`, `LabelEntry`.

  The cut is the **file**, not the semantics — ask "does this file parse into a tree?", not "is this type drawn?". `LayoutDocument` holds the tree rather than being a node within it, so it keeps a noun of its own, the same split as Roslyn's `SyntaxTree` and `SyntaxNode`.

Examples: `LayoutDocument`, `InputLabelsConfig`, `InputMappingConfig`, every `*Node` type in `Templates/LayoutDocument.cs`.

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

**Layout element naming.** The resolved layout mirrors the parse tree type for type, so the suffix tells you which layer you are in: `LabelNode` → `LabelDefinition` → `RenderedLabel`, that is syntax → bound → output. `*Definition` marks a bound type carrying data of its own — a name, or a resolved position, size and visibility. `InputGroup` and `OneOf` take no suffix because they carry only structure: which children, and how to choose between them.

**Marker interfaces mean "can nest", in both layers.** `ILayoutNode` (raw) and `ILayoutElement` (bound) are implemented only by types that can occupy a position in the tree — an input, a group or stack, a one-of. Renders, overlays and labels are owned by their parent and held in typed lists, so they carry the layer suffix without implementing the interface. This is deliberate and symmetric across the two layers; widening the interfaces would erase the distinction between what nests and what is owned.

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
- `tests/Core.IntegrationTests/` — integration tests above the unit level: subsystem tests (verify one [subsystem](architecture.md#subsystems-as-the-unit-of-work-and-the-unit-of-test) with its real internal wiring; I/O mocked so test data lives inline) and end-to-end tests (real templates, real Fixtures/ tree, full production pipeline)

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

## Class name suffixes

- **`*Service`** — a subsystem entry point. Expect a factory in `Composition/` and a subsystem-tier test to exist alongside it. `ControllerOverlayService` is the exception: it is the orchestrator that runs the subsystems, not one of them.
- **`*Resolver`** — turns inputs into a resolved value. Normally internal to one subsystem (`LayoutResolver`, `TemplateImageResolver`, `InputImageResolver`).
- **`*Loader`** — reads from disk and parses. Several also merge the `Defaults\`/`User\` layers or resolve `inheritFrom` chains while doing it (`ConfigLoader`, `InputLabelsLoader`, `InputMappingLoader`). `TemplateLoader` deliberately does not, leaving style inheritance and image lookup to `LayoutResolver` — so don't assume a loader is a pure parser without checking.
- **`*Config` / `*Node` / `*Entry`** — XML deserialisation targets; see [Raw config DTOs](#1-raw-config-dtos) for which of the three applies.

`StaticImageResolver` is the case that fixes the first two rules in place. It is called from outside its namespace like a subsystem entry point, but it is a file lookup that short-circuits the pipeline rather than a phase of it, so it stays a `*Resolver` — see [architecture.md](architecture.md#subsystems-as-the-unit-of-work-and-the-unit-of-test).

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

**A change to visible behaviour means checking every place that documents it, not just the nearest one.**

One concept commonly gets restated in five places: `README.md`, `docs/`, `CLAUDE.md`, `assets/**/README.txt`, and the build files (`.github/workflows/ci.yml`, `Directory.Build.props`). Those serve three audiences — users, contributors and agents — so two locations often serve the same reader. Where they do, collapse one into a pointer; the table is for facts that genuinely have to appear in more than one place.

Find the row for what you changed and check every file in it. Not every file will need an edit — the point is to have looked.

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

- **Prefer a pointer to a restatement.** Where a row names a canonical doc, the others link to it rather than duplicating its rules. A duplicated rule is one that will eventually disagree with itself.
- **A new user-facing XML element or attribute is not finished until the README names it.** The README is the only place a user discovers that a feature exists; documenting it solely in `assets/**/README.txt` reaches only people who already went looking in the data folder.

## Pull requests

Small, focused changes; one logical change per PR; tests passing locally before submitting. Run the table above before opening the PR — a reviewer can spot wrong code, but nobody reviews the doc you didn't think to open.
