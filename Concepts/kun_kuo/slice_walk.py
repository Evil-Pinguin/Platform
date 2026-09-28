"""Split Kun Kuo's generated 4x2 green-screen walk sheet into Unity sprites.

Requires ImageMagick's `convert`. The green matte is removed and its color spill
is un-mixed from anti-aliased edges before the frames are written as RGBA PNGs.
"""
from pathlib import Path
import shutil
import subprocess

HERE = Path(__file__).resolve().parent
SHEET = HERE / "walk_sheet_chroma.png"
OUT = HERE.parent.parent / "Assets" / "Resources" / "Playable" / "Күн Куо"
CELL_W, CELL_H = 344, 384
CROP_W, CROP_H = 342, 382  # skip the black separators between cells
OUT_W, OUT_H = 456, 512


def key_green(rgba):
    out = bytearray(len(rgba))
    for i in range(0, len(rgba), 4):
        r, g, b = rgba[i], rgba[i + 1], rgba[i + 2]
        green_excess = max(0, g - max(r, b))
        # Reject saturated green leftovers (including anti-aliased cell borders)
        # before estimating partial coverage along the character silhouette.
        if max(r, b) < 96 and g > 170:
            alpha = 0.0
        else:
            alpha = max(0.0, min(1.0, 1.0 - green_excess / 255.0))
        if alpha < 0.04:
            out[i:i + 4] = b"\0\0\0\0"
            continue

        # Un-composite the green matte to avoid a bright fringe around the art.
        inv = 1.0 / alpha
        red = max(0, min(255, round(r * inv)))
        green = max(0, min(255, round((g - 255.0 * (1.0 - alpha)) * inv)))
        blue = max(0, min(255, round(b * inv)))
        out[i:i + 4] = bytes((red, green, blue, round(alpha * 255)))
    return bytes(out)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    for frame in range(8):
        col, row = frame % 4, frame // 4
        x, y = col * CELL_W + 1, row * CELL_H + 1
        raw = subprocess.check_output([
            "convert", str(SHEET), "-crop", f"{CROP_W}x{CROP_H}+{x}+{y}",
            "+repage", "-filter", "Lanczos", "-resize", f"{OUT_W}x{OUT_H}!",
            "-depth", "8", "rgba:-"
        ])
        keyed = key_green(raw)
        target = OUT / f"walk_{frame + 1}.png"
        subprocess.run([
            "convert", "-size", f"{OUT_W}x{OUT_H}", "-depth", "8",
            "rgba:-", str(target)
        ], input=keyed, check=True)

    shutil.copyfile(OUT / "walk_2.png", OUT / "idle_front.png")
    print(f"Wrote idle_front.png and eight walk frames to {OUT}")


if __name__ == "__main__":
    main()
