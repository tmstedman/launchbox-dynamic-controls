# Architecture

The shape of the codebase, how data flows through it, and the decisions that hold it together.

## What it does

DynamicControls is a plugin for [LaunchBox](https://www.launchbox-app.com/) that displays a controller overlay when a game launches. The overlay shows the player's physical controller with the buttons they actually need labeled — Brake / Accelerate for a racing game, Punch / Kick for a fighter — derived from whichever ROM is being launched and whichever emulator and controller are configured to run it.

The interesting work is in producing the right labels in the right positions for any combination of platform, controller, emulator, and game, given that:

- Different controllers have different physical button layouts (Xbox vs. PlayStation vs. arcade stick)
- Different emulators map controllers differently (MAME has its own input scheme; RetroArch has core-specific configs; per-emulator XML mapping files override defaults)
- Different games supply their own labels (per-game label XML, MAME `controls.xml`)
- Labels need to land in slots that match the visible controller image, which itself depends on the user's chosen template

So the input is `(game, controller, emulator, template)` and the output is a `ControllerOverlayModel` — an image path plus a list of positioned labels and per-input image renders. The work in between is the pipeline this document describes.

## Project layout

```
src/
  Core/                          # platform-agnostic logic (net6.0, no WPF, no Windows deps)
    Composition/                 # factories that wire concrete services together
    Config/                      # plugin-wide config (GlobalConfig)
    InputMapping/                # platform-specific controller button → generic input name
    Labels/                      # raw label text per generic input
    Templates/                   # the visible controller image + layout
    Rendering/                   # the final pass - calculates positions + image paths + opacity, per-input/label
    Plugins/
      ControlsXml/               # BYOAC controls.xml label source (gated on the launched emulator)
      Mame/                      # MAME-specific data sources (cfg JOYCODE remaps)
      RetroArch/                 # RetroArch-specific data sources
    Static/                      # per-game static image override under User/Static/ (the cheapest path)
    Infrastructure/              # shared infrastructure utilities e.g. IFileSystem, ILogger, etc.
    ControllerOverlayService.cs  # top-level orchestrator
    ControllerOverlayModel.cs    # the output shape
    GameInfo.cs                  # the input shape
  LaunchBox/                     # a thin-wrapper that integrates with the Windows-only WPF shell; minimal logic
                                 # subscribes to LaunchBox events and forwards to Core
tests/
  Core.Tests/                    # unit tests (no fixture files, no I/O)
  Core.IntegrationTests/         # subsystem tests (real wiring within one subsystem)
                                 # and end-to-end tests (full pipeline against Fixtures/)
docs/                            # this file lives here
```

The hard boundary is `Core/` ↔ `LaunchBox/`. Anything that needs WPF, references LaunchBox SDK assemblies, or only runs on Windows lives in `LaunchBox/`. `Core/` is a plain `net6.0` library — runnable, testable, and renderable anywhere a runtime exists. That split is what lets the whole pipeline below run on non-Windows machines with no LaunchBox installed (see end-to-end tests).

## The pipeline

A single call sequence on game launch. Each meaningful step is a **subsystem** — one phase of the work with a single entry-point class (`*Service` or `*Resolver`), taking resolved types in and out. Subsystems don't share state; one subsystem's output is the next one's input. That independence is what holds the codebase together — each subsystem can change and be tested on its own, and is also the natural unit for integration testing, since wiring one subsystem up with real internals reaches a far wider scenario range than an end-to-end test can economically cover (see [conventions.md](conventions.md) for the three test tiers).

Top-down:

```
LaunchBox event
  → DynamicControlsPlugin
    → ControllerOverlayService.Resolve(GameInfo)
        1. StaticImageResolver.Find(game)          ─── early exit if hit
        2. InputMappingService.Load(game)          → ResolvedMapping
        3. InputLabelsService.Load(game, mapping)  → ResolvedLabels
        4. TemplateService.Load(templateName)      → Template
        5. InputRenderingService.Render(...)       → RenderResult
        6. assemble ControllerOverlayModel
  → DynamicControlsViewModel updates → WPF redraws
```

Steps 2–5 are the real work and what the rest of this document covers. Each step's contract is "input shape → output shape" and the steps don't share state — one step's output is the next one's input.

### 1. Static image fast path

Some users supply per-game pre-rendered overlays as raw images. `StaticImageResolver.Find(GameInfo)` looks for `User/Static/{platform}/{rom}.png` (or `.jpg`); if it hits, the rest of the pipeline is skipped and that image is returned directly. Cheap, and lets users override the dynamic output for specific games. This is the one user-facing data path with no `Defaults/` counterpart — shipping a static image would defeat the dynamic overlay.

Namespace: `src/Core/Static/`. Entry point: `StaticImageResolver` (a `*Resolver`, not a `*Service`, because there's no resolution chain to walk — just a file lookup).

### 2. Input mapping resolution

`InputMappingService.Load(GameInfo)` produces a `ResolvedMapping`. The job: take "platform button names" (e.g. `Cross`, `Square` for PlayStation) and produce a map to "generic input names" (e.g. `ButtonA`, `ButtonB`) that the rest of the codebase reasons about.

The resolution chain (highest priority wins):

1. **Per-game XML** mappings under `InputMappings/{platform}/`
2. **Emulator-specific** — RetroArch core config + per-content overrides
3. **Platform default** — the controller selected from `Controllers/{platform}.xml`

On top of whichever source wins, an ordered list of `IInputMappingTransform`s runs. MAME is the only one today: it overlays the JOYCODE assignments read from the emulator's `cfg/` files onto the mapping already chosen. A transform amends the winning mapping rather than producing one from scratch, which is why it isn't a source.

(All paths resolve through the `Defaults/`+`User/` layering — see [Config layering](#config-layering).)

After the base mapping is built, the service applies `AnalogToDigital` mirroring — if the configured controller has a Dpad and a left stick, the Dpad input drives both, so the rendered overlay shows labels on both at once.

`ResolvedMapping` carries two reverse-direction snapshots (`NaturalButtonToInput`, `NaturalInputToButton`) of the pre-game-modifier state, so the renderer can later detect when a button has been remapped and choose the right artwork.

Namespace: `src/Core/InputMapping/`. Entry point: `InputMappingService`. Emulator-specific sources are pluggable — see [Plugin architecture](#plugin-architecture).

### 3. Label resolution

`InputLabelsService.Load(GameInfo, ResolvedMapping)` produces a `ResolvedLabels`. The job: produce display text per *generic* input name (`ButtonA → "Brake"`), having merged per-game labels with platform defaults and translated through the mapping.

Each `IInputLabelsLoader` is tried in order. The first one with non-empty data for this ROM wins:

1. **Per-game entry** in `Labels/{platform}.xml` — one file per platform holding a `<Game>` element per title. Looked up by `launchBoxId`, then by case-insensitive `romName`, then by a normalised `romName` — `RomNameUtils.NormalizeRomName` strips `(...)` and `[...]` groups from both sides, so region and revision tags don't cost a match
2. **MAME controls.xml** (only if the emulator is MAME) — has labels for thousands of arcade games

If no loader has game labels, the `<Defaults>` block of `Labels/{platform}.xml` is used on its own — the pause/start labels common to the platform.

Every entry in that `<Defaults>` block is inheritable. When a game *does* have its own labels, defaults are merged in for any platform button the game didn't name, so Start = "Pause" applies to every Genesis game even if the game only specifies racing labels. Clone-of ROMs inherit their parent's labels. The final dictionary is keyed by generic input name (`ButtonA`, `AxisLeftStickUp`).

The `IsGameSpecific` flag tells the renderer "the game contributed at least one of its own label entries." This flips the meaning of `showIf="auto"` on a template — see below.

Namespace: `src/Core/Labels/`. Entry point: `InputLabelsService`. Emulator-specific loaders are pluggable — see [Plugin architecture](#plugin-architecture).

### 4. Template resolution

`TemplateService.Load(templateName)` produces an immutable `Template`, cached for the process lifetime per template name. The template is the visible controller (`Xbox Series X`, `Sega Genesis 3-Button`, etc.).

`TemplateService` leans on three pieces:

1. **`TemplateLoader`** parses `Templates/{templateName}/Layout.xml` into a raw `LayoutConfig` — a tree of `InputNode`, `GroupNode`, `StackNode`, `OneOfNode`. Pure XML deserialisation.

2. **`LayoutResolver`** transforms the raw config into `ResolvedLayout` — the same tree but resolved: relative coordinates → absolute canvas positions, image filenames derived from input names, overlay paths resolved via `TemplateImageResolver`, style chains flattened, `showIf` strings parsed to enum, collapsing-Stack metadata stamped. It also precomputes two lookup tables off the resolved tree (`InputDescendants` for visibility fan-out and `CollapseInfo` for render-time slot adjustments) so the renderer can run without re-walking the tree.

3. **`TemplateImageResolver`** finds the base image (`BaseImage.png`) and resolves each image filename to a *pair* of candidates: a **styled** path (`Templates/{template}/{platform}/{controller}/{file}`, else `Templates/{template}/{platform}/{file}`, else none) and a **generic** path (`Templates/{template}/{file}`, else the shared `Templates/{file}`). It doesn't pick between them — that is a render-time decision taken from the input's mapping state, see [Rendering](#5-rendering).

The result, a `Template`, holds the resolved layout, the image source, and the base image dimensions. Cached because templates rarely change and re-parsing them on every game launch would be wasteful.

Namespace: `src/Core/Templates/`. Entry point: `TemplateService`. The raw `LayoutConfig` (DTOs) → `ResolvedLayout` (immutable domain) transition is the canonical example of the three-layer type model from [conventions.md](conventions.md).

### 5. Rendering

`InputRenderingService.Render(Template, ResolvedMapping, ResolvedLabels)` produces a `RenderResult` — flat lists of `RenderedLabel` and `RenderedImage`, each with absolute canvas positions, opacity, blur radius, and the input name they belong to.

The internal flow is two passes:

**Layout filtering** (`LayoutFilter`) walks the template's resolved element tree applying visibility rules:

- `InputDefinition` → always considered; its renders are filtered by `showIf` against the label/mapping context
- `InputGroup` with `AlwaysInclude=true` (a Stack) → always rendered; children handle their own visibility
- `InputGroup` with `AlwaysInclude=false` (a plain Group) → rendered only when any descendant has a visible render; otherwise the whole group's inputs are dropped from `inputsToRender`
- `OneOf` → only the first alternative with a visible render is rendered; the rest are dropped

`showIf` modes: `label` (show when this input has a label), `mapping` (show when a platform button drives it), `auto` (label-mode if the game contributed its own labels, else mapping-mode), or omitted (always).

Then the filter computes per-input Y-offset adjustments for collapsing Stacks: members whose images are all zero-opacity vacate their slot, shifting subsequent members up by the stack's gap. The collapse-info dictionary (built by `CollapseGroupBuilder`) keys this lookup by reference identity, deduplicating per-stack.

**Image and label rendering** (`InputImageRenderer`, `InputLabelRenderer`) then walks the filtered layout per input, resolves image paths through the mapping-aware `InputImageResolver`, and emits the final `RenderedImage` / `RenderedLabel` records carrying positions, sources, opacity, and the input name. The `InputName` metadata lets end-to-end tests assert that the right label landed on the right input slot.

`InputImageResolver` is where the styled/generic pair from step 4 gets decided, by classifying the input against the mapping:

- **Unmapped** — no platform button drives this input. An identity render falls back to the generic image (platform-styled art for a button the controller doesn't have would mislead); a `useImage` render still honours the styled variant, because it's borrowing another input's asset rather than claiming one of its own.
- **MappedDefault** — a platform button drives the input and it's that button's natural target. The platform button's own file wins (`B.png` before `ButtonA.png`), falling back to the generic.
- **Remapped** — a platform button drives the input, but that button naturally targets something else. The image follows the *physical* button, so the player sees what they're actually pressing.

Namespace: `src/Core/Rendering/`. Entry point: `InputRenderingService`. The render pass only ever sees resolved types — it doesn't know about platforms or emulators (see [Renderer doesn't know about platforms or emulators](#renderer-doesnt-know-about-platforms-or-emulators)).

### 6. Assembling the model

`ControllerOverlayService` packages the `RenderResult` into a `ControllerOverlayModel` along with the canvas dimensions (from the template's base image, defaulting to constants in `RenderingDefaults` when none exists). The WPF view-model in `LaunchBox/` consumes this model and the WPF view renders the image and labels at the positions the model specifies.

## Plugin architecture

`Labels/` and `InputMapping/` both delegate part of their resolution to a pluggable, ordered list of contributors. Each emulator-specific source lives under `Plugins/{Emulator}/` and implements one or both of two interfaces:

- `IInputLabelsLoader` — given `GameInfo`, returns labels for the ROM or null if it doesn't recognise it
- `IInputMappingSource` — given `GameInfo`, returns a partial mapping or null
- `IInputMappingTransform` — given the mapping a source already produced, returns an amended copy; this is how MAME's JOYCODE overrides apply without displacing the platform mapping underneath them

The owning service tries contributors in priority order until one returns data. Every contributor also answers `IsEnabled(GlobalConfig)`, so a user can switch a whole emulator integration off (`EnableMame`, `EnableRetroArch`) without the service knowing what a config flag is. Adding a new emulator is a folder under `Plugins/`, an implementation of the relevant interface, and a registration line in the corresponding factory — no service code changes.

### `Plugins/ControlsXml/`

Reads the BYOAC `controls.xml` database to supply labels for thousands of arcade ROMs. It sits in its own folder rather than under `Mame/` because the database is emulator-agnostic data; the source gates *itself* on the launched emulator being MAME, and is not tied to the `EnableMame` flag.

### `Plugins/Mame/`

Reads the per-game MAME `cfg/` files to detect JOYCODE remaps, contributed as an `IInputMappingTransform`. Only active when the configured emulator is MAME and `EnableMame` is set.

### `Plugins/RetroArch/`

Reads the RetroArch cfg cascade and per-game `.rmp` remaps to detect game-specific controller variant overrides and button swaps on top of the platform's base mapping. Only active when the configured emulator is RetroArch.

**What the plugin resolves vs. what it trusts** — `Controllers.xml` is treated as already incorporating all non-game-specific configuration: the joypad driver choice, core-level cfg overrides, and core-level remaps. The plugin only resolves game-specific overrides on top of that base. Button swaps are not re-derived from non-game-specific layers because resolving them requires knowing the physical controller's button layout via RetroArch's autoconfig — a per-hardware mapping the plugin cannot reliably reconstruct. Which controller type is active (`input_libretro_device_p1`) is the exception: it is an explicit discrete choice with no hardware dependency, so it is read from the full remap cascade (game → content-dir → core → common, first match wins).

## Other namespaces

The remaining namespaces under `Core/` aren't pipeline subsystems — they support the ones that are.

### `Composition/`

The wiring layer. One factory per top-level subsystem (`TemplateFactory`, `InputLabelsFactory`, `InputMappingFactory`, `InputRenderingFactory`, `ControllerOverlayFactory`) plus `ConfigLoader`. `ControllerOverlayFactory.Create(rootDir)` is the top — it builds the `LayeredFileSystem`, loads the merged `GlobalConfig`, and threads both into the per-subsystem factories. This is where the dependency graph lives — anything that needs a `new`, lives here.

### `Config/`

Plugin-wide configuration (`GlobalConfig`) deserialised from `GlobalConfig.xml` at startup — default template name, debug logging, and which plugin sources (MAME, RetroArch) are enabled. Loaded by `ConfigLoader` (in `Composition/`), which merges the `User/` file over the `Defaults/` one field-by-field (see [Config layering](#config-layering)). Read by `DynamicControlsPlugin` to configure the resolver.

### `Infrastructure/`

Cross-cutting utilities: `IFileSystem` (testable filesystem abstraction), `LayeredFileSystem` (the `Defaults/`+`User/` two-tier path resolver that wraps `IFileSystem` — see [Config layering](#config-layering)), `IApplicationData` (abstracts `%APPDATA%` lookup), `ILogger`, `ImageHeader` (PNG/JPEG header parser for image dimension reads without WPF), `FileUtils`, `RomNameUtils` (ROM-name normalisation, used for the fuzzy label lookup). Implementations are injected via the `Composition/` factories.

## Key design decisions

### Three-layer type model

Raw config DTOs → resolved domain types → built domain types. Each layer is immutable once the next is constructed. The boundary is enforced by type — raw configs are mutable `Node`/`Config` records; resolved types are immutable records exposing `IReadOnlyList` / `IReadOnlyDictionary`. See [conventions.md](conventions.md) for the full pattern. This is the most important design decision in the codebase because it forces every subsystem to make its raw→resolved boundary explicit.

### Cache once, read forever

Templates are loaded once per process and cached per template name. `ResolvedMapping` is constructed per game launch but reused across the render pass. `InputDescendants` and `CollapseInfo` are precomputed at template-load time and reused on every render. The result: a game launch's overlay costs about as much as rendering, not loading.

The cache is process-scoped, not LRU — we're assuming a single LaunchBox session uses a small handful of templates. Template edits require restarting LaunchBox; we don't watch the filesystem.

### Plugin sources as composable loaders

`IInputLabelsLoader` and `IInputMappingSource` are interfaces with an ordered list of implementations. The service tries each in priority order until one has data. Adding a new emulator's data source means adding a loader and registering it in the factory — no service code changes. This is how MAME, RetroArch, and per-game XML coexist.

### Visibility as a pipeline, not a flag

Each render decision asks "should this be visible *now*" with explicit modes (`label`, `mapped`, `auto`, `always`) rather than mutating opacity through a single global flag. `showIf="auto"` switching its meaning based on whether the game has its own labels is the trick that lets the same template work for both a game with labels and one without — the same `<Render>` element shows the controller button shape when there's no label, and dims out (deferring to the label) when there is. It also lets `MameControlsXmlSource` act as a button mask for Arcade games: because controls.xml supplies game-specific labels, `auto` resolves to `HasLabel` — only buttons the game actually uses are active. For platforms with custom images but no per-game labels (e.g. Sega Genesis default), `auto` resolves to `IsMapped` — all mapped buttons are active.

### Collapsing stacks as opt-in

`<Stack>` is normally a vertical layout of always-visible elements. Adding `collapse="true"` switches it into a mode where invisible (zero-opacity) members vacate their slot and subsequent members shift up. This produces a clean look when only some inputs from a cluster (e.g. arcade buttons) have labels — instead of seeing five labelled spots interspersed with empty space, you see the labelled spots tightly stacked. The data structure for this (`CollapseInfo`) is keyed by reference identity to handle the case where the same `OneOf` slot contains multiple `InputDefinition` alternatives.

### Coordinate origins as a stack

Coordinates in `Layout.xml` can be absolute (`x="100"`) or relative (`x="+5"`, `x="-10"`). `Coordinate.Resolve(origin)` applies the origin if relative, ignores it if absolute. The `LayoutResolver` threads the current origin (canvas → input → render) through the build context, so authors can write coordinates relative to the enclosing container without manually tracking absolute positions. The resolved layout has only absolute doubles — the relativity is a compile-time concept that doesn't survive into the rendered output.

### Subsystems as the unit of work and the unit of test

A subsystem is one phase of the pipeline (`Templates/`, `Labels/`, `InputMapping/`, `Rendering/`) with one `*Service` entry point and resolved-type input and output. Subsystems don't share state — one subsystem's output is the next one's input — so each one can be thought about, changed, and tested on its own. The test tiers follow this split:

- **Unit tests** (`tests/Core.Tests/`) — one class at a time, collaborators substituted
- **Subsystem tests** (`tests/Core.IntegrationTests/Subsystem/`) — one subsystem wired up for real; scenario data lives inline next to the assertions, so each test covers a focused case without a new fixture tree
- **End-to-end tests** (`tests/Core.IntegrationTests/EndToEnd/`) — the full pipeline against an on-disk `Fixtures/` tree, proving the wiring against real data

Roughly: subsystem tests are where you cover lots of scenarios, E2E tests are where you prove the wiring works against real data, and unit tests are where you pin edge cases for individual classes.

### Renderer doesn't know about platforms or emulators

The render pass takes `Template`, `ResolvedMapping` and `ResolvedLabels`. It doesn't know whether labels came from MAME, RetroArch, or a static XML; it doesn't know which controller is plugged in. All platform/emulator-specific knowledge has been baked into the resolved types by step 3. This keeps the renderer testable in isolation (see `Core.Tests/Templates/`) and lets contributors add new platforms without touching rendering code.

### Config layering

Plugin data is split into two trees under the root: `Defaults/` (shipped with the plugin, overwritten wholesale on every update) and `User/` (the user's own files, never touched by an update). Every loader reads through `LayeredFileSystem.Resolve(...segments)`, which returns the `User/` path when that file exists and the `Defaults/` path otherwise. The effect is that a user file *wholesale shadows* its `Defaults/` counterpart — a `User/Controllers/Sega Genesis.xml` replaces the shipped one entirely. This keeps customizations update-safe without a migration step: the updater only ever writes `Defaults/`.

`Templates/`, `Logs/`, and the RetroArch emulator config tree live at the root and bypass layering — templates ship fixed, logs are output, and RetroArch's own configs are read from the emulator install, not the plugin data folder.

Two files are **exceptions** to wholesale shadowing, both because the shadowing granularity would be wrong for them.

`GlobalConfig.xml` is merged per *field*. `ConfigLoader` deserialises `Defaults/GlobalConfig.xml` as a base, then overwrites only the fields whose elements are actually *present* in `User/GlobalConfig.xml` (detected with an `XmlDocument` pass over the child element names). Without this, a user file that sets a single field would let every omitted bool deserialise to `false` and silently clobber a shipped `true` default.

`Labels/{platform}.xml` is merged per *entry*. `InputLabelsLoader` reads the `Defaults/` and `User/` copies separately and overlays the user's `<Game>` entries onto the shipped ones (matched by `launchBoxId`, then `romName`), and the user's `<Defaults>` buttons onto the shipped ones by name. Because one file now holds every game on a platform, wholesale shadowing would mean labelling one game costs you the labels for all the others.

The full rationale for both lives in [config-layering.md](config-layering.md).

## Extension points

If you want to contribute, these are the most likely entry points and the conventions for each.

### Add a new emulator's data source

Add a folder under `src/Core/Plugins/{Emulator}/`. Implement `IInputLabelsLoader` (for labels) and/or `IInputMappingSource` (for mappings). Register in the relevant factory (`InputLabelsFactory`, `InputMappingFactory`). The loader receives `GameInfo`; it should return null for ROMs it doesn't recognise rather than throw.

### Add a new platform

Place a `Defaults/Controllers/{platform}.xml` with the controllers that exist for that platform. The one marked `default="true"` is used when nothing selects a variant (falling back to the first in document order if none is marked); users can override per-game via `InputMappings/{platform}/{rom}.xml`. Game labels and the platform's inheritable defaults share one file, `Labels/{platform}.xml`. A variant that extends another can `inheritFrom` it rather than restating its mappings — see [templates.md](templates.md) for the full inheritance rules. Shipped data goes under `Defaults/`; a user can shadow any of it from `User/` (see [Config layering](#config-layering)). No code changes are required — the resolution is platform-agnostic.

### Add a new template

Make a folder under `Templates/{templateName}/`. Drop a `BaseImage.png` for the controller's chassis. Write `Layout.xml` describing the slots — see [layout-xml-schema.md](layout-xml-schema.md) for the schema. Per-input images can live alongside (`ButtonA.png`) or in platform-specific subfolders (`Templates/{template}/{platform}/ButtonA.png`) for a platform-specific look. The template is loaded by name; nothing needs to be registered in code.

### Add a new layout container

Create a `*Node` raw DTO under `Templates/LayoutConfig.cs`, a resolved `ILayoutElement` type under `Templates/LayoutNodes.cs`, parsing in `TemplateLoader`, and a `Build*` method in `LayoutResolver`. Update `LayoutFilter` to handle the new type during visibility evaluation. The existing `<Group>`, `<Stack>`, `<OneOf>` types are good templates for the pattern.

### Add a new render condition

Add an entry to `ShowIfCondition`, the `ParseShowIf` switch in `LayoutResolver`, and the visibility logic in `VisibilityEvaluator`. Document the new mode in the `<Render>` schema. The renderer is the only consumer.

## What's outside the scope of this document

- The Windows/WPF plugin shell (`src/LaunchBox/`) — small enough to read directly
- Build/test pipeline — see the root README
- Code style and conventions — see [conventions.md](conventions.md)
- Per-class API documentation — XML doc comments are authoritative; this document is for the "why" and the data flow only
