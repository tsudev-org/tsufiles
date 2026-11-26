"""So sánh bố cục hai bộ PDF cùng tên (gốc vs kết quả) - phiếu 20261125-03 / TG1.

Chạy: python3 -I tools/do-chat-luong/sosanh.py <thư-mục-gốc> <thư-mục-kết-quả> [--dpi 60]
Cần: pdftoppm (poppler-utils), numpy, pillow. Mỗi tệp in: số trang gốc/kết quả, F1 nét mực (lệch <= 1 px ở dpi đã
chọn), sai khác màu trung bình (0-255, trên vùng có mực). Không ghi gì ra ngoài thư mục tạm.
"""
import argparse
import subprocess
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image


def pages(pdf: Path, dpi: int, tmp: Path, tag: str) -> list[np.ndarray]:
    prefix = tmp / tag
    subprocess.run(["pdftoppm", "-r", str(dpi), "-png", str(pdf), str(prefix)], check=True,
                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    files = sorted(tmp.glob(f"{tag}-*.png"), key=lambda p: int(p.stem.rsplit("-", 1)[1]))
    return [np.asarray(Image.open(f).convert("RGB"), dtype=np.int16) for f in files]


def dilate(mask: np.ndarray) -> np.ndarray:
    out = mask.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            out |= np.roll(np.roll(mask, dy, 0), dx, 1)
    return out


def compare(a: np.ndarray, b: np.ndarray) -> tuple[float, float]:
    h, w = min(a.shape[0], b.shape[0]), min(a.shape[1], b.shape[1])
    a, b = a[:h, :w], b[:h, :w]
    ia, ib = a.mean(axis=2) < 170, b.mean(axis=2) < 170
    if not ia.any() and not ib.any():
        return 1.0, 0.0
    precision = (ib & dilate(ia)).sum() / max(ib.sum(), 1)
    recall = (ia & dilate(ib)).sum() / max(ia.sum(), 1)
    f1 = 0 if precision + recall == 0 else 2 * precision * recall / (precision + recall)
    ink = ia | ib
    colour = float(np.abs(a - b).mean(axis=2)[ink].mean()) if ink.any() else 0.0
    return float(f1), colour


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("goc", type=Path)
    ap.add_argument("ketqua", type=Path)
    ap.add_argument("--dpi", type=int, default=60)
    args = ap.parse_args()
    rows = []
    for src in sorted(args.goc.glob("*.pdf")):
        out = args.ketqua / src.name
        if not out.exists():
            print(f"THIẾU  {src.name}")
            continue
        with tempfile.TemporaryDirectory() as t:
            tmp = Path(t)
            pa, pb = pages(src, args.dpi, tmp, "a"), pages(out, args.dpi, tmp, "b")
        scores = [compare(x, y) for x, y in zip(pa, pb)]
        f1 = float(np.mean([s[0] for s in scores])) if scores else 0.0
        if len(pa) != len(pb):
            f1 *= min(len(pa), len(pb)) / max(len(pa), len(pb))
        colour = float(np.mean([s[1] for s in scores])) if scores else 0.0
        rows.append(f1)
        worst = min(range(len(scores)), key=lambda i: scores[i][0]) + 1 if scores else 0
        print(f"{len(pa):3d}/{len(pb):3d} trang  F1 {f1:.3f}  màu {colour:5.1f}  trang kém nhất {worst}  {src.name}")
    if rows:
        print(f"TRUNG BÌNH F1 {np.mean(rows):.3f} trên {len(rows)} tệp")
    return 0


if __name__ == "__main__":
    sys.exit(main())
