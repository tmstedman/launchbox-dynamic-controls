#!/usr/bin/env python3
"""Plan, and apply, repairs for the cfg entries a newer MAME drops.

    fix.py plan  --cfg <cfg-dir> --dump <dump-dir> --dropped dropped.json
    fix.py apply --cfg <cfg-dir> --dump <dump-dir> --dropped dropped.json \\
                 --backup <dir> [--labels <Labels.xml>] [--remove-gone]

<dump-dir> holds one <rom>.xml per machine from dump-roms.sh (it must carry defvalue and enabled).
dropped.json comes from dropped.py. Only entries listed there are touched. Each is given one fix:

  default   the port is there but its default value changed        -> new defvalue
  relocate  the type exists once, elsewhere (a coin, start or      -> keep the type and the joycodes, move
            service port; or nothing else sits on the old bit)        tag, mask and defvalue to where it is
  rename    one other enabled type now sits on the same bit        -> keep the bit and the joycodes, change
                                                                      the type (and rename its label)
  gone      no counterpart                                         -> reported; removed only with --remove-gone
  review    two readings disagree, or two fixes land on one spot   -> reported, never applied

Rename keeps what the bit does (a button renumbered by the driver keeps its pad button). Relocate keeps
what the type means (coin 1 stays coin 1), which is why coin, start and service ports always relocate.
"""
import argparse, collections, json, os, re, shutil, sys

SKIP = {'DIPSWITCH', 'CONFIG', 'ADJUSTER'}
PORT = re.compile(rb'<port tag="([^"]*)" type="([^"]*)" mask="(\d+)" defvalue="(-?\d+)"')
DUMP = re.compile(r'type="([^"]*)" tag="([^"]*)" mask="(\d+)" defvalue="(-?\d+)"( analog="yes")?( enabled="no")?')
ALWAYS_RELOCATE = re.compile(r'^(COIN\d|START\d|SERVICE\d?)$')


def family(t):
    """BUTTON, JOYSTICK, ... : the type without its player prefix, direction or trailing number."""
    return re.sub(r'(_(UP|DOWN|LEFT|RIGHT|X|Y|Z|V|H))?\d*$', '', re.sub(r'^P\d_', '', t))


def player(t):
    m = re.match(r'^P(\d)_', t)
    return m.group(1) if m else ''


def dump(path):
    rows = []
    for line in open(path):
        m = DUMP.search(line)
        if m:
            rows.append(dict(type=m.group(1), tag=m.group(2), mask=m.group(3), dv=m.group(4), on=not m.group(6)))
    return rows


def plan_rom(rom, data, rows, dropped):
    ops = []
    by_bit = collections.defaultdict(list)
    by_type = collections.defaultdict(list)
    for r in rows:
        if r['on']:
            by_bit[(r['tag'], r['mask'])].append(r)
            by_type[r['type']].append(r)
    for m in PORT.finditer(data):
        tag, ty, mask, dv = [x.decode() for x in m.groups()]
        tg = tag.lstrip(':')
        if [ty, tg, mask] not in dropped:
            continue
        span = m.span()
        same = [r for r in by_bit[(tg, mask)] if r['type'] == ty]
        if same:  # the port is there and enabled: only its default can differ
            ops.append(dict(span=span, old=(ty, tg, mask), kind='default', new=(ty, tg, mask, same[0]['dv'])))
            continue
        alt = [r for r in by_bit[(tg, mask)] if r['type'] != ty]
        elsewhere = by_type[ty]
        if ALWAYS_RELOCATE.match(ty) and len(elsewhere) == 1:
            kind, r = 'relocate', elsewhere[0]
            new = (ty, r['tag'], r['mask'], r['dv'])
        elif len(alt) == 1 and (not elsewhere or (family(alt[0]['type']) == family(ty) and player(alt[0]['type']) == player(ty))):
            kind, r = 'rename', alt[0]
            new = (r['type'], tg, mask, r['dv'])
        elif len(elsewhere) == 1 and not alt:
            kind, r = 'relocate', elsewhere[0]
            new = (ty, r['tag'], r['mask'], r['dv'])
        elif not alt and not elsewhere:
            ops.append(dict(span=span, old=(ty, tg, mask), kind='gone', new=None))
            continue
        else:
            ops.append(dict(span=span, old=(ty, tg, mask), kind='review', new=None,
                            why=f'bit holds {[a["type"] for a in alt]}, type is also at {[(e["tag"], e["mask"]) for e in elsewhere][:3]}'))
            continue
        ops.append(dict(span=span, old=(ty, tg, mask), kind=kind, new=new))
    # two fixes landing on one spot, or on a spot another entry already holds, are not safe to apply
    final = collections.Counter()
    moving = {o['span'] for o in ops}
    for m in PORT.finditer(data):
        if m.span() not in moving:
            final[(m.group(2).decode(), m.group(1).decode().lstrip(':'), m.group(3).decode())] += 1
    for o in ops:
        if o['new']:
            final[(o['new'][0], o['new'][1].lstrip(':'), o['new'][2])] += 1
    for o in ops:
        if o['new'] and final[(o['new'][0], o['new'][1].lstrip(':'), o['new'][2])] > 1:
            o.update(kind='review', why='another entry would sit on the same type, tag and mask', new=None)
    return ops


