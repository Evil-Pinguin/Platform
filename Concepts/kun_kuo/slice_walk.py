"""Split Kun Kuo's generated walk, jump, and 4-hit attack sheets into sprites.

Requires ImageMagick's `convert`. Green matte pixels are removed and the
color spill is un-mixed from anti-aliased edges before writing RGBA PNGs.
Attack sheet columns are the four combo moves; rows are wind-up and impact.
The jump sheet reuses its first and third top-row cells for ascent and descent.
"""
from pathlib import Path
import shutil
import subprocess

HERE = Path(__file__).resolve().parent
WALK_SHEET = HERE / "walk_sheet_chroma.png"
JUMP_SHEET = HERE / "jump_sheet_chroma.png"
ATTACK_SHEET = HERE / "attack_sheet_chroma.png"
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


def slice_cell(sheet, col, row, target):
    x, y = col * CELL_W + 1, row * CELL_H + 1
    raw = subprocess.check_output([
        "convert", str(sheet), "-crop", f"{CROP_W}x{CROP_H}+{x}+{y}",
        "+repage", "-filter", "Lanczos", "-resize", f"{OUT_W}x{OUT_H}!",
        "-depth", "8", "rgba:-"
    ])
    subprocess.run([
        "convert", "-size", f"{OUT_W}x{OUT_H}", "-depth", "8",
        "rgba:-", str(target)
    ], input=key_green(raw), check=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)

    for frame in range(8):
        col, row = frame % 4, frame // 4
        slice_cell(WALK_SHEET, col, row, OUT / f"walk_{frame + 1}.png")
    shutil.copyfile(OUT / "walk_2.png", OUT / "idle_front.png")

    # The generated jump sheet has repeated filler cells in the same 4x2 layout
    # as the walk sheet. Its top-left cell is ascent; top-row column 3 is descent.
    for frame, col in enumerate((0, 2), start=1):
        slice_cell(JUMP_SHEET, col, 0, OUT / f"jump_{frame}.png")

    for attack in range(4):
        for stage in range(2):
            slice_cell(ATTACK_SHEET, attack, stage,
                       OUT / f"combo_{attack + 1}_{stage + 1}.png")

    print(f"Wrote idle, eight walk frames, two jump frames, and four two-frame combos to {OUT}")


if __name__ == "__main__":
    main()
