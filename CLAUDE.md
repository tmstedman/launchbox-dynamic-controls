# Dynamic Controls

LaunchBox plugin that overlays controller button images and labels on the pause screen.

## Project layout

```
src/Core/          net6.0 — all business logic, no LaunchBox/WPF references
src/LaunchBox/     net6.0-windows — LaunchBox plugin host, WPF, Windows-only APIs
tests/Core.Tests/          unit tests (NSubstitute mocks, no disk I/O)
tests/Core.TestHelpers/    shared fixture builders used by both test projects
tests/Core.IntegrationTests/  subsystem + E2E tests against real fixture files
tests/LaunchBox.Tests/     LaunchBox-layer unit tests
```

`Core` must stay platform-neutral. Windows-specific APIs (registry, `SpecialFolder`, WPF) belong in `LaunchBox` or behind an interface implemented there.

## Conventions

**Three-layer type model** — data moves through three distinct shapes:
1. `*Config` / `*Node` / `*Entry` records — XML deserialisation targets. `Layout.xml` is the one config file that parses into a tree, so its DTOs take AST names (`*Node`, held by `LayoutDocument`); the flat settings files take `*Config`; name/value leaves take `*Entry` (see [conventions.md](docs/conventions.md#1-raw-config-dtos)); **all** properties are mutable `{ get; set; }` (the deserialiser needs setters — there is no `init` in this layer), collection fields are `List<T>` with `= []` initialisers (parsers call `.Add()` after construction); never escape the loader
2. Immutable positional records (`ResolvedLayout`, `ResolvedMapping`, `InputDefinition`, …) — built once, `IReadOnlyList`/`IReadOnlyDictionary` collections, `with` for derivation
3. Derived index fields (e.g. `InputDescendants`, `CollapseInfo`) computed during the build and stored on the resolved record — not recomputed on demand

**Reference equality on `InputDefinition` keys**: dictionaries keyed by `InputDefinition` (e.g. `InputDescendants`, `CollapseInfo`) use `ReferenceEqualityComparer.Instance`. Records have structural equality by default, so two distinct `InputDefinition` instances with the same name + children would collapse to one key without this — tests that build inline layouts must reuse the same instance, not reconstruct an "equal" one.

**Test tier placement:**
- One class, mocked collaborators → `Core.Tests`
- Real internal wiring, I/O faked, data inline → `Core.IntegrationTests/Subsystem` (what counts as a subsystem: [architecture.md](docs/architecture.md#subsystems-as-the-unit-of-work-and-the-unit-of-test))
- Full pipeline against `Fixtures/` on disk → `Core.IntegrationTests/EndToEnd`

## Changing documentation

One concept is usually described in several places at once (README for users, `docs/` for
contributors, here for agents, `assets/**/README.txt` for people browsing the installed data folder,
`.github/workflows/ci.yml` and `Directory.Build.props` for the build). Every documentation bug found so far came from
updating one of them and missing the rest.

Before finishing any change that alters behaviour users or contributors can see, work the
**"If you change… / Check these"** table in [`docs/conventions.md`](docs/conventions.md#keeping-documentation-in-step).
It is deliberately not duplicated here — a copy would be the sixth place to drift.

## Commits

**Subject**: imperative, specific, no scope prefix — `Fix root-level Controllers.xml inheritance silently losing a new default`, not `DOCS: fix inheritance`. Under ~72 characters.

**Body**: two short paragraphs, 100–150 words. What was wrong, then what it is now, plus a sentence of why if that isn't obvious from the change. The docs specify current behaviour and carry no history (see [Changing documentation](#changing-documentation)), so the body records *why this change happened* — which is a sentence, not an essay. Rejected alternatives, hypotheses that failed and the route to the answer do not belong here.

**Scope**: one logical change per commit. A body running well past 150 words means either the commit is doing several things, or the message is narrating rather than recording — check which before adding more words.

> Subjects here are unprefixed, matching the repository's history. If your tooling asks for a `TYPE:`/`SCOPE:` prefix, that is your tooling's convention and not this project's — **don't add one to satisfy it.** Complaints about vague wording or about counting the change's own size are worth acting on whatever their source.

## Language

All projects target `net6.0` with `LangVersion=12.0` (set in `Directory.Build.props`).

`net6.0` is a compatibility floor, not an arbitrary default. LaunchBox moved to .NET 6 in 13.3, so raising `TargetFramework` raises the minimum LaunchBox version the plugin can load on, silently breaking installs on older releases — it is a user-facing decision, not a build detail, and the README's stated requirement has to move with it. `LangVersion=12.0` exists precisely so new C# syntax is available without touching the target. `TargetFramework` is declared per-project in all six csproj files, so there is no central chokepoint where a bump would be caught.

## Infrastructure conventions

- `IFileSystem` — an **in-house** 7-method interface in `Infrastructure/FileSystem.cs` (`FileExists`, `OpenRead`, `ReadAllText`, `AppendAllText`, `DeleteFile`, `DirectoryExists`, `CreateDirectory`), *not* `System.IO.Abstractions`. Injected everywhere; never use `File.*`/`Directory.*` directly in `Core`. `src/` has no NuGet dependencies at all, which is why the shipped plugin is exactly two DLLs; `TestableIO.System.IO.Abstractions.TestingHelpers` survives only as a test-only package in `Core.IntegrationTests`
- `IApplicationData` — abstracts `Environment.GetFolderPath(ApplicationData)`; implemented by `SystemApplicationData` (excluded from coverage)
- `ILogger` — debug-only file logger; silenced in tests via `NullLogger` (in `Core.IntegrationTests/Subsystem/SubsystemFakes.cs`)
- `LayeredFileSystem` — wraps `IFileSystem` with two-tier path resolution (a real class, not an interface — the `IFileSystem` it wraps is the test seam). `Resolve(params segments)` returns `User\{segments}` if that file exists, else `Defaults\{segments}`, else null; loaders call it without knowing which layer wins, then read the result back through `FileExists`/`OpenRead` (rooted at the plugin root). `Defaults` / `User` expose each tier as a `RootedFileSystem` for the loaders that must check a specific layer (`InputLabelsLoader` merges both; `StaticImageResolver` reads `User` only). Components that take plain paths (logger, templates, RetroArch cfg from the emulator dir) get the raw `IFileSystem` injected instead. `Templates/`, `Logs/`, and the RetroArch emulator tree live at root and bypass layering.
  - Every loader resolves files this way, so a `User\` file *wholesale shadows* its `Defaults\` counterpart — **except `GlobalConfig.xml` and `Labels\{Platform}.xml`**, which are merged. `ConfigLoader` merges the config field-by-field: it deserialises `Defaults\GlobalConfig.xml` as the base, then overwrites only the fields whose elements are *present* in `User\GlobalConfig.xml` (detected via an `XmlDocument` pass over the child element names). This stops a user file that sets one field from silently forcing every omitted bool back to `false`.
  - `InputLabelsLoader` merges the labels file *entry-by-entry*: it reads the `Defaults\` and `User\` copies separately (via `lfs.Defaults` / `lfs.User`, not `Resolve`) and lays the user's `<Game>` entries over the shipped ones (matched by `launchBoxId`, then `romName`), and the user's `<Defaults>` buttons over the shipped ones by name. One file now holds every game on a platform, so shadowing would make labelling one game drop the rest.
- `[ExcludeFromCodeCoverage]` — applied to infrastructure shims, factory classes, and pure DTOs

## Test patterns

**Unit tests (`Core.Tests`)**
- `TestFs.Create()` — bare NSubstitute `IFileSystem` (path ops use `Path.*` directly, not the filesystem, so no wiring needed)
- Fixture builders (each in its own `Core.TestHelpers/{Topic}/{Name}.cs`, intended to be `using static` imported):
  - `LabelsFixtures.LabelsOf(isGameSpecific, ...)` / `EmptyLabels()`
  - `MappingFixtures.MappingOf(...)` / `EmptyMapping(...)`
  - `TemplateFixtures.TemplateOf(...)` — wraps `Template` construction
  - `LayoutElements.Input(...)` / `Group(...)` / `Stack(...)` / `OneOf(...)` — `ILayoutElement` builders
  - `RenderingFixtures.Ctx(...)` / `Descendants(...)` — `VisibilityContext` + descendants index
  - `TestLayout` — fluent `LayoutDocument` builder (raw XML-shaped DTOs, for `LayoutResolver` tests)
  - `InputMappingFixtures.Game(...)` / `MappingConfig(...)` / `PlatformConfig(...)` / `ControllerDef(...)` — builders for `GameInfo` and the raw Controllers/InputMappings DTOs; use these rather than hand-constructing `InputMappingConfig` / `PlatformControllersConfig` / `ControllerConfig` inline
  - `LayoutNavigation` — extensions on `ResolvedLayout` *and* on element sequences, so lookups chain (`result.FirstInput().Children.FirstInputGroup()`): `FirstInput()` / `FirstInputGroup()` / `FirstOneOf()`, plus `Flatten()` for a depth-first walk that reaches inputs nested inside transparent Groups
- `ShouldBeDictionaryOf(...)` — custom Shouldly assertion for exact dict contents; `ShouldContainEntry(name, input)` / `ShouldNotContainEntry(...)` match a whole `MappingEntry` by value. Both live in `Core.TestHelpers/Shouldly/` and are declared *in the `Shouldly` namespace* so they autocomplete alongside the built-ins
- Global usings (`tests/Core.Tests/GlobalUsings.cs`) cover `Xunit`, `Shouldly`, `DynamicControls.Infrastructure`, `DynamicControls.Core.Tests.Infrastructure` and the common `System.*` namespaces — **not** `NSubstitute`, which every test file that needs it imports explicitly

**Subsystem tests (`Core.IntegrationTests/Subsystem`)**
- Real service wiring via factory; fake at one seam (e.g. `FakeTemplateImageSource`)
- `TemplateFixtures.TemplateOf(elements, imageSource, ...)` builds a `Template` with `InputDescendantsBuilder` run automatically; pass `inputDescendants:` explicitly to bypass it (needed for tests with synthetic `ILayoutElement` subtypes)

**E2E tests (`Core.IntegrationTests/EndToEnd`)**
- `ControllerOverlayFactory.Create(FixturesRoot)` — full production stack
- Fixtures live under `tests/Core.IntegrationTests/Fixtures/` and are copied to output via `<None Include="Fixtures\**\*" CopyToOutputDirectory="PreserveNewest" />`
- Assertions via `overlay.InTemplate(@"Templates\Xbox Series X").ShouldHaveImages(...)` and `overlay.ShouldHaveLabels(...)`
- `ShouldHaveImages` compares short template-relative paths (e.g. `@"Sega Genesis\A.png"`, `"ButtonA.png"`); throws `Exception` (not `ShouldAssertException`) so VS Code renders the message without cascading indentation

## Fixture structure

```
Fixtures/
  Defaults/
    GlobalConfig.xml
    Controllers/{Platform}.xml            — button vocabulary + analogToDigital per controller variant; a <Controller> may carry inheritFrom="OtherController" to prepend that controller's mappings before its own. Inheritance is transitive (the base may itself inheritFrom another, e.g. NEC 6-Button → 3-Button → 2-Button) with cycles detected and broken. Only mappings are inherited — analogToDigital is read from the controller's own attribute, not the chain. The root <Controllers> element may also carry inheritFrom="RootPlatform" to pull in the family root's controllers and merge this file's own on top (override by name, append new) — descendants in a hardware family reduce to a one-line pointer at the root (e.g. Nintendo Famicom.xml → inheritFrom="Nintendo Entertainment System"; Sony Playstation 2.xml → inheritFrom="Sony Playstation"). Root inheritance is transitive and cycle-safe; a missing base file is logged and the file falls back to its own controllers
    InputMappings/{Platform}/{Rom}.xml    — per-game controller selection or button remaps
    Labels/{Platform}.xml                 — one file per platform: a <Defaults> block (all
                                            entries inheritable) plus a <Game launchBoxId= romName=>
                                            element per title. Merged across Defaults\ and User\ at
                                            the entry level, not shadowed wholesale
    Emulators/RetroArch/{CoreDisplayName}.xml — maps RetroArch device-type IDs to controller variants
    Emulators/MAME/JoycodeMapping.xml        — JOYCODE → generic-input lookup
  User/
    (mirrors Defaults/ structure; files here shadow the Defaults counterpart —
     except Labels/{Platform}.xml, which is merged entry-by-entry)
  controls.xml                            — BYOAC MAME controls database (plugin root, unlayered)
  Emulators/
    mame/mame.exe + cfg/{rom}.cfg
    retroarch/retroarch.exe + retroarch.cfg (presence signals portable mode)
               info/{coreDll}.info         — corename = "..." for display name resolution
               config/{CoreDisplayName}/{Rom}.cfg
  Templates/Xbox Series X/Layout.xml + images
```

## Pipeline overview

```
StaticImageResolver.Find(game)     → early exit if Static/{platform}/{rom}.png/jpg exists
InputMappingService.Load(game)     → ResolvedMapping
InputLabelsService.Load(game, mapping) → ResolvedLabels
TemplateService.Load(templateName) → Template  (cached per name, never invalidated)
InputRenderingService.Render(...)  → RenderResult (flat lists of RenderedImage / RenderedLabel)
```

`ControllerOverlayService.Resolve` wraps the whole pipeline in a try/catch — any thrown exception is logged and converted to an empty `ControllerOverlayModel` so the pause screen doesn't show stale data. Tests against this service won't see exceptions surface; failures appear as empty output plus an error log entry.

## Input mapping

**Source priority** (first non-null wins): `PerGameXmlMappingSource` → `RetroArchMappingSource` → `PlatformDefaultMappingSource`. Transforms (`MameInputMappingSource`) are applied on top of whichever source wins. When a transform applies, `InputMappingService` re-splices the `Natural*` maps from the pre-transform baseline so remap detection still works correctly.

**Per-game overlay** (`PerGameXmlMappingSource`, via `MappingOverlay.Apply`): a `<GameMapping>` file overlays the selected controller's baseline. `<Mapping name="A" input="..."/>` replaces whatever that platform button had; `<Unmap name="C"/>` drops a baseline button with nothing in its place — for buttons a game isn't meant to use, in parity with RetroArch's `-1` sentinel. Baseline entries whose `name` appears in either list are dropped, then every overlay entry is appended — so repeating `<Mapping name="A">` with different `input` values drives several template slots from one platform button. The same `MappingOverlay.Apply` backs controller-level `inheritFrom` resolution.

`ResolvedMapping` carries two parallel views:
- `ButtonToInput` / `InputToButton` — the *current* mapping (after per-game remaps/transforms)
- `NaturalButtonToInput` / `NaturalInputToButton` — snapshot of the mapping *before* transforms

**`IsMapped`** (used by `VisibilityEvaluator`): an input is considered mapped if a platform button currently drives it *or* if its natural physical button is still present in the current mapping. This keeps a button visible on screen after its action has been remapped away (e.g. ButtonB after MAME swaps BUTTON2 onto ButtonA — ButtonB's natural button still exists in the mapping).

`InputImageResolver` classifies each input as `Unmapped`, `MappedDefault`, or `Remapped`:
- **Unmapped** — no platform button drives this input; identity renders fall back to generic image, `useImage` renders get platform-specific variant
- **MappedDefault** — platform button drives this input and it's the same button as the controller default; image resolution prefers `{platformButton}.png` over the generic
- **Remapped** — a platform button drives this input but that button's *natural* target is a different input; image follows the physical button so the player sees what they're pressing

`AnalogToDigitalMirror` appends stick generics to every `ButtonToInput` list that contains a matching Dpad generic — it affects `ButtonToInput` only, not `InputToButton`, so image resolution is unaffected. The triggering attribute lives on `<Controller>` in Controllers.xml or on `<GameMapping>` in per-game InputMappings.

## MAME plugin

Two parts with different roles:
- `MameControlsXmlSource` — `IInputLabelsLoader`; supplies labels from `controls.xml` when the emulator is MAME
- `MameInputMappingSource` — `IInputMappingTransform` (not `IInputMappingSource`); overlays JOYCODE overrides onto the baseline mapping rather than producing a full mapping from scratch

cfg lookup: `cfg/{romName}.cfg` first, fallback to `cfg/default.cfg`. JOYCODE values are translated to generic input names via `JoycodeMapping.xml` (must exist; empty mapping if absent). A single MAME port can list multiple JOYCODEs joined with `OR`, driving multiple generic inputs simultaneously — this is how joystick ports can label both Dpad and AxisLeftStick at once.

## RetroArch config resolution

Portable mode: `retroarch.cfg` exists next to `retroarch.exe` → config root = exe dir.
Non-portable: config root = `%APPDATA%\RetroArch`.
Cascade (later overrides earlier): `retroarch.cfg` (joypad driver only) → `config/{core}/{core}.cfg` → `config/{core}/{contentDir}.cfg` → `config/{core}/{rom}.cfg`.
Remap file: `config/remaps/{core}/{rom}.rmp`. Variant selection walks the full remap cascade (game → content-dir → core → common, first match wins); swap detection uses game-level only (see trust boundary below).
Variant selection: rmp wins over cfg; neither → platform Controllers.xml default.

**Trust boundary** — Controllers.xml is treated as already incorporating all non-game-specific configuration (global cfg, core cfg, core remap). Button swaps from those layers are not re-applied because resolving them requires knowing the physical controller layout via RetroArch autoconfig — too fragile to derive reliably. Which controller type is active (`input_libretro_device_p1`) is the exception: it is an explicit discrete choice with no hardware dependency, so it is read from the full cascade.

## Labels pipeline

1. `InputLabelsLoader` — the `<Game>` entry in `Labels/{platform}.xml` (tried first). Lookup order: `launchBoxId` → case-insensitive `romName` → `RomNameUtils.NormalizeRomName` on both sides (strips `(...)`/`[...]` groups)
2. `MameControlsXmlSource` — `controls.xml` (only when `EmulatorPath` is MAME; gated on the emulator, *not* on `EnableMame`)
3. If no game labels found → the `<Defaults>` block of `Labels/{platform}.xml` on its own
4. Every `<Defaults>` entry is inheritable — merged into game-specific labels for any platform button the game didn't name (e.g. Start=Pause applies even when the game only defines button labels). There is no `inherit` attribute
5. Clone-of ROMs inherit their parent's labels

## Layout rendering notes

- `showIf="auto"` → `HasLabel` when game-specific, `IsMapped` when default. This serves two distinct rendering modes: for Arcade/MAME games where `MameControlsXmlSource` supplies labels, `IsGameSpecific=true` so `auto` resolves to `HasLabel` — controls.xml acts as a button mask. For platforms with custom images but no per-game labels (e.g. Sega Genesis default), `IsGameSpecific=false` so `auto` resolves to `IsMapped` — all mapped buttons are active.
- `showIf="label"` → `HasLabel` always; `showIf="mapping"` → `IsMapped` always
- `Group` (`AlwaysInclude=false`) — included only when any descendant has a visible render; otherwise the entire group (inputs, overlays, Stack entries) is dropped
- `Stack` (`AlwaysInclude=true`) — always included; individual entries handle their own visibility via `showIf`
- `OneOf` picks first alternative where `AnyVisible` is true
- Face-button cluster is a `<Group>` wrapping a `<Stack>` of `<Input>`s with `style="input-label"` (showIf=label). When no face-button labels exist, the Group's any-descendant-visible check fails and the whole cluster (Stack + overlays) drops out
- Directional inputs (Dpad, AxisLeftStick) use a `<OneOf>`: per-direction labels fire the first alternative (directional Stack + `Line_ButtonDpad_Multi.png` / `Line_AxisLeftStick_Multi.png`); a whole-input label only fires the second alternative (single render + `Line_AxisLeftStick.png` etc.)
- An `<Overlay>`'s `InputName` comes from its parent: null when parented to a `<Group>` or `<Stack>`, the input's name when parented to an `<Input>`
