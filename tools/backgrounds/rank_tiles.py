#!/usr/bin/env python3
"""Bewertet die Teilstuecke eines Mosaiks: welches taugt als Website-Hintergrund?

Nutzt ein KOSTENLOSES Multimodell-Modell (`:free`) und gibt nur Text zurueck -
es entstehen keine Bildkosten. Ergebnis ist eine JSON-Datei mit Score und
Kurzbeschreibung je Teilstueck.

Score:
  10 = eigenstaendige photographische Szene mit Raum fuer ein breites Bild
   7-9 = gut geeignet
   4-6 = brauchbar
   0-3 = unbrauchbar (Schrift, Dokument, Bauplan, Landkarte, Logo, Symbol,
        Ornament, reine Nahaufnahme, Grafik)

Aufruf:
    python rank_tiles.py --in /tmp/slices/header_slides_1 --out /tmp/ranking.json
    python rank_tiles.py --in /tmp/slices --out /tmp/ranking.json --workers 8
"""
import argparse
import base64
import json
import time
import urllib.error
import urllib.request
from concurrent.futures import ThreadPoolExecutor
from pathlib import Path

API_URL = "https://openrouter.ai/api/v1/chat/completions"
HERMES_ENV = Path.home() / ".hermes" / ".env"

PROMPT = """Du beurteilst ein Bild als moeglichen Website-Hintergrund fuer eine Verschwoerungs-Ermittlungsseite.
Antworte ausschliesslich mit JSON: {"scene": "<Kurzbeschreibung auf Deutsch, max 10 Worte>", "score": <0-10>}
score: 10 = eigenstaendige photographische Szene mit Raum fuer ein breites Bild (Motiv erkennbar, nicht abgeschnitten, kein Schrift-Dokument, kein Symbol, kein Muster), 7-9 = gut geeignet, 4-6 = brauchbar, 0-3 = unbrauchbar (Schrift, Dokument, Bauplan, Landkarte, Logo, Symbol, Ornament, reine Nahaufnahme, Grafik)."""


def load_key() -> str:
    import os

    key = os.environ.get("OPENROUTER_API_KEY", "").strip()
    if key:
        return key
    if HERMES_ENV.exists():
        for line in HERMES_ENV.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.strip().startswith("OPENROUTER_API_KEY="):
                return line.split("=", 1)[1].strip()
    raise SystemExit("Kein OPENROUTER_API_KEY gefunden (Umgebung oder ~/.hermes/.env).")


def grade(key: str, model: str, path: Path) -> tuple[str, dict]:
    img = base64.b64encode(path.read_bytes()).decode()
    payload = {
        "model": model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {"type": "text", "text": PROMPT},
                    {"type": "image_url", "image_url": {"url": f"data:image/png;base64,{img}"}},
                ],
            }
        ],
    }
    req = urllib.request.Request(
        API_URL,
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
    )
    for attempt in range(2):
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                data = json.load(resp)
            text = data["choices"][0]["message"]["content"]
            parsed = json.loads(text[text.find("{") : text.rfind("}") + 1])
            return str(path), parsed
        except Exception:  # noqa: BLE001
            time.sleep(2)
    return str(path), {"scene": "", "score": 0}


def main() -> int:
    ap = argparse.ArgumentParser(description="Teilstuecke als Hintergrund bewerten (kostenlos)")
    ap.add_argument("--in", dest="src", required=True, help="Verzeichnis mit Teilstuecken")
    ap.add_argument("--out", dest="dst", required=True, help="Ziel-JSON")
    ap.add_argument("--model", default="minimax/minimax-m3.1-flash:free")
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--min-score", type=int, default=6, help="ab diesem Score = tauglich")
    args = ap.parse_args()

    src = Path(args.src)
    files = sorted(p for p in src.rglob("*.png") if p.name != "manifest.json")
    if not files:
        print(f"Keine Teilstuecke unter {src}", file=sys.stderr)
        return 1

    key = load_key()
    print(f"Bewerte {len(files)} Teilstuecke mit {args.model} (kostenlos, nur Textausgabe)")
    results: dict[str, dict] = {}
    with ThreadPoolExecutor(max_workers=args.workers) as ex:
        for i, (path, verdict) in enumerate(ex.map(lambda p: grade(key, args.model, p), files), 1):
            results[path] = verdict
            if i % 40 == 0:
                print(f"  ... {i}/{len(files)}")

    out = Path(args.dst)
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(results, indent=1, ensure_ascii=False), encoding="utf-8")

    good = sorted(
        ((v["score"], k, v["scene"]) for k, v in results.items() if v["score"] >= args.min_score),
        reverse=True,
    )
    print(f"\nTauglich (Score >= {args.min_score}): {len(good)} von {len(files)}")
    for score, path, scene in good:
        print(f"  {score:2d}  {Path(path).parent.name}/{Path(path).name:32s} {scene[:55]}")
    print(f"\nGespeichert: {out}")
    return 0


if __name__ == "__main__":
    import sys

    raise SystemExit(main())