def rename_labels(text, rom, renames):
    """Rename port names inside one <Game>, all at once so a swap does not chain."""
    m = re.search(r'(<Game [^>]*romName="%s"[^>]*>)(.*?)(</Game>)' % re.escape(rom), text, re.S)
    if not m or not renames:
        return text

    def one(tok):
        base = re.sub(r'\.(inc|dec)$', '', tok)
        return renames[base] + tok[len(base):] if base in renames else tok

    body = re.sub(r'name="([^"]*)"', lambda n: 'name="%s"' % ' '.join(one(t) for t in n.group(1).split()), m.group(2))
    return text[:m.start(2)] + body + text[m.end(2):]


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('mode', choices=['plan', 'apply'])
    ap.add_argument('--cfg', required=True)
    ap.add_argument('--dump', required=True)
    ap.add_argument('--dropped', required=True)
    ap.add_argument('--backup')
    ap.add_argument('--labels')
    ap.add_argument('--remove-gone', action='store_true')
    ap.add_argument('roms', nargs='*', help='limit to these machines')
    a = ap.parse_args()
    dropped = json.load(open(a.dropped))
    labels = open(a.labels, encoding='utf-8').read() if a.labels else None
    counts = collections.Counter()
    for rom in sorted(dropped):
        if a.roms and rom not in a.roms:
            continue
        path = os.path.join(a.cfg, rom + '.cfg')
        dpath = os.path.join(a.dump, rom + '.xml')
        if not os.path.exists(dpath):
            print(f'{rom}: no dump, skipped')
            continue
        data = open(path, 'rb').read()
        ops = plan_rom(rom, data, dump(dpath), dropped[rom])
        print(rom)
        for o in ops:
            counts[o['kind']] += 1
            ty, tg, mask = o['old']
            tail = f" -> {o['new'][0]} {o['new'][1]}:{o['new'][2]} (default {o['new'][3]})" if o['new'] else (f" ({o['why']})" if o.get('why') else '')
            print(f"   {o['kind']:8} {ty} {tg}:{mask}{tail}")
        if a.mode != 'apply':
            continue
        if not a.backup:
            sys.exit('apply needs --backup')
        os.makedirs(a.backup, exist_ok=True)
        shutil.copy(path, os.path.join(a.backup, rom + '.cfg'))
        renames = {}
        for o in sorted(ops, key=lambda o: o['span'], reverse=True):
            s, e = o['span']
            if o['kind'] in ('default', 'relocate', 'rename'):
                ty, tag, mask, dv = o['new']
                tag = tag if tag.startswith(':') else ':' + tag
                data = data[:s] + f'<port tag="{tag}" type="{ty}" mask="{mask}" defvalue="{dv}"'.encode() + data[e:]
                if o['kind'] == 'rename':
                    renames[o['old'][0]] = ty
                    renames[f"{o['old'][0]}:{o['old'][1]}:{o['old'][2]}"] = ty
            elif o['kind'] == 'gone' and a.remove_gone:
                # the whole <port> block, with its indent and line end
                head = data.rfind(b'\n', 0, s) + 1
                end = data.find(b'</port>', s) + len(b'</port>')
                end = data.find(b'\n', end) + 1
                data = data[:head] + data[end:]
        open(path, 'wb').write(data)
        if labels is not None:
            labels = rename_labels(labels, rom, renames)
    if a.mode == 'apply' and labels is not None:
        open(a.labels, 'w', encoding='utf-8').write(labels)
    print(dict(counts))


if __name__ == '__main__':
    main()
