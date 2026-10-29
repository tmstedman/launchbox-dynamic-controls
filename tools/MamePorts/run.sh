#!/bin/sh
# Dump the ports of every machine the pack has a cfg for, into ports.xml.
#   tools/MamePorts/run.sh [cfg dir, default romcontrols/cfg]
#   MAME_ROMPATH=/Volumes/Arcade tools/MamePorts/run.sh   (a real set, searched first)
# No ROM set is needed: each machine boots from zero-filled files of the right names and sizes.
# MAME refuses a missing ROM but only warns about a wrong one, and the port list comes from
# the driver, not the ROM's contents. A disk image (CHD) gets the same treatment, a blank hard
# disk or CD made with chdman (Homebrew's rom-tools) and copied in under the disk's name. A machine
# can still hang booting from zeros; MAME_ROMPATH lets a real set boot it instead.
# Machines that fail to start are listed in failed.txt.
set -u
here=$(cd "$(dirname "$0")" && pwd)
cfgdir=${1:-romcontrols/cfg}
mkdir -p "$here/ports"
: > "$here/failed.txt"
# MAME writes its own cfg, nvram, disk diffs and snapshots; keep them, and the dummy ROMs, out of the repo.
scratch=$(mktemp -d)
trap 'rm -rf "$scratch"' EXIT
if command -v chdman >/dev/null; then
	chdman createhd -o "$scratch/blank-hd.chd" -chs 1024,16,63 -ss 512 -f >/dev/null 2>&1
	head -c 4194304 /dev/zero > "$scratch/blank.iso"
	chdman createcd -i "$scratch/blank.iso" -o "$scratch/blank-cd.chd" -f >/dev/null 2>&1
	chdman createdvd -i "$scratch/blank.iso" -o "$scratch/blank-dvd.chd" -f >/dev/null 2>&1
fi
for cfg in "$cfgdir"/*.cfg; do
	rom=$(basename "$cfg" .cfg)
	[ "$rom" = default ] && continue
	[ -s "$here/ports/$rom.xml" ] && continue
	rm -rf "$scratch/roms" && mkdir -p "$scratch/roms/$rom"
	# File names can hold spaces ("nes-gl-0 prg"), so split on the size column, not on whitespace.
	mame -listroms "$rom" 2>/dev/null | sed -nE 's/^(.*[^ ]) +([0-9]+) +(CRC|NO GOOD|BAD).*/\1	\2/p' |
		while IFS='	' read -r file size; do
			mkdir -p "$scratch/roms/$rom/$(dirname "$file")"
			head -c "$size" /dev/zero > "$scratch/roms/$rom/$file"
		done
	# A drive refuses an image of another kind. The region names the drive, though not always as
	# "cdrom" (Konami's "runtime", the CD32's "akiko", the "cr589"), so anything not a disk, card or
	# DVD is taken as a CD. A laserdisc needs real video and is left to MAME_ROMPATH.
	[ -f "$scratch/blank-hd.chd" ] && mame -listxml "$rom" 2>/dev/null |
		sed -nE 's/.*<disk name="([^"]+)".* region="([^"]+)".*/\1	\2/p' |
		while IFS='	' read -r disk region; do
			case "$region" in
				*hdd* | *harddisk* | *pccard* | *cfcard*) blank=blank-hd ;;
				*dvd*) blank=blank-dvd ;;
				*laserdisc*) continue ;;
				*) blank=blank-cd ;;
			esac
			cp "$scratch/$blank.chd" "$scratch/roms/$rom/$disk.chd"
		done
	# SDL's dummy driver stops MAME registering as a windowed app, which on macOS takes focus
	# from the desktop on every launch even with -video none.
	SDL_VIDEODRIVER=dummy MAME_PORTS_OUT="$here/ports/$rom.xml" mame "$rom" -rompath "${MAME_ROMPATH:+$MAME_ROMPATH;}$scratch/roms" \
		-autoboot_script "$here/ports.lua" -video none -sound none -nothrottle \
		-skip_gameinfo -seconds_to_run 10 \
		-cfg_directory "$scratch" -nvram_directory "$scratch" -diff_directory "$scratch" -snapshot_directory "$scratch" >/dev/null 2>&1 &
	pid=$!
	# -seconds_to_run counts emulated time, so a machine hung before its clock moves (ccastles)
	# never reaches it; this wall-clock limit is the real backstop.
	( sleep 30; kill -9 "$pid" 2>/dev/null ) &
	watchdog=$!
	wait "$pid"
	kill "$watchdog" 2>/dev/null
	[ -s "$here/ports/$rom.xml" ] || echo "$rom" >> "$here/failed.txt"
done
mame -version | sed 's/^/<!-- MAME /; s/$/ -->/' > "$here/ports.xml"
echo "<machines>" >> "$here/ports.xml"
cat "$here"/ports/*.xml >> "$here/ports.xml"
echo "</machines>" >> "$here/ports.xml"
echo "$(ls "$here"/ports | wc -l | tr -d ' ') dumped, $(wc -l < "$here/failed.txt" | tr -d ' ') failed (failed.txt)"
