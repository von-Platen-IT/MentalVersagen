#!/usr/bin/env python3
"""Optimiert Hintergrundbilder fuer den Webeinsatz.

Die KI-Bilder kommen als 3136 x 1344 px JPEG (~1,2 MB). Fuer die Website wird
daraus eine Web-Optimierung:

  1. auf Zielbreite verkleinern (Standard 2560 px)
  2. JPEG neu kodieren: Qualitaet 80, progressiv, ohne EXIF
  3. zusaetzlich eine WebP-Variante (deutlich kleiner, von allen modernen
     Browsern unterstuetzt)

Die Originale bleiben unangetastet - das Skript schreibt neue Dateien und
rührt die Quelle nicht an.

Aufruf:
    python optimize_images.py --in /pfad/mit/bildern
    python optimize_images.py --in /pfad/mit/bildern --width 2560 --quality 80
    python optimize_images.py --in /pfad/mit/bildern --replace   # JPEG in-place
"""
import argparse
import json
from pathlib import Path

from PIL import Image


def optimize_one(
    src: Path, out_jpeg: Path, out_webp: Path, width: int, quality: int
) -> dict:
    im = Image.open(src).convert("RGB")
    orig_size = im.size
    # Groesse VOR dem Ueberschreiben messen - bei --replace ist src == out_jpeg
    orig_bytes = src.stat().st_size
    if width and im.width > width:
        ratio = width / im.width
        im = im.resize((width, round(im.height * ratio)), Image.LANCZOS)
    if out_jpeg:
        out_jpeg.parent.mkdir(parents=True, exist_ok=True)
        im.save(out_jpeg, "JPEG", quality=quality, optimize=True, progressive=True)
    if out_webp:
        out_webp.parent.mkdir(parents=True, exist_ok=True)
        im.save(out_webp, "WEBP", quality=quality, method=6)
    return {
        "quelle": src.name,
        "original_groesse": f"{orig_size[0]} x {orig_size[1]}",
        "neu_groesse": f"{im.width} x {im.height}",
        "original_kb": round(orig_bytes / 1024),
        "jpeg_kb": round(out_jpeg.stat().st_size / 1024) if out_jpeg else None,
        "webp_kb": round(out_webp.stat().st_size / 1024) if out_webp else None,
    }


def main() -> int:
    ap = argparse.ArgumentParser(description="Bilder fuer die Website optimieren")
    ap.add_argument("--in", dest="src", required=True, help="Verzeichnis mit Bildern")
    ap.add_argument("--pattern", default="*.jpg", help="Glob der Eingabedateien")
    ap.add_argument("--width", type=int, default=2560, help="Zielbreite in px (0 = keine Verkleinerung)")
    ap.add_argument("--quality", type=int, default=80, help="JPEG/WebP-Qualitaet")
    ap.add_argument("--webp", action="store_true", help="zusaetzlich WebP-Variante erzeugen")
    ap.add_argument("--replace", action="store_true", help="JPEG-Datei im Quellordner ersetzen")
    ap.add_argument("--report", default="", help="JSON-Bericht schreiben")
    args = ap.parse_args()

    src = Path(args.src)
    files = sorted(p for p in src.glob(args.pattern) if p.is_file())
    if not files:
        print(f"Keine Dateien {args.pattern} in {src}")
        return 1

    rows = []
    for f in files:
        if args.replace:
            out_jpeg, out_webp = f, (f.with_suffix(".webp") if args.webp else None)
        else:
            out_jpeg = f.with_name(f.stem + "_web.jpg")
            out_webp = f.with_name(f.stem + "_web.webp") if args.webp else None
        row = optimize_one(f, out_jpeg, out_webp, args.width, args.quality)
        rows.append(row)
        line = f"{f.name}: {row['original_kb']} KB -> JPEG {row['jpeg_kb']} KB"
        if row["webp_kb"]:
            line += f" | WebP {row['webp_kb']} KB"
        print(f"  {line}  ({row['original_groesse']} -> {row['neu_groesse']})")

    vor = sum(r["original_kb"] for r in rows)
    nach = sum(r["jpeg_kb"] for r in rows)
    print(f"\nGesamt: {vor} KB -> {nach} KB  (JPEG, -{round(100 - nach / vor * 100)} %)")
    if args.webp:
        w = sum(r["webp_kb"] for r in rows)
        print(f"        WebP gesamt: {w} KB  (-{round(100 - w / vor * 100)} %)")
    if args.report:
        Path(args.report).write_text(json.dumps(rows, indent=2), encoding="utf-8")
        print(f"Bericht: {args.report}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
