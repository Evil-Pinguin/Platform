"""Режет листы Айыыны (зелёный фон) на кадры для Assets/Art/Aiyyna.

Альфа: хромакей по зелёному + подавление зелёной каймы.
Кадры: связные области маски, мелкие куски приклеиваются к ближайшей крупной.
Все кадры масштабируются одним коэффициентом на лист (рост героини одинаковый),
ставятся ногами на FEET_Y и центрируются по корпусу (нижняя половина фигуры
без крыльев сложно выделить, поэтому берём центр массы нижних 45% маски).
"""
import os
import numpy as np
from PIL import Image
from scipy import ndimage

HERE = os.path.dirname(__file__)
OUT = os.path.join(HERE, '..', '..', 'Assets', 'Resources', 'Playable', 'Айыына')
CANVAS_H, FEET_Y = 512, 504
TARGET_H = 479          # рост героини в пикселях кадра, как у основной


def load_rgba(path):
    a = np.asarray(Image.open(path).convert('RGB')).astype(np.float32)
    r, g, b = a[..., 0], a[..., 1], a[..., 2]
    greenness = g - np.maximum(r, b)
    alpha = np.clip(1.0 - (greenness - 40) / 80.0, 0, 1)
    # убираем зелёный отсвет на краях
    spill = np.maximum(g - np.maximum(r, b), 0)
    a[..., 1] -= spill * 0.9
    rgba = np.dstack([np.clip(a, 0, 255), alpha * 255]).astype(np.uint8)
    return rgba


def split(rgba, rows, hints=None):
    """rows — сколько кадров в каждом ряду. Ряды делятся по пустым строкам,
    кадры в ряду — по самым «тонким» столбцам маски (фигуры могут касаться)."""
    mask = rgba[..., 3] > 128
    rs = mask.sum(1)
    # границы рядов: самые пустые строки между рядами
    H = mask.shape[0]
    cuts_y = [0]
    for r in range(1, len(rows)):
        lo, hi = int(H * r / len(rows) - H * 0.15), int(H * r / len(rows) + H * 0.15)
        cuts_y.append(lo + int(np.argmin(rs[lo:hi])))
    cuts_y.append(H)
    frames = []
    for ri, k in enumerate(rows):
        band = mask[cuts_y[ri]:cuts_y[ri + 1]]
        cs = ndimage.uniform_filter1d(band.sum(0).astype(float), 9)
        xs = np.where(cs > 0)[0]
        x0, x1 = xs.min(), xs.max()
        cuts = [x0]
        for j in range(1, k):
            if hints:
                c = hints[j - 1] * rgba.shape[1]
                lo, hi = int(c - 40), int(c + 40)
            else:
                c = x0 + (x1 - x0) * j / k
                lo, hi = int(c - (x1 - x0) / k * 0.35), int(c + (x1 - x0) / k * 0.35)
            cuts.append(lo + int(np.argmin(cs[lo:hi])))
        cuts.append(x1 + 1)
        for j in range(k):
            sub = rgba[cuts_y[ri]:cuts_y[ri + 1], cuts[j]:cuts[j + 1]].copy()
            m = sub[..., 3] > 128
            lab, n = ndimage.label(m)
            if n:
                sizes = ndimage.sum(m, lab, range(1, n + 1))
                keep = [i + 1 for i, v in enumerate(sizes) if v > sizes.max() * 0.02]
                sub[..., 3] = np.where(np.isin(lab, keep), sub[..., 3], 0)
            ys, xs2 = np.where(sub[..., 3] > 128)
            frames.append(sub[ys.min():ys.max() + 1, xs2.min():xs2.max() + 1])
    return frames


def body_center_x(crop):
    a = crop[..., 3] > 128
    h = a.shape[0]
    low = a[int(h * 0.55):]
    ys, xs = np.where(low)
    return xs.mean() if len(xs) else a.shape[1] / 2


def place(crop, scale, name, feet_on_ground=True, width=None):
    img = Image.fromarray(crop)
    w, h = int(img.width * scale), int(img.height * scale)
    img = img.resize((max(w, 1), max(h, 1)), Image.LANCZOS)
    cx = body_center_x(np.asarray(img))
    half = int(max(cx, img.width - cx)) + 4
    cw = width or 2 * half
    cw = max(cw, 2 * half)
    canvas = Image.new('RGBA', (cw, max(CANVAS_H, img.height + 8)), (0, 0, 0, 0))
    ch = canvas.height
    y = (ch - (CANVAS_H - FEET_Y)) - img.height
    canvas.paste(img, (int(cw / 2 - cx), y), img)
    canvas.save(os.path.join(OUT, name + '.png'))
    return canvas.size


