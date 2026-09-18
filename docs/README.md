# Documentation

These documents are for people working **on** the plugin. To install, configure or use it, read the [project README](../README.md) instead.

**New to the codebase, start with [architecture.md](architecture.md)** — it explains what the plugin does and how a game launch flows through it, and everything else assumes that shape.

## Find the document by what you want to do

| I want to… | Read |
|---|---|
| Understand how the plugin turns a game launch into an overlay | [architecture.md](architecture.md) |
| Know the code style, type patterns and test tiers before writing C# | [conventions.md](conventions.md) |
| Add button art for a platform, or build a new controller template | [templates.md](templates.md) |
| Look up a `Layout.xml` element, attribute or `showIf` mode | [layout-xml-schema.md](layout-xml-schema.md) |
| Change where a data file loads from, or how two copies of one combine | [config-layering.md](config-layering.md) |
| Check which docs to update alongside a change | [conventions.md](conventions.md#keeping-documentation-in-step) |

## The documents

| Document | Written for | Shape |
|---|---|---|
| [architecture.md](architecture.md) | Anyone changing code | Read through once |
| [conventions.md](conventions.md) | Contributors writing C# or docs | Read once, then refer back |
| [templates.md](templates.md) | Artwork and platform contributors — **no C# needed** | Task guide |
| [layout-xml-schema.md](layout-xml-schema.md) | Template authors | Reference — look things up |
| [config-layering.md](config-layering.md) | Anyone touching file loading or merging | Reference |

`CLAUDE.md` in the repository root covers the same ground for AI agents working in this codebase. It is deliberately terse and assumes these documents exist; it is not a substitute for them.
