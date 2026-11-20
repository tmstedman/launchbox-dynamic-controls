#!/bin/sh
# Dump the ports of a list of machines, one file per machine, using ports.lua.
#   tools/MamePorts/dump-roms.sh <roms.txt> <out-dir>
# Same method as run.sh (zero-filled ROMs, no real set needed), but for a chosen list and into a
# directory of your choosing, so a handful of machines can be re-dumped in minutes instead of hours.
# Each <out-dir>/<rom>.xml carries type, tag, mask, defvalue and, where it applies, analog and
# enabled="no". A machine that does not start leaves no file.
set -u
here=$(cd "$(dirname "$0")" && pwd)
list=$1
out=$2
mkdir -p "$out"
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
. "$here/disks.sh"
blank_disks_init "$scratch"
while read -r rom; do
	[ -z "$rom" ] && continue
	rm -rf "$scratch/roms" && mkdir -p "$scratch/roms/$rom"
	mame -listroms "$rom" 2>/dev/null | sed -nE 's/^(.*[^ ]) +([0-9]+) +(CRC|NO GOOD|BAD).*/\1	\2/p' |
		while IFS='	' read -r file size; do
			mkdir -p "$scratch/roms/$rom/$(dirname "$file")"
			head -c "$size" /dev/zero > "$scratch/roms/$rom/$file"
		done
	place_blank_disks "$scratch" "$rom" "$scratch/roms/$rom"
	SDL_VIDEODRIVER=dummy MAME_PORTS_OUT="$out/$rom.xml" timeout -k 5 40 mame "$rom" \
		-rompath "${MAME_ROMPATH:+$MAME_ROMPATH;}$scratch/roms" \
		-autoboot_script "$here/ports.lua" -video none -sound none -nothrottle -skip_gameinfo -seconds_to_run 10 \
		-cfg_directory "$scratch" -nvram_directory "$scratch" -diff_directory "$scratch" -snapshot_directory "$scratch" >/dev/null 2>&1
	[ -s "$out/$rom.xml" ] || echo "no dump: $rom" >&2
done < "$list"
