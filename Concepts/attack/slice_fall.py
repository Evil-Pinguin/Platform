"""Режет лист удара при падении на 2 кадра: Assets/Art/Heroine/Attack/fall_*.

Режет тот же приём, что Concepts/jump/slice_jump.py, но с двумя отличиями,
оба нужны ради бесшовности перехода:
  * холст 560 px, как у кадров атаки, а не 330, как у прыжка;
  * пивот в тех же 165 px, что у attack_*. Иначе при переходе
    «обычный удар -> удар при падении» корпус дёргался бы вбок: у attack_*
    пивот 0.2946 от ширины 560, у прыжка 0.5 от ширины 330 — одно и то же
    абсолютное смещение (165 px), но доля от холста разная.


Почему не полный Concepts/walk/slice_walk.py: тот режет лист на три кадра,
а прыжку нужно два. Логика та же (альфа снимается заливкой от границы по
цвету фона, каждая фигура приводится к общей высоте, головы ставятся на
одну точку).

Почему не поза с подтянутыми коленями (Concepts/jump/jump_sheet_v1b.png):
там фигуры 482 и 619 px, разница 28%. Прыжок в платформере обязан держать
рост: при выравнивании по общей высоте сжатая поза растянулась бы вдвое, а
без выравнивания прыжок выглядел бы как уменьшение героини. Поэтому взяты
позы в полный рост (887 и 858 px, разброс 3.3%) — разлетающиеся руки и коса
считываются как «в воздухе», а размер остаётся прежним.

Рукава в исходном стиле длинные — до запястья, со сборкой в кружевную
манжету. Листы v1a/v2a без длинных рукавов брать нельзя.

Рамка, высота, точка под ступнями и пивот — ровно как у кадров ходьбы, чтобы
переход ходьба -> прыжок -> ходьба не дёргался.
"""
import hashlib
import os
import re
import subprocess
from collections import deque

SHEET = 'Concepts/attack/fall_sheet_v2a.png'
OUT_DIR = 'Assets/Art/Heroine/Attack'
META_TPL = 'Assets/Art/Heroine/Attack/attack_1.png.meta'
CANVAS_W, CANVAS_H = 560, 512
TARGET_H = 479           # общая высота фигуры, как у кадров атаки и ходьбы
FEET_Y = 504             # на какой высоте стоят ступни
HEAD_FRACTION = 0.30
PICK = (0, 1)           # замах и сам удар

os.makedirs(OUT_DIR, exist_ok=True)

BG = None


def im_size(img):
    a, b = subprocess.run(['identify', '-format', '%w %h', img],
                          capture_output=True, text=True).stdout.split()
    return int(a), int(b)


def is_bg(px, i):
    r, g, b, a = px[i * 4:i * 4 + 4]
    if a < 200:
        return False
    return (abs(r - BG[0]) <= 14 and abs(g - BG[1]) <= 14
            and abs(b - BG[2]) <= 14)


def flood_mask(px, w, h):
    bg = bytearray(w * h)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            i = y * w + x
            if is_bg(px, i):
                bg[i] = 1
                q.append(i)
    for y in range(h):
        for x in (0, w - 1):
            i = y * w + x
            if is_bg(px, i):
                bg[i] = 1
                q.append(i)
    while q:
        i = q.popleft()
        x, y = i % w, i // w
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not bg[j] and is_bg(px, j):
                    bg[j] = 1
                    q.append(j)
    return bytearray(0 if v else 255 for v in bg)


def clear_enclosed(rgba, mask, w, h):
    """Вычищает дыры фона, оказавшиеся внутри фигуры."""
    seen = bytearray(w * h)
    q = deque()
    for x in range(w):
        for y in (0, h - 1):
            i = y * w + x
            if not mask[i]:
                seen[i] = 1
                q.append(i)
    for y in range(h):
        for x in (0, w - 1):
            i = y * w + x
            if not mask[i]:
                seen[i] = 1
                q.append(i)
    while q:
        i = q.popleft()
        x, y = i % w, i // w
        for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not seen[j] and not mask[j]:
                    seen[j] = 1
                    q.append(j)
    out = bytearray(rgba)
    n = 0
    for i in range(w * h):
        if not mask[i] and not seen[i]:
            out[i * 4:i * 4 + 4] = b'\x00\x00\x00\x00'
            n += 1
    return bytes(out), n


