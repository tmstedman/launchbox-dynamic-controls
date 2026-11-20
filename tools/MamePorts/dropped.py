#!/usr/bin/env python3
"""List the cfg entries MAME drops.

    dropped.py <cfg-dir> <rewritten-dir> <roms.txt> [--dump <dump-dir>] > dropped.json

<rewritten-dir> is the output of rewrite.sh (it holds cfg/ and nowrite.txt). For each machine the
entries present in the original cfg and missing from MAME's rewrite are the dropped ones. For a
machine MAME did not rewrite (see nowrite.txt) the drops are predicted instead, from the port dump
in --dump: an entry is predicted dropped when the dump has no enabled port with that type, tag, mask
and default value. That prediction matched MAME on 282 of the 288 entries it was checked against.

Output is JSON: {rom: [[type, tag, mask], ...]}, tag without its leading colon.
"""
import json, os, re, sys

SKIP = {'DIPSWITCH', 'CONFIG', 'ADJUSTER'}  # settings, not controls
PORT = re.compile(r'<port tag="([^"]*)" type="([^"]*)" mask="(\d+)"(?: defvalue="(-?\d+)")?')
DUMP = re.compile(r'type="([^"]*)" tag="([^"]*)" mask="(\d+)"(?: defvalue="(-?\d+)")?( analog="yes")?( enabled="no")?')


def cfg_ports(path):
    text = open(path, encoding='utf-8-sig').read()
    return [(m.group(2), m.group(1).lstrip(':'), m.group(3), m.group(4)) for m in PORT.finditer(text) if m.group(2) not in SKIP]


def dump_rows(path):
    rows = {}
    for line in open(path):
        m = DUMP.search(line)
        if m:
            rows[(m.group(1), m.group(2), m.group(3))] = (m.group(4), not m.group(6))
    return rows


def main(argv):
    args = [a for a in argv if not a.startswith('--')]
    dump_dir = argv[argv.index('--dump') + 1] if '--dump' in argv else None
    if dump_dir:
        args.remove(dump_dir)
    cfg_dir, rewritten, roms = args[:3]
    nowrite = set(open(os.path.join(rewritten, 'nowrite.txt')).read().split())
    result = {}
    for rom in open(roms).read().split():
        original = cfg_ports(os.path.join(cfg_dir, rom + '.cfg'))
        if rom not in nowrite:
            kept = {(t, g, m) for t, g, m, _ in cfg_ports(os.path.join(rewritten, 'cfg', rom + '.cfg'))}
            dropped = [(t, g, m) for t, g, m, _ in original if (t, g, m) not in kept]
        elif dump_dir and os.path.exists(os.path.join(dump_dir, rom + '.xml')):
            rows = dump_rows(os.path.join(dump_dir, rom + '.xml'))
            dropped = []
            for t, g, m, dv in original:
                row = rows.get((t, g, m))
                if row is None or not row[1] or (dv is not None and row[0] not in (None, dv)):
                    dropped.append((t, g, m))
            print(f'{rom}: MAME did not rewrite it; drops predicted from the dump', file=sys.stderr)
        else:
            print(f'{rom}: MAME did not rewrite it and there is no dump; skipped', file=sys.stderr)
            continue
        if dropped:
            result[rom] = [list(x) for x in dropped]
    print(f'{len(result)} machines drop entries ({sum(map(len, result.values()))} in all)', file=sys.stderr)
    json.dump(result, sys.stdout, indent=1)


if __name__ == '__main__':
    main(sys.argv[1:])
