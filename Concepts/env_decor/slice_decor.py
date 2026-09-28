"""Режет лист окружения на отдельные спрайты.

Метод тот же, что сработал для цикла ходьбы: альфа берётся из заливки
с границы (floodfill), цвет — из оригинала, потом дочищаются белые
дыры, замкнутые внутри предмета. Ламповый белый убирать нельзя —
он выедает блики на камнях и траве.
"""
import subprocess, sys, os
from collections import deque

SHEET = 'Concepts/env_decor/props_sheet.png'
OUT = 'Assets/Art/Environment/Decor'
os.makedirs(OUT, exist_ok=True)

NAMES = ['spruce', 'rocks', 'grass_tuft', 'serge_pole']


def sheet_size(img=SHEET):
    out = subprocess.run(['identify', '-format', '%w %h', img],
                         capture_output=True, text=True).stdout.split()
    return int(out[0]), int(out[1])


def load(img, size):
    """RGBA как плоские байты."""
    raw = subprocess.run(['convert', img, '-resize', f'{size[0]}x{size[1]}!',
                          '-depth', '8', 'rgba:-'], capture_output=True).stdout
    return raw


def rgba_bytes(img, size):
    return load(img, size)


def alpha_from_floodfill(src_rgba, w, h):
    """1 = объект, 0 = фон. Заливка от всех граничных пикселей,
    у которых цвет близок к фону."""
    a = bytearray(w * h)
    px = src_rgba
    q = deque()

    def is_bg(i):
        r, g, b, al = px[i*4:i*4+4]
        return al > 200 and r > 236 and g > 236 and b > 236

    for x in range(w):
        for y in (0, h - 1):
            i = y * w + x
            if is_bg(i):
                a[i] = 1
                q.append(i)
    for y in range(h):
        for x in (0, w - 1):
            i = y * w + x
            if is_bg(i):
                a[i] = 1
                q.append(i)

    while q:
        i = q.popleft()
        x, y = i % w, i // w
        for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not a[j] and is_bg(j):
                    a[j] = 1
                    q.append(j)
    return bytes(0 if v else 255 for v in a)


def clear_enclosed(rgba, mask, w, h):
    """Белые дыры, полностью окружённые предметом, — в фон."""
    seen = bytearray(w * h)
    bgq = deque()
    for x in range(w):
        for y in (0, h - 1):
            i = y * w + x
            if not mask[i]:
                seen[i] = 1
                bgq.append(i)
    for y in range(h):
        for x in (0, w - 1):
            i = y * w + x
            if not mask[i]:
                seen[i] = 1
                bgq.append(i)
    while bgq:
        i = bgq.popleft()
        x, y = i % w, i // w
        for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
            if 0 <= nx < w and 0 <= ny < h:
                j = ny * w + nx
                if not seen[j] and not mask[j]:
                    seen[j] = 1
                    bgq.append(j)

    out = bytearray(rgba)
    cleared = 0
    for y in range(h):
        for x in range(w):
            i = y * w + x
            if not mask[i] and not seen[i]:
                out[i*4:i*4+4] = b'\x00\x00\x00\x00'
                cleared += 1
    return bytes(out), cleared


def bbox(mask, w, h):
    xs, ys = [], []
    for y in range(h):
        row = mask[y*w:(y+1)*w]
        if 255 in row:
            ys.append(y)
    for x in range(w):
        col = [mask[y*w + x] for y in range(h)]
        if 255 in col:
            xs.append(x)
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def components(mask, w, h):
    """Связные области маски, по убыванию площади."""
    seen = bytearray(w * h)
    out = []
    for start in range(w * h):
        if seen[start] or not mask[start]:
            continue
        q = deque([start])
        seen[start] = 1
        pix = []
        while q:
            i = q.popleft()
            pix.append(i)
            x, y = i % w, i // w
            for nx, ny in ((x-1, y), (x+1, y), (x, y-1), (x, y+1)):
                if 0 <= nx < w and 0 <= ny < h:
                    j = ny * w + nx
                    if mask[j] and not seen[j]:
                        seen[j] = 1
                        q.append(j)
        xs = [i % w for i in pix]
        ys = [i // w for i in pix]
        out.append((len(pix), min(xs), min(ys), max(xs) + 1, max(ys) + 1))
    return out


def write_png(path, rgba, w, h):
    tmp = path + '.rgba'
    with open(tmp, 'wb') as f:
        f.write(rgba)
    subprocess.run(['convert', '-size', f'{w}x{h}', '-depth', '8', f'rgba:{tmp}',
                    path], check=True)
    os.remove(tmp)


W, H = sheet_size()
print(f'лист: {W}x{H}')
src = rgba_bytes(SHEET, (W, H))
mask = alpha_from_floodfill(src, W, H)
# цвет из оригинала, прозрачность из маски: в исходном листе альфы нет,
# иначе белый фон остался бы непрозрачным
painted = bytearray(src)
for i in range(W * H):
    painted[i * 4 + 3] = mask[i]
fixed, cleared = clear_enclosed(bytes(painted), mask, W, H)
print(f'дыр вычищено: {cleared}')

comps = [c for c in components(mask, W, H) if c[0] > W * H * 0.002]
comps.sort(key=lambda c: c[1])
print('компонент:', [(c[1], c[2], c[3]-c[1], c[4]-c[2]) for c in comps])

if len(comps) != 4:
    sys.exit(f'ожидал 4 предмета, получил {len(comps)} — разбирай вручную')

for (area, x0, y0, x1, y1), name in zip(comps, NAMES):
    cw, ch = x1 - x0, y1 - y0
    crop = bytearray()
    for y in range(y0, y1):
        crop += fixed[(y * W + x0) * 4:(y * W + x1) * 4]
    write_png(f'{OUT}/{name}.png', bytes(crop), cw, ch)
    print(f'{name}: {cw}x{ch} (пиксель/юнит -> размер в юнитах)')
