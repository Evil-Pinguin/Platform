"""Рендерит вид из камеры на уровне TestLevel — чтобы проверять
проходимость, высоты и вид без запуска Unity.

Читает сцену как есть: позиции, размеры, порядок отрисовки и флипы
у всех SpriteRenderer. Правила размещения спрайтов — из их .meta:

  ground.png  верх-центр, DrawMode Tiled  — тайлим по ширине, верх = y
  block.png   центр,     DrawMode Tiled  — тайлим, верх = y + 1
  chasm.png   центр,     DrawMode Simple  — центр = y
  декор       низ-центр, DrawMode Simple  — низ = y
"""
import math
import re
import subprocess

S = 100                 # пикселей на юнит
HW, HH = 7.111, 4.0     # полуэкран при orthographic size 4 и 16:9
HUGE = '9999'

PNG = {
    '715ee93385374f0b96aa2d97ab5033cb': 'Assets/Art/Environment/ground.png',
    'b836230688a54cfe9c6e1d59eee2e42e': 'Assets/Art/Environment/block.png',
    '3d184408974c4b50961cd85749ce031c': 'Assets/Art/Environment/chasm.png',
    'e82b0731b1864b78a8e4394c0e57da08': 'Assets/Art/Environment/background_sky.png',
    'acd1ec0f7dd66712a76a7f4aa03de836': 'Assets/Art/Environment/Decor/spruce.png',
    'b017c5b7638bea95aacc23fe69524e10': 'Assets/Art/Environment/Decor/rocks.png',
    '5c2806664d4ba3f3f50b3741002bf34e': 'Assets/Art/Environment/Decor/grass_tuft.png',
    'dfefd0bca0374f584c131084affd95aa': 'Assets/Art/Environment/Decor/serge_pole.png',
    '76322453d221141f89f1cbb255c6d91f': 'Assets/Art/Environment/flower_lily.png',
    '6abdb613866b23b9b4e3eb024b21c8fd': 'Assets/Art/Environment/flower_lilies.png',
    'dd7b4a1d458ab880b373f467639af009': 'Assets/Art/Environment/flower_iris.png',
    '16e75b7cffae3121ece545242bee023b': 'Assets/Art/Environment/flower_campion.png',
    '8252fdaaf1543cb289b74cb3e8b5e17b': 'Assets/Art/Environment/tree_larch.png',
    'd0d17ec449fc6f2b17e3769108b6432c': 'Assets/Art/Environment/tree_birch.png',
    'b104a11421e6377350e663afdf1bcb1b': 'Assets/Art/Environment/tree_cluster.png',
    '33fe94647fdf3869ad5cd73fc249316d': 'Assets/Art/Environment/pillar.png',
    'c46dd459691fd1dd598775d241a19a84': 'Assets/Art/Heroine/Walk/walk_2.png',
    '2afd8a1670691ca60e7e3185170acb77': 'Assets/Art/Environment/midground.png',
}
HERO = '7a2d62ffd4fe461990b64ffe293dc930'   # walk_1

s = open('Assets/Scenes/TestLevel.unity', encoding='utf-8').read()
blocks = re.split(r'^--- ', s, flags=re.M)


def fb(cls, fid):
    for b in blocks:
        if re.match(rf'!u!{cls} &{fid}\b', b):
            return b
    return None


items = []          # (order, name, x, y, w, h, draw, flip, guid, parent)
for b in blocks:
    if not b.startswith('!u!1 &'):
        continue
    name = re.search(r'm_Name: (.*)', b).group(1).strip()
    comps = re.findall(r'\{fileID: (\d+)\}', re.search(
        r'm_Component:\n((?:  - component: \{fileID: \d+\}\n)+)', b).group(1))
    tid = sr = None
    for cid in comps:
        blk = fb(r'\d+', cid)
        if blk is None:
            continue
        kind = blk.split('\n')[1].rstrip(':')
        if kind == 'Transform':
            tid = cid
        elif kind == 'SpriteRenderer':
            sr = cid
    if not (tid and sr):
        continue
    t = fb('4', tid)
    p = re.search(r'm_LocalPosition: \{x: ([-\d.]+), y: ([-\d.]+), z: ([-\d.]+)\}', t)
    parent = re.search(r'm_Father: \{fileID: (\d+)\}', t).group(1)
    r = fb('212', sr)
    sz = re.search(r'm_Size: \{x: ([-\d.]+), y: ([-\d.]+)\}', r)
    g = re.search(r'm_Sprite: \{fileID: \d+, guid: ([0-9a-f]{32})', r)
    if not (sz and g):
        continue
    items.append((
        int(re.search(r'm_SortingOrder: (-?\d+)', r).group(1)), name,
        float(p.group(1)), float(p.group(2)),
        float(sz.group(1)), float(sz.group(2)),
        int(re.search(r'm_DrawMode: (\d+)', r).group(1)),
        int(re.search(r'm_FlipX: (\d+)', r).group(1)),
        g.group(1), int(parent)))
items.sort(key=lambda i: (i[0], i[1]))
print(f'спрайтов в сцене: {len(items)}')


