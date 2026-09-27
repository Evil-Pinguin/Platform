"""Рендерит вид из камеры на уровне TestLevel — чтобы увидеть результат
без запуска Unity. Геометрия берётся из самой сцены, не «на глаз»."""
import re, subprocess

S = 100               # пикселей на юнит
HW, HH = 7.111, 4.0   # полуэкран при orthographic size 4 и 16:9

G_GROUND = '715ee93385374f0b96aa2d97ab5033cb'
G_BLOCK  = 'b836230688a54cfe9c6e1d59eee2e42e'
G_CHASM  = '3d184408974c4b50961cd85749ce031c'

s = open('Assets/Scenes/TestLevel.unity', encoding='utf-8').read()
blocks = re.split(r'^--- ', s, flags=re.M)

def find_block(cls, fid):
    for b in blocks:
        if re.match(rf'!u!{cls} &{fid}\b', b):
            return b
    raise KeyError(f'{cls} {fid} не найден')

objs = {re.search(r'm_Name: (.*)', b).group(1).strip(): re.match(r'!u!1 &(\d+)', b).group(1)
        for b in blocks if b.startswith('!u!1 &')}

def info(name):
    go = find_block('1', objs[name])
    comp = re.search(r'm_Component:\n((?:  - component: \{fileID: \d+\}\n)+)', go).group(1)
    x = y = 0.0
    w = h = 1.0
    guid = None
    for cid in re.findall(r'\{fileID: (\d+)\}', comp):
        b = find_block(r'\d+', cid)
        kind = b.split('\n')[1].rstrip(':')
        if kind == 'Transform':
            p = re.search(r'm_LocalPosition: \{x: ([-\d.]+), y: ([-\d.]+)', b)
            x, y = float(p.group(1)), float(p.group(2))
        elif kind == 'SpriteRenderer':
            sz = re.search(r'm_Size: \{x: ([-\d.]+), y: ([-\d.]+)\}', b)
            w, h = float(sz.group(1)), float(sz.group(2))
            guid = re.search(r'm_Sprite: \{fileID: \d+, guid: ([0-9a-f]{32})', b).group(1)
    return x, y, w, h, guid

for src, args, dst, note in [
    ('Assets/Art/Environment/ground.png', ['-resize', f'{int(4*S)}x'],
     '/tmp/gt.png', 'ground.png — ширина 4 юнита'),
    ('Assets/Art/Environment/block.png', ['-resize', f'{int(1*S)}x'],
     '/tmp/bt.png', 'block.png — 1 юнит'),
]:
    subprocess.run(['convert', src] + args + [dst], check=True)
subprocess.run(['convert', 'Assets/Art/Heroine/Walk/walk_1.png',
                '-resize', f'{int(1.289*S)}x{int(2*S)}', '/tmp/hh.png'], check=True)


def render(camx, camy, herox, out):
    W, H = int(2 * HW * S), int(2 * HH * S)
    cx = lambda wx: (wx - camx + HW) * S
    cy = lambda wy: (camy + HH - wy) * S

    subprocess.run(['convert', 'Assets/Art/Environment/background_sky.png',
                    '-resize', f'{int(16.78*S)}x{int(9.37*S)}!',
                    '-gravity', 'center', '-crop', f'{W}x{H}+0+0', '+repage',
                    '/tmp/bgc.png'], check=True)
    stack = [('/tmp/bgc.png', 0, 0)]

    def put(img, ox, oy, tag):
        """Обрезаем по холсту отдельным вызовом: в общей команде -crop
        применился бы ко всем накопленным слоям и схлопнул список."""
        iw, ih = map(int, subprocess.run(['identify', '-format', '%w %h', img],
                                 capture_output=True, text=True).stdout.split())
        x0, y0 = max(0, ox), max(0, oy)
        x1, y1 = min(W, ox + iw), min(H, oy + ih)
        if x1 <= x0 or y1 <= y0:
            return
        cut = f'/tmp/cut_{tag}.png'
        subprocess.run(['convert', img, '-crop', f'{x1-x0}x{y1-y0}+{x0-ox}+{y0-oy}',
                        '+repage', cut], check=True)
        stack.append((cut, x0, y0))

    for name, (x, y, w, h, guid) in ((n, info(n)) for n in objs):
        if guid not in (G_GROUND, G_BLOCK, G_CHASM):
            continue
        px = int(cx(x))
        if guid == G_CHASM:
            # у каждого провала своя ширина — растягиваем без сохранения
            # пропорций, ровно в габариты спрайта из сцены
            dark = f'/tmp/dark_{name}.png'
            subprocess.run(['convert', 'Assets/Art/Environment/chasm.png',
                            '-resize', f'{int(w*S)}x{int(h*S)}!', dark], check=True)
            put(dark, px - int(w * S / 2), int(cy(y) - h * S / 2), name)
            continue
        import math
        tile = '/tmp/gt.png' if guid == G_GROUND else '/tmp/bt.png'
        unit = 4.0 if guid == G_GROUND else 1.0
        n = math.ceil(w / unit)
        strip = f'/tmp/strip_{name}.png'
        # кладём тайлы и подрезаем до точной ширины сегмента, иначе
        # некратный сегмент (6.2 юнита) наезжает на следующий
        subprocess.run(['convert'] + [tile] * n + ['+append', '-crop',
                        f'{int(round(w*S))}x{huge}', '+repage', strip], check=True)
        top = int(cy(0)) if guid == G_GROUND else int(cy(y) - h / 2)
        put(strip, px - int(w * S / 2), top, name)

    put('/tmp/hh.png', int(cx(herox)) - int(1.289 * S / 2),
        int(cy(0.031)) - int(2 * S), 'heroine')

    args = ['convert', '-size', f'{W}x{H}', 'xc:none']
    for img, x0, y0 in stack:
        args += [img, '-geometry', f'+{x0}+{y0}', '-composite']
    args.append(out)
    subprocess.run(args, check=True)
    print(f'{out}: камера ({camx}, {camy}), слоёв {len(stack)}')


huge = '9999'
for i, (camx, camy, herox, tag) in enumerate([(-3, -0.27, -3, 'start'),
                                              (5.5, 1.4, 3.5, 'platforms'),
                                              (23, 0.2, 22.5, 'far')]):
    render(camx, camy, herox, f'/tmp/view{i}_{tag}.png')
