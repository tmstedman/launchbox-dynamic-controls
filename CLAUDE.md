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
1. `*Config` / `*Node` records — XML deserialisation targets; scalar properties use `init`, collection fields use mutable `List<T>` (parsers call `.Add()` after construction); never escape the loader
2. Immutable positional records (`ResolvedLayout`, `ResolvedMapping`, `InputDefinition`, …) — built once, `IReadOnlyList`/`IReadOnlyDictionary` collections, `with` for derivation
3. Derived index fields (e.g. `InputDescendants`, `CollapseInfo`) computed during the build and stored on the resolved record — not recomputed on demand

**Reference equality on `InputDefinition` keys**: dictionaries keyed by `InputDefinition` (e.g. `InputDescendants`, `CollapseInfo`) use `ReferenceEqualityComparer.Instance`. Records have structural equality by default, so two distinct `InputDefinition` instances with the same name + children would collapse to one key without this — tests that build inline layouts must reuse the same instance, not reconstruct an "equal" one.

**Test tier placement:**
- One class, mocked collaborators → `Core.Tests`
- Real internal wiring, I/O faked, data inline → `Core.IntegrationTests/Subsystem`
- Full pipeline against `Fixtures/` on disk → `Core.IntegrationTests/EndToEnd`

## Language

All projects target `net6.0` with `LangVersion=12.0` (set in `Directory.Build.props`).

## Infrastructure conventions

- `IFileSystem` — from `System.IO.Abstractions`; injected everywhere; never use `File.*`/`Directory.*` directly in `Core`
- `IApplicationData` — abstracts `Environment.GetFolderPath(ApplicationData)`; implemented by `SystemApplicationData` (excluded from coverage)
- `ILogger` — debug-only file logger; silenced in tests via `NullLogger` (in `Core.IntegrationTests/Subsystem/SubsystemFakes.cs`)
- `LayeredFileSystem` — wraps `IFileSystem` with two-tier path resolution (a real class, not an interface — the `IFileSystem` it wraps is the test seam). `Resolve(params segments)` returns `User\{segments}` if that file exists, else `Defaults\{segments}`; loaders call it without knowing which layer wins. `FileExists`/`OpenRead` delegate straight through; `Fs` exposes the underlying `IFileSystem` for components that take plain paths (logger, templates, RetroArch cfg reading from the emulator dir). `Templates/`, `Logs/`, and the RetroArch emulator tree live at root and bypass layering.
  - Every loader resolves files this way, so a `User\` file *wholesale shadows* its `Defaults\` counterpart — **except `GlobalConfig.xml`**. `ConfigLoader` merges that one field-by-field: it deserialises `Defaults\GlobalConfig.xml` as the base, then overwrites only the fields whose elements are *present* in `User\GlobalConfig.xml` (detected via an `XmlDocument` pass over the child element names). This stops a user file that sets one field from silently forcing every omitted bool back to `false`.
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
  - `TestLayout` — fluent `LayoutConfig` builder (raw XML-shaped DTOs, for `LayoutResolver` tests)
- `ShouldBeDictionaryOf(...)` — custom Shouldly assertion for exact dict contents (in `Core.TestHelpers/Shouldly/`)
- Global usings include `NSubstitute`, `Shouldly`, `DynamicControls.Infrastructure`, `DynamicControls.Core.Tests.Infrastructure`

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
    Labels/{Platform}/_DefaultLabels.xml  — inheritable defaults (inherit="true")
    Labels/{Platform}/{Rom}.xml           — game-specific labels
    Emulators/RetroArch/{CoreDisplayName}.xml — maps RetroArch device-type IDs to controller variants
    Emulators/MAME/JoycodeMapping.xml        — JOYCODE → generic-input lookup
    controls.xml                          — BYOAC MAME controls database
  User/
    (mirrors Defaults/ structure; files here shadow the Defaults counterpart)
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

1. `InputLabelsLoader` — `Labels/{platform}/{rom}.xml` (file-based, tried first)
2. `MameControlsXmlSource` — `controls.xml` (only when `EmulatorPath` is MAME)
3. If no game labels found → `_DefaultLabels.xml`
4. Entries marked `inherit="true"` in `_DefaultLabels.xml` are merged into game-specific labels (e.g. Start=Pause applies even when the game only defines button labels)
5. Clone-of ROMs inherit their parent's labels

## Layout rendering notes

- `showIf="auto"` → `HasLabel` when game-specific, `IsMapped` when default. This serves two distinct rendering modes: for Arcade/MAME games where `MameControlsXmlSource` supplies labels, `IsGameSpecific=true` so `auto` resolves to `HasLabel` — controls.xml acts as a button mask. For platforms with custom images but no per-game labels (e.g. Sega Genesis default), `IsGameSpecific=false` so `auto` resolves to `IsMapped` — all mapped buttons are active.
- `showIf="label"` → `HasLabel` always; `showIf="mapping"` → `IsMapped` always
- `Group` (`AlwaysInclude=false`) — included only when any descendant has a visible render; otherwise the entire group (inputs, overlays, Stack entries) is dropped
- `Stack` (`AlwaysInclude=true`) — always included; individual entries handle their own visibility via `showIf`
- `OneOf` picks first alternative where `AnyVisible` is true
- Face-button cluster is a `<Group>` wrapping a `<Stack>` of `<Input>`s with `style="input-label"` (showIf=label). When no face-button labels exist, the Group's any-descendant-visible check fails and the whole cluster (Stack + overlays) drops out
- Directional inputs (Dpad, AxisLeftStick) use a `<OneOf>`: per-direction labels fire the first alternative (directional Stack + `LineDpad_Multi.png` / `LineL_Multi.png`); a whole-input label only fires the second alternative (single render + `LineL.png` etc.)
- An `<Overlay>`'s `InputName` comes from its parent: null when parented to a `<Group>` or `<Stack>`, the input's name when parented to an `<Input>`