def tile(img, unit_w, w):
    """Раскладывает тайл в полосу шириной w юнитов (как DrawMode Tiled)."""
    n = max(1, math.ceil(w / unit_w))
    strip = '/tmp/_strip.png'
    subprocess.run(['convert'] + [img] * n + ['+append', strip], check=True)
    subprocess.run(['convert', strip, '-crop', f'{int(round(w*S))}x{HUGE}',
                    '+repage', strip], check=True)
    return strip


def sprite_px(guid, w, h):
    """Растровый размер спрайта в пикселях экрана."""
    out = subprocess.run(['identify', '-format', '%w %h', PNG[guid]],
                         capture_output=True, text=True).stdout.split()
    return int(out[0]), int(out[1])


def render(camx, camy, herox, heroy, out):
    W, H = int(2 * HW * S), int(2 * HH * S)
    cx = lambda wx: (wx - camx + HW) * S
    cy = lambda wy: (camy + HH - wy) * S
    stack = []

    def put(img, ox, oy):
        iw, ih = map(int, subprocess.run(['identify', '-format', '%w %h', img],
                                 capture_output=True, text=True).stdout.split())
        x0, y0 = max(0, ox), max(0, oy)
        x1, y1 = min(W, ox + iw), min(H, oy + ih)
        if x1 <= x0 or y1 <= y0:
            return
        cut = f'/tmp/_cut_{len(stack)}.png'
        subprocess.run(['convert', img, '-crop', f'{x1-x0}x{y1-y0}+{x0-ox}+{y0-oy}',
                        '+repage', cut], check=True)
        stack.append((cut, x0, y0))

    for order, name, x, y, w, h, draw, flip, guid, parent in items:
        if guid not in PNG:      # кадры героини рисуем отдельно, из walk_1
            continue
        if parent:                          # ребёнок камеры — едет за ней
            x, y = camx + x, camy + y
        if draw == 2:                       # Tiled: тайлим по ширине
            unit = TILE_UNITS[guid]
            src = f'/tmp/_t_{name}.png'
            subprocess.run(['convert', PNG[guid], '-resize', f'{int(unit*S)}x',
                            src], check=True)
            img = tile(src, unit, w)
            ih = int(round(h * S))
            if guid in GROUND_IDS:
                top = int(cy(y))                 # земля: верх холста = y
            elif guid in MID_IDS:
                top = int(cy(y + h))             # берег: низ холста = y
            else:
                top = int(cy(y + h / 2))
            put(img, int(cx(x) - w * S / 2), top)
        elif guid in DECOR_IDS:             # низ по центру
            src = f'/tmp/_d_{name}.png'
            subprocess.run(['convert', PNG[guid], '-resize', f'{int(w*S)}x{int(h*S)}',
                            src], check=True)
            put(src, int(cx(x) - w * S / 2), int(cy(y) - h * S))
        else:                               # Simple, центр
            src = f'/tmp/_s_{name}.png'
            cmd = ['convert', PNG[guid], '-resize', f'{int(w*S)}x{int(h*S)}']
            subprocess.run(cmd + (['-flop'] if flip else []) + [src], check=True)
            put(src, int(cx(x) - w * S / 2), int(cy(y) - h * S / 2))

    hero = '/tmp/_hero.png'
    subprocess.run(['convert', 'Assets/Art/Heroine/Walk/walk_1.png',
                    '-resize', f'{int(1.289*S)}x{int(2*S)}', hero], check=True)
    put(hero, int(cx(herox)) - int(1.289 * S / 2), int(cy(heroy)) - int(2 * S))

    args = ['convert', '-size', f'{W}x{H}', 'xc:none']
    for img, x0, y0 in stack:
        args += [img, '-geometry', f'+{x0}+{y0}', '-composite']
    args.append(out)
    subprocess.run(args, check=True)
    print(f'  {name_last} -> {out}')


TILE_UNITS = {'715ee93385374f0b96aa2d97ab5033cb': 4.0,
              'b836230688a54cfe9c6e1d59eee2e42e': 1.0,
              '2afd8a1670691ca60e7e3185170acb77': 31.68}   # берег, тайл 3168 px / 100
GROUND_IDS = {'715ee93385374f0b96aa2d97ab5033cb', 'b836230688a54cfe9c6e1d59eee2e42e'}
MID_IDS = {'2afd8a1670691ca60e7e3185170acb77'}
DECOR_IDS = {'acd1ec0f7dd66712a76a7f4aa03de836', 'b017c5b7638bea95aacc23fe69524e10',
             '5c2806664d4ba3f3f50b3741002bf34e', 'dfefd0bca0374f584c131084affd95aa'}

VIEWS = [
    ('start',   -29.0, -0.27, -30.0, 0.031),
    ('climb',   -10.0,  1.50, -11.0, 1.831),
    ('pit',      4.5,  0.60,   8.0, 0.031),
    ('bridge',  23.5,  0.30,  23.5, 0.031),
    ('crest',   39.0,  2.20,  40.0, 2.431),
]
for name_last, camx, camy, herox, heroy in VIEWS:
    render(camx, camy, herox, heroy, f'/tmp/v_{name_last}.png')
