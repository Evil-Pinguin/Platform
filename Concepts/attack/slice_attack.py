"""Режет лист атаки на 3 кадра и кладёт их в Assets/Art/Heroine/Attack.

Альфа из заливки по границе, цвет из оригинала — тот же приём, что
вытащил рабочий цикл ходьбы.

Отличие от walk: кадры атаки крепятся НЕ по центру рамки, а по корпусу.
В замахе нож уходит назад, в выпаде — вперёд, и при центрировании по
рамке сама героиня ��ыла бы ездила влево-вправо при смене анимации.
Опорная точка — центр головы (верхние 30% фигуры), он от позы не
зависит. Та же точка считается и на кадрах ходьбы, чтобы при переходе
с ходьбы на удар фигура не смещалась.
"""
import os
import re
import subprocess
import hashlib
from statistics import median
from collections import deque

SHEET = 'Concepts/attack/attack_sheet.png'
OUT_DIR = 'Assets/Art/Heroine/Attack'
# Холст атаки шире ходьбы: в выпаде вытянутая рука с ножом не влезает
# в 330 пикселей и клинок обрезался бы по краю. Пивот считается так, чтобы
# корпус встал ровно туда же, что на кадрах ходьбы: у ходьбы пивот в
# 165 px, то есть на (165 - 152) = 13 px правее головы. Держим эти же
# 13 px, но уже на более широком холсте.
WALK_CANVAS_W, WALK_PIVOT_PX = 330, 165
CANVAS_W, CANVAS_H = 560, 512
TARGET_H = 479
FEET_Y = 504
HEAD_FRACTION = 0.30      # какая доля высоты считается «головой»

os.makedirs(OUT_DIR, exist_ok=True)


def im_size(img):
    a, b = subprocess.run(['identify', '-format', '%w %h', img],
                          capture_output=True, text=True).stdout.split()
    return int(a), int(b)


def is_bg(px, i):
    r, g, b, a = px[i * 4:i * 4 + 4]
    return a > 200 and r > 236 and g > 236 and b > 236


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
    """Центр головы: середина x по верхним HEAD_FRACTION высоте фигуры."""
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


# --- где держится корпус на кадрах ходьбы (чтобы не дёргаться при смене)
walk_centres = []
for i in range(1, 5):
    px, w, h = alpha_mask(f'Assets/Art/Heroine/Walk/walk_{i}.png')
    m = bytearray(255 if a > 40 else 0 for a in px[3::4])
    boxes = components(m, w, h, 2000)
    if boxes:
        walk_centres.append(head_centre(m, w, h, max(boxes, key=lambda b: b[1] - b[0])))
TARGET_HEAD_X = sum(walk_centres) / len(walk_centres)
print(f'корпус на кадрах ходьбы: {TARGET_HEAD_X:.1f}px '
      f'(разброс {max(walk_centres) - min(walk_centres):.1f}px)')

W, H = im_size(SHEET)
print(f'лист атаки: {W}x{H}')
src = subprocess.run(['convert', SHEET, '-depth', '8', 'rgba:-'],
                     capture_output=True).stdout
mask = flood_mask(src, W, H)
painted = bytearray(src)
for i in range(W * H):
    painted[i * 4 + 3] = mask[i]
fixed, holes = clear_enclosed(bytes(painted), mask, W, H)
print(f'белых дыр вычищено: {holes}')

boxes = components(mask, W, H, 2000)
print('фигур найдено:', len(boxes))
if len(boxes) != 3:
    raise SystemExit('ожидалось ровно 3 фигуры')

heights = [b[3] - b[1] for b in boxes]
print('высоты в листе:', heights,
      f'разброс {100 * (max(heights) - min(heights)) / min(heights):.1f}%')
# Каждый кадр приводим к общей высоте: разброс в 6% дал бы в игре
# пульсацию фигуры — ровно то, за что ругали ходьбу. Высота берётся по
# рамке, поэтому у кадра, где коса разлетелась, корпус чуть сядет.
scales = [TARGET_H / h for h in heights]

for idx, (x0, y0, x1, y1) in enumerate(boxes, 1):
    fw, fh = x1 - x0, y1 - y0
    crop = bytearray()
    for y in range(y0, y1):
        crop += fixed[(y * W + x0) * 4:(y * W + x1) * 4]
    tmp = f'/tmp/at_{idx}.rgba'
    with open(tmp, 'wb') as f:
        f.write(bytes(crop))
    scaled = f'/tmp/at_{idx}_s.png'
    scale = scales[idx - 1]
    subprocess.run(['convert', '-size', f'{fw}x{fh}', '-depth', '8', f'rgba:{tmp}',
                    '-filter', 'Lanczos', '-resize',
                    f'{int(fw*scale)}x{int(fh*scale)}', scaled], check=True)
    sw, sh = im_size(scaled)

    # где оказалась голова после масштабирования
    hx = (head_centre(mask, W, H, (x0, y0, x1, y1)) - x0) * scale
    dx = round(TARGET_HEAD_X - hx)
    pivot_px = TARGET_HEAD_X + (WALK_PIVOT_PX - TARGET_HEAD_X)
    out = f'{OUT_DIR}/attack_{idx}.png'
    # фигура шире холста: режем по нужному куску, а не роняем композитинг
    left = min(0, dx)
    width = max(CANVAS_W, dx + sw) - left
    subprocess.run(['convert', '-size', f'{width}x{CANVAS_H}', 'xc:none', scaled,
                    '-geometry', f'{dx:+d}+0', '-composite',
                    '-crop', f'{CANVAS_W}x{CANVAS_H}+{-left}+0', '+repage', out],
                   check=True)
    os.remove(tmp)
    os.remove(scaled)
    print(f'кадр {idx}: {fw}x{fh} -> {sw}x{sh}, сдвиг {dx:+d}px, {out}')

    # .meta как у кадров ходьбы: низ-центр, 256 пикселей на юнит
    guid = hashlib.md5(('attack-' + str(idx)).encode()).hexdigest()
    sid = hashlib.md5(('attack-sprite-' + str(idx)).encode()).hexdigest()
    meta = open('Assets/Art/Heroine/Walk/walk_1.png.meta', encoding='utf-8').read()
    meta = (meta.replace(re.search(r'guid: ([0-9a-f]{32})', meta).group(1), guid)
                .replace(re.search(r'spriteID: ([0-9a-f]{32})', meta).group(1), sid)
                .replace('  alignment: 7\n  spritePivot: {x: 0.5, y: 0}',
                         f'  alignment: 1\n  spritePivot: {{x: {pivot_px/CANVAS_W:.4f}, y: 0}}'))
    assert 'alignment: 1' in meta
    open(f'{OUT_DIR}/attack_{idx}.png.meta', 'w', encoding='utf-8').write(meta)

subprocess.run(['convert'] + [f'{OUT_DIR}/attack_{i}.png' for i in range(1, 4)]
               + ['+append', '-background', '#2b2f3a', '-bordercolor', '#2b2f3a',
                  '-border', '6', '/tmp/attack_preview.png'], check=True)
print('превью: /tmp/attack_preview.png')
print(f'пивот атаки: {(TARGET_HEAD_X + (WALK_PIVOT_PX - TARGET_HEAD_X))/CANVAS_W:.4f} '
      f'({pivot_px:.0f}px на холсте {CANVAS_W})')