def main():
    os.makedirs(OUT, exist_ok=True)
    walk = split(load_rgba(os.path.join(HERE, 'walk_sheet.png')), [5],
                 hints=[440 / 1376, 660 / 1376, 880 / 1376, 1100 / 1376])
    act = split(load_rgba(os.path.join(HERE, 'action_sheet.png')), [3, 4])
    # масштаб по стоящей позе: её рост = TARGET_H
    ref_h = walk[0].shape[0]
    s = TARGET_H / ref_h
    names_w = ['idle_front', 'walk_1', 'walk_2', 'walk_3', 'walk_4']
    for c, n in zip(walk, names_w):
        print(n, place(c, s, n))
    # в листе действий фигуры нарисованы мельче: выравниваем по голове-ноги позы защиты
    s2 = TARGET_H / act[6].shape[0] * 0.92
    names_a = ['jump_1', 'jump_2', 'attack_1', 'attack_2', 'fall_1', 'fall_2', 'guard_1']
    for c, n in zip(act, names_a):
        print(n, place(c, s2, n))


if __name__ == '__main__':
    main()


def glide():
    """Отдельный кадр планирования: крылья раскрыты во всю ширину."""
    rgba = shrink_wings()
    m = rgba[..., 3] > 128
    lab, n = ndimage.label(m)
    sizes = ndimage.sum(m, lab, range(1, n + 1))
    keep = [i + 1 for i, v in enumerate(sizes) if v > sizes.max() * 0.02]
    rgba[..., 3] = np.where(np.isin(lab, keep), rgba[..., 3], 0)
    ys, xs = np.where(rgba[..., 3] > 128)
    crop = rgba[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    # рост фигуры (макушка ~190 px, ступни ~725 px листа) приводим к росту героини
    s = TARGET_H / 560.0
    img = Image.fromarray(crop)
    img = img.resize((int(img.width * s), int(img.height * s)), Image.LANCZOS)
    body_x = (620 - xs.min()) * s          # центр корпуса на листе ~ x=620
    half = int(max(body_x, img.width - body_x)) + 4
    canvas = Image.new('RGBA', (2 * half, max(CANVAS_H, img.height + 8)), (0, 0, 0, 0))
    canvas.paste(img, (int(half - body_x), canvas.height - (CANVAS_H - FEET_Y) - img.height), img)
    canvas.save(os.path.join(OUT, 'glide.png'))
    print('glide', canvas.size)


def shrink_wings(k=0.72):
    """Уменьшает крылья на листе планирования: всё, что вне контура фигуры
    (голова, волосы, тело, ноги, копьё), считается крылом и сжимается к
    основанию крыла у спины. Фигура кладётся сверху без изменений."""
    from PIL import ImageDraw
    src = Image.fromarray(load_rgba(os.path.join(HERE, 'glide_sheet.png')))
    W, H = src.size
    body_poly = [(660, 175), (720, 168), (790, 228), (800, 330), (790, 420), (820, 468),
                 (1080, 668), (1045, 672), (800, 500), (700, 520), (680, 705), (640, 712),
                 (500, 732), (455, 700), (415, 660), (365, 525), (470, 440), (430, 385),
                 (430, 300), (560, 238), (640, 215)]
    pm = Image.new('L', (W, H), 0)
    ImageDraw.Draw(pm).polygon(body_poly, fill=255)
    pm_a = np.asarray(pm) > 0
    s = np.asarray(src)
    xs = np.arange(W)[None, :].repeat(H, 0)
    body = s.copy(); body[..., 3] = np.where(pm_a, s[..., 3], 0)
    out = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    # крыло = крупная связная область вне контура фигуры; сторона — по центру
    # области. Мелкие обрывки (края платья, копья) остаются на месте, в фигуре.
    outside = (~pm_a) & (s[..., 3] > 20)
    lab, n = ndimage.label(outside)
    sizes = ndimage.sum(outside, lab, range(1, n + 1))
    left_ids, right_ids, keep_ids = [], [], []
    for i, v in enumerate(sizes, 1):
        if v < sizes.max() * 0.05:
            keep_ids.append(i)
        elif ndimage.center_of_mass(outside, lab, i)[1] < 620:
            left_ids.append(i)
        else:
            right_ids.append(i)
    body[..., 3] = np.where(pm_a | np.isin(lab, keep_ids), s[..., 3], 0)
    for side, root in ((np.isin(lab, left_ids), (560, 250)), (np.isin(lab, right_ids), (800, 250))):
        layer = s.copy(); layer[..., 3] = np.where(side, s[..., 3], 0)
        rx, ry = root
        # обратное отображение: точка вывода p -> источник R + (p - R) / k
        m = (1 / k, 0, rx - rx / k, 0, 1 / k, ry - ry / k)
        out.alpha_composite(Image.fromarray(layer).transform((W, H), Image.AFFINE, m, Image.BICUBIC))
    out.alpha_composite(Image.fromarray(body))
    return np.asarray(out).copy()