def components(mask, w, h, min_area):
    seen = bytearray(w * h)
    out = []
    for st in range(w * h):
        if seen[st] or not mask[st]:
            continue
        q = deque([st])
        seen[st] = 1
        xs, ys = [], []
        while q:
            i = q.popleft()
            xs.append(i % w)
            ys.append(i // w)
            x, y = i % w, i // w
            for nx, ny in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
                if 0 <= nx < w and 0 <= ny < h:
                    j = ny * w + nx
                    if mask[j] and not seen[j]:
                        seen[j] = 1
                        q.append(j)
        if len(xs) >= min_area:
            out.append((min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    out.sort()
    return out


def head_centre(mask, w, h, box):
    x0, y0, x1, y1 = box
    cut = y0 + max(1, int((y1 - y0) * HEAD_FRACTION))
    cols = [x for x in range(x0, x1)
            for y in range(y0, cut) if mask[y * w + x]]
    return (min(cols) + max(cols)) / 2.0 if cols else (x0 + x1) / 2.0


def alpha_mask(path):
    w, h = im_size(path)
    px = subprocess.run(['convert', path, '-depth', '8', 'rgba:-'],
                        capture_output=True).stdout
    return px, w, h


# --- опорная точка берётся с УЖЕ готовых кадров ходьбы, чтобы при переходе
#     на удар корпус не уезжал вбок. Только walk_1..3: walk_4..8 — легаси от
#     прежнего четырёхкадрового цикла и в среднем сбивали бы точку.
walk_centres = []
for i in (1, 2, 3):
    px, w, h = alpha_mask(f'Assets/Art/Heroine/Walk/walk_{i}.png')
    m = bytearray(255 if a > 40 else 0 for a in px[3::4])
    boxes = components(m, w, h, 2000)
    if boxes:
        walk_centres.append(head_centre(m, w, h, max(boxes, key=lambda b: b[1] - b[0])))
TARGET_HEAD_X = sum(walk_centres) / len(walk_centres)
print(f'корпус на кадрах ходьбы: {TARGET_HEAD_X:.1f}px '
      f'(разброс {max(walk_centres) - min(walk_centres):.1f}px)')

W, H = im_size(SHEET)
print(f'лист: {W}x{H}')
src = subprocess.run(['convert', SHEET, '-depth', '8', 'rgba:-'],
                     capture_output=True).stdout
globals()['BG'] = tuple(src[0:3])
print(f'фон листа: rgb{BG}')
mask = flood_mask(src, W, H)
painted = bytearray(src)
for i in range(W * H):
    painted[i * 4 + 3] = mask[i]
fixed, holes = clear_enclosed(bytes(painted), mask, W, H)
print(f'дыр вычищено: {holes}')

boxes = components(mask, W, H, 2000)
print('фигур найдено:', len(boxes), '-> берём', [i + 1 for i in PICK])
if len(boxes) != 2:
    raise SystemExit('ожидалось ровно 2 фигуры на листе прыжка')

picked = [boxes[i] for i in PICK]
heights = [b[3] - b[1] for b in picked]
print('высоты в листе:', heights,
      f'разброс {100 * (max(heights) - min(heights)) / min(heights):.1f}%')
if (max(heights) - min(heights)) / min(heights) > 0.08:
    raise SystemExit('разброс высот больше 8%: прыжок не должен менять рост')
scales = [TARGET_H / h for h in heights]

for n, (x0, y0, x1, y1) in enumerate(picked, 1):
    fw, fh = x1 - x0, y1 - y0
    crop = bytearray()
    for y in range(y0, y1):
        crop += fixed[(y * W + x0) * 4:(y * W + x1) * 4]
    tmp = f'/tmp/jp_{n}.rgba'
    with open(tmp, 'wb') as f:
        f.write(bytes(crop))
    scaled = f'/tmp/jp_{n}_s.png'
    scale = scales[n - 1]
    subprocess.run(['convert', '-size', f'{fw}x{fh}', '-depth', '8', f'rgba:{tmp}',
                    '-filter', 'Lanczos', '-resize',
                    f'{int(fw*scale)}x{int(fh*scale)}', scaled], check=True)
    sw, sh = im_size(scaled)

    dy = FEET_Y - sh
    hx = (head_centre(mask, W, H, (x0, y0, x1, y1)) - x0) * scale
    dx = round(TARGET_HEAD_X - hx)

    out = f'{OUT_DIR}/fall_{n}.png'
    left = min(0, dx)
    width = max(CANVAS_W, dx + sw) - left
    subprocess.run(['convert', '-size', f'{width}x{CANVAS_H}', 'xc:none', scaled,
                    '-geometry', f'{dx:+d}{dy:+d}', '-composite',
                    '-crop', f'{CANVAS_W}x{CANVAS_H}+{-left}+0', '+repage', out],
                   check=True)
    os.remove(tmp)
    os.remove(scaled)
    print(f'кадр {n}: {fw}x{fh} -> {sw}x{sh}, сдвиг {dx:+d}/{dy:+d}, {out}')

    guid = hashlib.md5(('fall-v1-' + str(n)).encode()).hexdigest()
    sid = hashlib.md5(('fall-v1-sprite-' + str(n)).encode()).hexdigest()
    meta = open(META_TPL, encoding='utf-8').read()
    meta = (meta.replace(re.search(r'guid: ([0-9a-f]{32})', meta).group(1), guid)
                .replace(re.search(r'spriteID: ([0-9a-f]{32})', meta).group(1), sid))
    meta = meta.replace('  spritePivot: {x: 0.2946, y: 0}', '  spritePivot: {x: 0.2946, y: 0}')
    assert 'alignment: 7' in meta, meta[:300]
    assert re.search(r'  spritePivot: \{x: 0\.2946, y: 0\}', meta), meta[:300]
    open(f'{OUT_DIR}/fall_{n}.png.meta', 'w', encoding='utf-8').write(meta)
    print(f'  guid {guid}')

subprocess.run(['convert'] + [f'{OUT_DIR}/fall_{i}.png' for i in (1, 2)]
               + ['+append', '-background', '#2b2f3a', '-bordercolor', '#2b2f3a',
                  '-border', '6', '/tmp/fall_preview.png'], check=True)
print('превью: /tmp/fall_preview.png')
print('пивот кадров: 0.2946 (165px на холсте 560, как у attack_*)')
