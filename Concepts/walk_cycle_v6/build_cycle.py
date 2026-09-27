"""Собирает 4-кадровый цикл ходьбы из листа walk_sheet_v6b.png.

Тот же метод, что сработал для v5: альфа из заливки по границе, цвет
из оригинала, потом дочистка белых дыр, замкнутых внутри фигуры.
Ламповый белый убирать нельзя — он выедает блики на платье.

Четвёртая фигура в листе нарисована на 10% мельче остальных, поэтому
все кадры приводятся к общей высоте: иначе в игре героиня пульсирует
размером. Опорная точка — ступни, они у всех на одной линии.
"""
import os
import subprocess
from collections import deque

SHEET = 'Concepts/walk_cycle_v6/walk_sheet_v6b.png'
OUT_DIR = 'Concepts/walk_cycle_v6/frames'
CANVAS_W, CANVAS_H = 330, 512
TARGET_H = 479          # рост фигуры в кадре
FEET_Y = 504            # ступни на этой высоте

os.makedirs(OUT_DIR, exist_ok=True)


def im_size(img):
    a, b = subprocess.run(['identify', '-format', '%w %h', img],
                          capture_output=True, text=True).stdout.split()
    return int(a), int(b)


def load(img):
    return subprocess.run(['convert', img, '-depth', '8', 'rgba:-'],
                          capture_output=True).stdout


def is_bg(px, i):
    r, g, b, a = px[i * 4:i * 4 + 4]
    return a > 200 and r > 236 and g > 236 and b > 236


def flood_mask(px, w, h):
    """255 — объект, 0 — фон."""
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
    """Белые дыры, не соединённые с фоном, — в прозрачность."""
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


def write_png(path, rgba, w, h):
    tmp = path + '.rgba'
    with open(tmp, 'wb') as f:
        f.write(rgba)
    subprocess.run(['convert', '-size', f'{w}x{h}', '-depth', '8', f'rgba:{tmp}',
                    path], check=True)
    os.remove(tmp)


W, H = im_size(SHEET)
print(f'лист: {W}x{H}')
src = load(SHEET)
mask = flood_mask(src, W, H)

painted = bytearray(src)
for i in range(W * H):
    painted[i * 4 + 3] = mask[i]
fixed, holes = clear_enclosed(bytes(painted), mask, W, H)
print(f'белых дыр вычищено: {holes}')

boxes = components(mask, W, H, 2000)
print('фигур найдено:', len(boxes))
if len(boxes) != 4:
    raise SystemExit('ожидалось ровно 4 фигуры')

heights = [b[3] - b[1] for b in boxes]
print('высоты в листе:', heights,
      f'разброс {100 * (max(heights) - min(heights)) / min(heights):.1f}%')

# общий масштаб считаем по САМОЙ БОЛЬШОЙ фигуре: мелкие увеличиваем,
# иначе в игре будет пульсация размера
scale = TARGET_H / max(heights)
print(f'общий масштаб: {scale:.4f}')

for idx, (x0, y0, x1, y1) in enumerate(boxes, 1):
    fw, fh = x1 - x0, y1 - y0
    crop = bytearray()
    for y in range(y0, y1):
        crop += fixed[(y * W + x0) * 4:(y * W + x1) * 4]
    tmp = f'/tmp/v6_{idx}.rgba'
    with open(tmp, 'wb') as f:
        f.write(bytes(crop))
    scaled = f'/tmp/v6_{idx}_scaled.png'
    out = f'{OUT_DIR}/frame_{idx:02d}.png'
    # сперва уменьшаем во временный файл, потом наносим на холст 330x512
    # так, чтобы ступни оказались на высоте FEET_Y
    subprocess.run(['convert', '-size', f'{fw}x{fh}', '-depth', '8', f'rgba:{tmp}',
                    '-filter', 'Lanczos', '-resize',
                    f'{int(fw*scale)}x{int(fh*scale)}', scaled], check=True)
    subprocess.run(['convert', '-size', f'{CANVAS_W}x{CANVAS_H}', 'xc:none', scaled,
                    '-gravity', 'south', '-geometry', f'+0+{CANVAS_H - FEET_Y}',
                    '-composite', out], check=True)
    os.remove(tmp)
    os.remove(scaled)
    got = im_size(out)
    print(f'кадр {idx}: {fw}x{fh} -> {got[0]}x{got[1]}  {out}')

# превью в ряд, чтобы одним взглядом проверить фазы
strip = f'{OUT_DIR}/_preview.png'
subprocess.run(['convert'] + [f'{OUT_DIR}/frame_{i:02d}.png' for i in range(1, 5)]
               + ['+append', '-background', '#2b2f3a', '-bordercolor', '#2b2f3a',
                  '-border', '6', strip], check=True)
print('превью:', strip, im_size(strip))
