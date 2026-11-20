# MamePorts

Finds the cfg entries a newer MAME no longer accepts, and repairs them.

MAME loads a cfg entry only when it matches a port the driver defines, on **type, tag, mask and
default value**, and only when that port is enabled. A driver release that renames a port, moves
its bit, or changes its default makes MAME drop the entry silently the next time it saves the cfg,
so the game loses the binding. The pack's cfgs were written by older builds, so some of them no
longer work in the current MAME (0.289 at the time of writing).

## The tools

| Tool | What it does |
|---|---|
| `run.sh` | Dumps every port of every machine the pack has a cfg for, into `ports.xml`. About five hours. It skips any machine that already has a file in `ports/`, so to redo the lot (a new `ports.lua` attribute, a new MAME) move `ports/` aside first. |
| `ports.lua` | The dump itself, run inside MAME. Writes type, tag, mask, `defvalue`, `analog`, and `enabled="no"` for a port that is off under the default DIP settings. |
| `dump-roms.sh <roms.txt> <out-dir>` | Same dump for a chosen list, one file per machine, in minutes. |
| `rewrite.sh <cfg-dir> <roms.txt> <out-dir>` | Boots each machine in MAME against a **copy** of its cfg so MAME rewrites it and keeps only what it accepts. |
| `dropped.py <cfg-dir> <rewritten-dir> <roms.txt> [--dump dir]` | The entries in the original and not in MAME's rewrite: `{rom: [[type, tag, mask]]}`. For a machine MAME would not boot it predicts from the dump. |
| `fix.py plan\|apply` | Gives each dropped entry a fix (below), applies it with a backup, and renames the matching labels. |

`ports.xml` is committed as the dump of MAME 0.289, since remaking it takes hours; `run.sh` rewrites
it for another release.

## Updating for a new MAME release

1. **Find what breaks.** `rewrite.sh romcontrols/cfg all-roms.txt work/` over every cfg (hours), then
   `dropped.py romcontrols/cfg work/ all-roms.txt > dropped.json`. MAME's own rewrite is the ground
   truth. Comparing against `ports.xml` instead predicts it, but only when the comparison includes
   `defvalue` and `enabled`.
2. **Dump the ports of those machines.** `dump-roms.sh` on the ones in `dropped.json`.
3. **Plan.** `fix.py plan --cfg romcontrols/cfg --dump dumps/ --dropped dropped.json`.
4. **Apply** what is safe, with a backup: add `--backup bk/ --labels assets/Defaults/Platforms/Arcade/Labels.xml`.
   `--remove-gone` also deletes entries with no counterpart; check first that this does not cost a
   label (step 5).
5. **Verify**, in this order:
   - `rewrite.sh` again on the fixed cfgs. Nothing may be dropped.
   - `dotnet run --project tools/LabelDump -- --cfg <pack>`, once before the repair and once after,
     then compare the two `tools/LabelDump/output/arcade-pipeline-sorted.xml` files. Every
     difference must be one the repair meant to make.
   - A rom MAME will not boot here (`nowrite.txt`) can only be checked on a machine that runs it.

Run the LabelDump pass on a copy of the pack (cfg swapped, the rest symlinked) when trying a change,
because it overwrites `tools/LabelDump/output`; keep the earlier output aside to compare against.

## The fixes `fix.py` chooses

| Fix | When | Effect |
|---|---|---|
| default | The port is there, enabled, with another default | New `defvalue` |
| relocate | A coin, start or service port; or the type exists once and the old bit holds nothing | Keep the type and the joycodes; move tag, mask and default to where MAME puts that type |
| rename | One other enabled type now sits on the old bit, and the type is not also elsewhere (or it is the same kind and player) | Keep the bit and the joycodes; change the type, and rename that port in `Labels.xml` |
| gone | No counterpart | Reported; removed only with `--remove-gone` |
| review | The readings disagree, or two fixes land on one spot | Never applied |

**Rename keeps what the bit does; relocate keeps what the type means.** A button the driver
renumbered keeps its pad button, so it is a rename. Coin 1 stays coin 1 wherever the driver now puts
it, so coins always relocate.

## Traps

- **`ports.lua` must read each bit.** `port.fields` in MAME's Lua is keyed by field name, so two
  fields with one name (both players' "Spot") collapse into one; `port:field(mask)` returns one field
  per bit, so fields sharing bits hide each other. The script takes both and keeps one row per type
  and mask. A dump made by only one of them is missing ports.
- **A disabled port is dropped.** A `PORT_CONDITION` field that is off under the default DIPs
  (`enabled="no"`) loses its cfg entry on load. The flag can differ from run to run for the same
  machine, so treat it as a strong hint, not a verdict.
- **The default value counts.** A driver that changes an analog port's default (pedals, sticks)
  makes MAME drop the entry though type, tag and mask match.
- **A dump can still miss.** botss11's cfg loses every entry though most match the dump exactly, and
  wwallyja3p keeps a coin the dump says is another type. Trust `rewrite.sh`.
- **cfg files are CRLF with a BOM.** `fix.py` edits bytes in place; do not rewrite them with a tool
  that normalises line ends.
- **Unnamed or conditional ports on some machines are `TYPE_OTHER(n,m)`.** No cfg can bind them;
  `ports.lua` skips them.
- **MAME writes a cfg only when it exits cleanly.** A machine that never boots here has no rewrite;
  `dropped.py` predicts for it from the dump instead.
