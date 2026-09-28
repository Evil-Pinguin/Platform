"""Убирает белый фон с листа ходьбы, сохраняя цвет полупрозрачных краёв.

Схема: m = min(r,g,b). Если m >= HI — чистый фон (alpha 0).
Если m <= LO — чистая фигура (alpha 255). Между — линейная рампа с
пересчётом цвета от белого фона (unpremultiply), чтобы края не
чернели.
"""
import subprocess

SRC = '/home/user/Platform/Concepts/walk_cycle_v5/walk_sheet_v5.png'
LO, HI = 244.0, 251.0

W, H = map(int, subprocess.run(['identify', '-format', '%w %h', SRC],
                               capture_output=True, text=True).stdout.split())
src = subprocess.run(['convert', SRC, '-depth', '8', 'rgb:-'],
                     capture_output=True).stdout
assert len(src) == W * H * 3, (len(src), W * H * 3)

out = bytearray(W * H * 4)
hist = {}
for i in range(W * H):
    r, g, b = src[i*3], src[i*3+1], src[i*3+2]
    m = min(r, g, b)
    bucket = int(m // 8) * 8
    hist[bucket] = hist.get(bucket, 0) + 1
    if m >= HI:
        a = 0.0
    elif m <= LO:
        a = 1.0
    else:
        a = (HI - m) / (HI - LO)
    j = i * 4
    if a <= 0.0:
        out[j] = out[j+1] = out[j+2] = out[j+3] = 0
    else:
        out[j+3] = int(round(a * 255))
        for k, c in enumerate((r, g, b)):
            v = (c - 255.0 * (1.0 - a)) / a
            out[j+k] = 0 if v < 0 else (255 if v > 255 else int(round(v)))
open('/tmp/v5.rgba', 'wb').write(bytes(out))
subprocess.run(['convert', '-size', f'{W}x{H}', '-depth', '8',
                'rgba:/tmp/v5.rgba', 'PNG32:/tmp/v5_clean.png'], check=True)
print('распределение min(r,g,b):',
      ', '.join(f'{k}:{v}' for k, v in sorted(hist.items())))
print(f'готово: {W}x{H}, рампа {LO}..{HI}')
