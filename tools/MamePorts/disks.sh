# Sourced by rewrite.sh and dump-roms.sh. A machine with a disk (CHD) will not start without one, so give
# it a blank of the right kind, the same way run.sh does.
#   blank_disks_init <dir>              make the blank hard disk, CD and DVD in <dir> (needs chdman)
#   place_blank_disks <dir> <rom> <to>  copy the right blank into <to> under each disk name <rom> asks for
blank_disks_init() {
	command -v chdman >/dev/null || return 0
	chdman createhd -o "$1/blank-hd.chd" -chs 1024,16,63 -ss 512 -f >/dev/null 2>&1
	head -c 4194304 /dev/zero > "$1/blank.iso"
	chdman createcd -i "$1/blank.iso" -o "$1/blank-cd.chd" -f >/dev/null 2>&1
	chdman createdvd -i "$1/blank.iso" -o "$1/blank-dvd.chd" -f >/dev/null 2>&1
}
place_blank_disks() {
	[ -f "$1/blank-hd.chd" ] || return 0
	mame -listxml "$2" 2>/dev/null | sed -nE 's/.*<disk name="([^"]+)".* region="([^"]+)".*/\1	\2/p' |
		while IFS='	' read -r disk region; do
			case "$region" in
				*hdd* | *harddisk* | *pccard* | *cfcard*) blank=blank-hd ;;
				*dvd*) blank=blank-dvd ;;
				*laserdisc*) continue ;;
				*) blank=blank-cd ;;
			esac
			cp "$1/$blank.chd" "$3/$disk.chd"
		done
}
