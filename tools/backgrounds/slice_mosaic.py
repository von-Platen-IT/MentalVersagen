#!/usr/bin/env python3
"""Zerschneidet Mosaik-/Build-Bilder (mehrere Einzelbilder in einem Canvas)
in die einzelnen Teilbilder und speichert sie getrennt.

Erkennung:
  - Zeilen: helle, fast vollstaendig weisse Trennlinien (>=85% helle Pixel in der Zeile)
  - Spalten: rekursiv die staerkste vertikale Kante innerhalb einer Bildreihe,
             mit Mindestbreite je Kachel.

Aufruf:
    python slice_mosaic.py --in pics/header_slides_1.png --out out/header_slides_1
    python slice_mosaic.py --in pics --out out --pattern "header_slides_*.png"
"""
import argparse
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image


def row_separators(gray: np.ndarray, bright_thr: float = 200, cover: float = 0.85) -> list[tuple[int, int]]:
    """Helle, durchgehende Trennzeilen finden -> Liste (start, end)."""
    bright_frac = (gray > bright_thr).mean(axis=1)
    groups: list[list[int]] = []
    for y, frac in enumerate(bright_frac):
        if frac > cover:
            if groups and y - groups[-1][-1] <= 3:
                groups[-1].append(y)
            else:
                groups.append([y])
    return [(g[0], g[-1]) for g in groups]


def strips(gray: np.ndarray, min_height: int = 40) -> list[tuple[int, int]]:
    """Bildreihen zwischen den Trennlinien."""
    out: list[tuple[int, int]] = []
    y = 0
    for start, end in row_separators(gray):
        if start - y > min_height:
            out.append((y, start - 1))
        y = end + 1
    if gray.shape[0] - y > min_height:
        out.append((y, gray.shape[0] - 1))
    return out


def cut_profile(gray: np.ndarray, y0: int, y1: int, diff_thr: float = 18) -> np.ndarray:
    """Kantenstaerke je Spalte (x -> x+1) innerhalb einer Bildreihe."""
    seg = gray[y0 : y1 + 1].astype(float)
    d = np.abs(np.diff(seg, axis=1))
    return d.mean(axis=0)


def _segment(prof: np.ndarray, x0: int, x1: int, min_width: int, depth: int, max_depth: int) -> list[int]:
    """Rekursiv die staerkste Kante im Bereich [x0, x1) suchen und teilen."""
    if x1 - x0 < 2 * min_width or depth > max_depth:
        return []
    lo, hi = x0 + min_width, x1 - min_width
    if hi <= lo:
        return []
    best = int(np.argmax(prof[lo:hi])) + lo
    if prof[best] < 12:
        return []
    return (
        [best]
        + _segment(prof, x0, best, min_width, depth + 1, max_depth)
        + _segment(prof, best, x1, min_width, depth + 1, max_depth)
    )


def column_cuts(gray: np.ndarray, y0: int, y1: int, min_width: int = 90, max_depth: int = 8) -> list[int]:
    prof = cut_profile(gray, y0, y1)
    return sorted(_segment(prof, 0, len(prof), min_width, 0, max_depth))


def slice_image(path: Path, out_dir: Path, min_width: int = 90, min_height: int = 40) -> list[dict]:
    out_dir.mkdir(parents=True, exist_ok=True)
    img = Image.open(path).convert("RGB")
    gray = np.asarray(img.convert("L")).astype(float)
    written: list[dict] = []
    stem = path.stem
    for row_idx, (y0, y1) in enumerate(strips(gray, min_height), start=1):
        cuts = column_cuts(gray, y0, y1, min_width)
        bounds = [0] + cuts + [img.width]
        for col_idx in range(len(bounds) - 1):
            x0, x1 = bounds[col_idx], bounds[col_idx + 1]
            if x1 - x0 < min_width:
                continue
            tile = img.crop((x0, y0, x1, y1 + 1))
            name = f"{stem}_r{row_idx:02d}_c{col_idx + 1:02d}.png"
            tile.save(out_dir / name)
            written.append(
                {"file": name, "row": row_idx, "col": col_idx + 1, "box": [x0, y0, x1, y1 + 1]}
            )
    manifest = out_dir / "manifest.json"
    manifest.write_text(json.dumps({"source": str(path), "tiles": written}, indent=2), encoding="utf-8")
    return written


def main() -> int:
    ap = argparse.ArgumentParser(description="Mosaikbild in Einzelbilder zerschneiden")
    ap.add_argument("--in", dest="src", required=True, help="Bilddatei oder Verzeichnis")
    ap.add_argument("--out", dest="dst", required=True, help="Zielverzeichnis")
    ap.add_argument("--pattern", default="*.png", help="Glob fuer Verzeichnis-Eingabe")
    ap.add_argument("--min-width", type=int, default=90, help="Mindestbreite einer Kachel in px")
    ap.add_argument("--min-height", type=int, default=40, help="Mindesthoehe einer Bildreihe in px")
    args = ap.parse_args()

    src = Path(args.src)
    dst = Path(args.dst)
    files = sorted(src.glob(args.pattern)) if src.is_dir() else [src]
    if not files:
        print(f"Keine Eingabedateien gefunden: {src} / {args.pattern}", file=sys.stderr)
        return 1

    total = 0
    for f in files:
        target = dst / f.stem if src.is_dir() else dst
        tiles = slice_image(f, target, args.min_width, args.min_height)
        total += len(tiles)
        print(f"{f.name}: {len(tiles)} Teilbilder -> {target}")
    print(f"gesamt {total} Teilbilder")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
