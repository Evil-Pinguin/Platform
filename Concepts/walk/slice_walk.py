"""Режет лист походки на кадры и кладёт их в Assets/Art/Heroine/Walk.

Тот же приём, что в Concepts/attack/slice_attack.py: альфа снимается
заливкой от границы по цвету фона, дыры внутри фигуры вычищаются,
каждая фигура приводится к общей высоте (иначе в игре пульсирует
рост), и головы всех кадров ставятся на одну и ту же точку — чтобы
корпус не ездил влево-вправо и чтобы переход с ходьбы на удар не
дёргался.

Отличия от резальщика атаки:
  * холст 330 px, а не 560: ходьбе широкий холст не нужен;
  * пивот остаётся ровно посередине (165 px), как у прежних кадров,
    поэтому кадры атаки продолжают попадать в ту же точку;
  * кадров три, а не четыре. Модель надёжно рисует три фигуры в ряд;
    четыре она уводит в сетку 2x2 и теряет различие поз. Три кадра при
    9 fps дают те же 0.33 с на цикл, что и четыре при 12.
"""
import os
import re
import subprocess
import hashlib
from collections import deque

SHEET = 'Concepts/walk/walk_sheet_v8a.png'
OUT_DIR = 'Assets/Art/Heroine/Walk'
CANVAS_W, CANVAS_H = 330, 512
WALK_PIVOT_PX = 165      # пивот прежних кадров: ровно середина холста
TARGET_H = 479           # общая высота фигуры в пикселях
FEET_Y = 504             # на какой высоте стоят ступни
HEAD_FRACTION = 0.30

os.makedirs(OUT_DIR, exist_ok=True)


def im_size(img):
    a, b = subprocess.run(['identify', '-format', '%w %h', img],
                          capture_output=True, text=True).stdout.split()
    return int(a), int(b)


# Фон берём с угла листа, а не задаём порогами: на этом листе он не
# белый, а кремовый (245, 240, 216), и проверка «все каналы > 236»
# его не узнавала — весь лист считался одной фигурой.
BG = None


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
    """Вычищает белые дыры, оказавшиеся внутри фигуры (между рукавом и
    туловищем, между косой и платьем)."""
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
    """Середина головы по верхним HEAD_FRACTION высоте фигуры: не зависит
    от позы рук и ног, в отличие от середины всей рамки."""
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


# --- опорная точка: где голова на НЫНЕШНИХ кадрах ходьбы. Считаем до
# перезаписи файлов, иначе опорой станет сам себя.
walk_centres = []
for i in range(1, 5):
    px, w, h = alpha_mask(f'Assets/Art/Heroine/Walk/walk_{i}.png')
    m = bytearray(255 if a > 40 else 0 for a in px[3::4])
    boxes = components(m, w, h, 2000)
    if boxes:
        walk_centres.append(head_centre(m, w, h, max(boxes, key=lambda b: b[1] - b[0])))
TARGET_HEAD_X = sum(walk_centres) / len(walk_centres)
print(f'корпус на прежних кадрах ходьбы: {TARGET_HEAD_X:.1f}px '
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
print(f'белых дыр вычищено: {holes}')

boxes = components(mask, W, H, 2000)
print('фигур найдено:', len(boxes))
if len(boxes) != 3:
    raise SystemExit('ожидалось ровно 3 фигуры')

heights = [b[3] - b[1] for b in boxes]
print('высоты в листе:', heights,
      f'разброс {100 * (max(heights) - min(heights)) / min(heights):.1f}%')
scales = [TARGET_H / h for h in heights]

for idx, (x0, y0, x1, y1) in enumerate(boxes, 1):
    fw, fh = x1 - x0, y1 - y0
    crop = bytearray()
    for y in range(y0, y1):
        crop += fixed[(y * W + x0) * 4:(y * W + x1) * 4]
    tmp = f'/tmp/wk_{idx}.rgba'
    with open(tmp, 'wb') as f:
        f.write(bytes(crop))
    scaled = f'/tmp/wk_{idx}_s.png'
    scale = scales[idx - 1]
    subprocess.run(['convert', '-size', f'{fw}x{fh}', '-depth', '8', f'rgba:{tmp}',
                    '-filter', 'Lanczos', '-resize',
                    f'{int(fw*scale)}x{int(fh*scale)}', scaled], check=True)
    sw, sh = im_size(scaled)

    # после масштабирования ступни должны встать на FEET_Y
    dy = FEET_Y - sh
    # голова после масштабирования — на ту же опорную точку
    hx = (head_centre(mask, W, H, (x0, y0, x1, y1)) - x0) * scale
    dx = round(TARGET_HEAD_X - hx)

    out = f'{OUT_DIR}/walk_{idx}.png'
    left = min(0, dx)
    width = max(CANVAS_W, dx + sw) - left
    subprocess.run(['convert', '-size', f'{width}x{CANVAS_H}', 'xc:none', scaled,
                    '-geometry', f'{dx:+d}{dy:+d}', '-composite',
                    '-crop', f'{CANVAS_W}x{CANVAS_H}+{-left}+0', '+repage', out],
                   check=True)
    os.remove(tmp)
    os.remove(scaled)
    print(f'кадр {idx}: {fw}x{fh} -> {sw}x{sh}, сдвиг {dx:+d}/{dy:+d}, {out}')

    guid = hashlib.md5(('walk-v8-' + str(idx)).encode()).hexdigest()
    sid = hashlib.md5(('walk-v8-sprite-' + str(idx)).encode()).hexdigest()
    meta = open('Assets/Art/Heroine/Walk/walk_1.png.meta', encoding='utf-8').read()
    meta = (meta.replace(re.search(r'guid: ([0-9a-f]{32})', meta).group(1), guid)
                .replace(re.search(r'spriteID: ([0-9a-f]{32})', meta).group(1), sid)
                .replace('  alignment: 1\n  spritePivot: {x: 0.2946, y: 0.0488}',
                         '  alignment: 7\n  spritePivot: {x: 0.5, y: 0}'))
    assert 'alignment: 7' in meta, meta[:200]
    open(f'{OUT_DIR}/walk_{idx}.png.meta', 'w', encoding='utf-8').write(meta)

subprocess.run(['convert'] + [f'{OUT_DIR}/walk_{i}.png' for i in range(1, 4)]
               + ['+append', '-background', '#2b2f3a', '-bordercolor', '#2b2f3a',
                  '-border', '6', '/tmp/walk_preview.png'], check=True)
print('превью: /tmp/walk_preview.png')
print(f'пивот кадров: 0.5000 ({WALK_PIVOT_PX}px на холсте {CANVAS_W})')
