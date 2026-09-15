# Config layering — defaults vs. user overrides

## Problem

The plugin's data folder (`…\LaunchBox\Data\Dynamic Controls\`) holds both **shipped defaults**
(controller vocabularies for ~50 platforms, a starter `GlobalConfig.xml`, labels) and a user's
**own customizations** (their settings, controller tweaks, per-game remaps). Today they live in the
same tree, so re-extracting the assets/ALL zip on update overwrites the user's work.

The fix: split the data folder into two layers and resolve **user-over-default** at load time, so a
download can refresh the defaults without ever touching what the user authored.

## Layout

```
Dynamic Controls\
  Defaults\          ← shipped; the assets/ALL zip overwrites this wholesale (no user data here)
    GlobalConfig.xml
    Controllers\{Platform}.xml
    InputMappings\{Platform}\{Rom}.xml
    Labels\{Platform}.xml
  User\              ← user-authored; no user file is ever in a zip or overwritten
                     (a README.txt skeleton ships in the folders — see Packaging impact)
    GlobalConfig.xml            (optional)
    Controllers\{Platform}.xml  (optional)
    InputMappings\{Platform}\{Rom}.xml (optional)
    Labels\{Platform}.xml        (optional)
    Static\{Platform}\{Rom}.png         (user-supplied; not layered)
  Templates\{Name}\  ← shipped, fixed; not overridable, so not part of the layers
```

`Defaults\` and `User\` are strictly the two **layers of the override mechanism** — only config
that a user can shadow lives there. Two folders sit outside that mechanism:
- **`Templates\`** — shipped and fixed; users can't override it, so it stays at the root (not in
  `Defaults\`). Its resolver path is unchanged.
- **`Static\`** — purely user-supplied per-game overlay images; there's no shipped default to
  override, so it lives under `User\` (no image is ever zipped or overwritten).

## Resolution

Every layered lookup checks **`User\<path>` first, then falls back to `Defaults\<path>`.** A loader
that today resolves `rootDir + relPath` instead resolves `rootDir\User\relPath` then
`rootDir\Defaults\relPath`.

The two non-layered folders resolve from a single location: `Templates\` from the root (unchanged),
`Static\` from `User\` (a missing static image just falls through to the normal rendering pipeline,
exactly as today).

## Per-type behaviour

| Config | User override? | Strategy | Granularity |
|---|---|---|---|
| `GlobalConfig.xml` | yes | **Merged** | per-setting |
| `Controllers\{Platform}.xml` | yes | **Overridden** | whole file, per platform |
| `InputMappings\{Platform}\{Rom}.xml` | yes | **Overridden** | whole file, per game |
| `Labels\{Platform}.xml` | yes | **Merged** | per entry (per game, per default button) |
| `Templates\` | no | shipped, fixed — root, not layered | — |
| `Static\` | n/a — user-only | user content under `User\`, no shipped default | per game image |

**`GlobalConfig.xml` and `Labels\{Platform}.xml` are merged**; everything else is a **whole-file
override** (the user's file for that platform/game replaces the default; if absent, the default is
used). Whole-file override is the right granularity for `Controllers\` and `InputMappings\` — each
file describes exactly one platform or one game, so replacing it is precisely the customization the
user meant — and it avoids any cross-file merge logic. The two merged files are merged because each
one holds *many* independent settings, so replacing the file would discard far more than the user
intended to change.

## GlobalConfig merge details

`GlobalConfig.xml` is a single global file, so a whole-file override would force users to restate
every setting. Instead: **load `Defaults\GlobalConfig.xml`, then overlay only the settings that are
*present* in `User\GlobalConfig.xml`.** A user who only wants a different template writes:

```xml
<Config>
  <DefaultTemplate>My Controller</DefaultTemplate>
</Config>
```

and the other defaults (incl. future new ones) still apply.

> **Implementation note:** the overlay must merge by **element presence**, not by deserializing the
> user file into a `GlobalConfig` and copying fields. Plain deserialization fills absent elements
> with type defaults (e.g. an omitted `<EnableRetroArch>` becomes `false`), which would silently
> override a shipped `true`. Read which elements the user file actually contains and override only
> those.

## Labels merge details

`Labels\{Platform}.xml` holds every game on the platform in one file — a `<Defaults>` block plus a
`<Game>` element per title. Whole-file override would mean that adding labels for one game silently
drops the shipped labels for every other game on that platform, which is never what the user wanted.

So the loader reads **both** copies and overlays entry-by-entry:

- `<Game>` entries are matched by `launchBoxId` first, then by `romName`; a user entry that matches a
  shipped one replaces it, and an unmatched user entry is added.
- `<Defaults>` button entries are overlaid by element name — a user `<Start>` replaces the shipped
  `<Start>`, and shipped buttons the user didn't mention survive.

The merge is per entry, not per label: a user `<Game>` entry replaces the shipped entry for that game
outright rather than merging button-by-button within it. Redefining a game means redefining its
buttons — which keeps "I want this game to show *these* labels" expressible, instead of leaving the
user unable to delete a shipped label.

## Packaging impact

- The **assets** and **ALL** release zips ship **`Defaults\` and `Templates\`**, and never any
  user-authored file under `User\` (which is where the user's overrides *and* their `Static\`
  images live). Re-extracting on update is therefore always safe.
- The zips *do* carry a `User\` skeleton: the empty subfolders plus a `README.txt` in each
  explaining what belongs there. Those README files are the only thing an update overwrites under
  `User\`, and they hold no user data — so keep them documentation-only and never write config
  into that path.
- Absence of `User\` is still handled — layered lookups fall through to `Defaults\`, and a missing
  `User\Static\` image just renders normally.

## Migration

Pre-1.0, so no shim: adopt the new layout directly. Release notes tell existing users to move any
customizations from the old flat `Config\`/`Data\` into `User\`. (Their old files are otherwise
inert, since loaders now read `User\`/`Defaults\`.)

## Out of scope

- `Templates\` user overrides — shipped-only for now; revisit if users want custom artwork.
- Per-entry merging of `Controllers`/`InputMappings` — whole-file override is sufficient for the
  one-platform-or-one-game files; finer merging can be added later if a real need appears.
  (`Labels` started here and moved out — see *Labels merge details* above — once one file per
  platform made whole-file override lossy.)
