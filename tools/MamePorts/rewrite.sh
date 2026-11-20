#!/bin/sh
# Boot each machine in the installed MAME against a COPY of its cfg, so MAME rewrites the copy on
# exit and keeps only the entries it accepts for that machine.
#   tools/MamePorts/rewrite.sh <cfg-dir> <roms.txt> <out-dir>
# Writes <out-dir>/cfg/<rom>.cfg (the rewritten copies) and <out-dir>/nowrite.txt (machines whose
# copy was not rewritten: they would not start, or MAME found nothing to change).
# Set MAME_ROMPATH to a real ROM set to boot machines the zero-filled stand-ins cannot start.
# The originals in <cfg-dir> are never touched. Compare with dropped.py. A machine that finished leaves
# a marker in <out-dir>/done, so an interrupted run resumes where it stopped.
set -u
cfgdir=$1
list=$2
out=$3
mkdir -p "$out/cfg" "$out/roms" "$out/done"
touch "$out/nowrite.txt"
here=$(cd "$(dirname "$0")" && pwd)
. "$here/disks.sh"
blank_disks_init "$out"
while read -r rom; do
	[ -z "$rom" ] && continue
	[ -e "$out/done/$rom" ] && continue   # finished by an earlier run: running again resumes
	cp "$cfgdir/$rom.cfg" "$out/cfg/$rom.cfg"
	rm -rf "$out/roms/$rom" && mkdir -p "$out/roms/$rom"
	mame -listroms "$rom" 2>/dev/null | sed -nE 's/^(.*[^ ]) +([0-9]+) +(CRC|NO GOOD|BAD).*/\1	\2/p' |
		while IFS='	' read -r file size; do
			mkdir -p "$out/roms/$rom/$(dirname "$file")"
			head -c "$size" /dev/zero > "$out/roms/$rom/$file"
		done
	place_blank_disks "$out" "$rom" "$out/roms/$rom"
	before=$(stat -f %m "$out/cfg/$rom.cfg" 2>/dev/null || stat -c %Y "$out/cfg/$rom.cfg")
	SDL_VIDEODRIVER=dummy timeout -k 5 30 mame "$rom" -rompath "${MAME_ROMPATH:+$MAME_ROMPATH;}$out/roms" -video none -sound none -nothrottle \
		-skip_gameinfo -seconds_to_run 8 -cfg_directory "$out/cfg" -nvram_directory "$out" \
		-diff_directory "$out" -snapshot_directory "$out" >/dev/null 2>&1
	after=$(stat -f %m "$out/cfg/$rom.cfg" 2>/dev/null || stat -c %Y "$out/cfg/$rom.cfg")
	[ "$before" != "$after" ] || echo "$rom" >> "$out/nowrite.txt"
	rm -rf "$out/roms/$rom"
	touch "$out/done/$rom"
done < "$list"
