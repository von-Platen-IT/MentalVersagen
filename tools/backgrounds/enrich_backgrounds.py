#!/usr/bin/env python3
"""Erzeugt aus separierten Teilbildern KI-angereicherte Hintergrundbilder.

Nicht einfach vergroessern: das Teilbild dient als Referenz, die Bild-KI
generiert daraus eine eigenstaendige, vollstaendige Szene im Zielformat
(Outpainting / Szenenerweiterung) - nutzbar als Website-Hintergrund.

Bild-KI: OpenRouter Image API. Standard: black-forest-labs/flux-3-image-klein
(günstigste Option, ~0.009 USD/Bild bei 1K, ~0.014 USD/Bild bei 2K).
Key aus OPENROUTER_API_KEY bzw. ~/.hermes/.env.

Kosten:
  - Jeder Lauf hat ein hartes Limit (--max-cost, Default 2 USD).
  - Vor dem Lauf wird die Schaetzung ausgegeben, bei Ueberschreitung bricht das Skript ab.
  - Die tatsaechlichen Kosten stehen in <out>/generated.json und auf der Konsole.

Aufruf:
    python enrich_backgrounds.py --in /tmp/slices/header_slides_1 --out /tmp/bg
    python enrich_backgrounds.py --in ... --out ... --limit 5 --resolution 2K
    python enrich_backgrounds.py --in ... --out ... --dry-run
"""
import argparse
import base64
import io
import json
import mimetypes
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

from PIL import Image

API_URL = "https://openrouter.ai/api/v1/images"
HERMES_ENV = Path.home() / ".hermes" / ".env"

STYLE = (
    "cinematic photographic background for a conspiracy-investigation website, "
    "teal-and-orange colour grade, dramatic directional light, high dynamic range, "
    "fine realistic texture, shallow depth of field, "
    "no text, no captions, no watermarks, no logos, no frames, no borders, "
    "full-bleed image that fills the entire canvas"
)

# Kosten je Bild in USD je Modell und Aufloesung.
# Werte mit "gemessen" sind aus echten API-Antworten (usage.cost) dieser Sitzung;
# "Liste" = OpenRouter-Preisliste, ungeprueft.
# Massgeblich bleibt der Wert aus der API-Antwort (usage.cost).
ESTIMATED_COST_USD = {
    # --- gemessen ---
    "black-forest-labs/flux-3-image-klein": {"1K": 0.009, "2K": 0.014},
    "google/gemini-nano-banana-2.1": {"1K": 0.039, "2K": 0.056},
    "bytedance-seed/seedream-5-0-flash": {"1K": 0.018},
    "bytedance-seed/seedream-5-0-lite": {"2K": 0.035},
    "qwen/qwen-image-2.1-turbo": {"1K": 0.0184},
    # --- nur Preisliste, ungeprueft ---
    "black-forest-labs/flux-3-image": {"1K": 0.041, "2K": 0.048},
    "google/gemini-3.1-flash-lite-image": {"1K": 0.039},
    "google/gemini-3.1-flash-image": {"1K": 0.100, "2K": 0.200},
    "openai/gpt-image-1-mini": {"1K": 0.030, "2K": 0.060},
    "openai/gpt-image-2": {"1K": 0.150, "2K": 0.300},
}

# Modelle, die eine Referenz mit mindestens 256x256 Pixel verlangen.
MIN_REF_PIXELS = 256


def load_key() -> str:
    key = os.environ.get("OPENROUTER_API_KEY", "").strip()
    if key:
        return key
    if HERMES_ENV.exists():
        for line in HERMES_ENV.read_text(encoding="utf-8", errors="replace").splitlines():
            if line.strip().startswith("OPENROUTER_API_KEY="):
                return line.split("=", 1)[1].strip()
    raise SystemExit("Kein OPENROUTER_API_KEY gefunden (Umgebung oder ~/.hermes/.env).")


def prepare_reference(path: Path, min_pixels: int = MIN_REF_PIXELS) -> tuple[str, tuple[int, int]]:
    """Referenz aufbereiten: RGB, mindestens min_pixels Kantenlaenge, Seitenverhaeltnis erhalten.

    Manche Modelle (FLUX 3) verlangen mind. 256x256; die Teilbilder sind oft nur
    100-300 px breit, deshalb wird hochskaliert statt verkleinert.
    """
    img = Image.open(path).convert("RGB")
    orig = img.size
    w, h = img.size
    scale = max(min_pixels / w, min_pixels / h, 1.0)
    if scale > 1.0:
        img = img.resize((max(min_pixels, int(w * scale)), max(min_pixels, int(h * scale))), Image.LANCZOS)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    payload = base64.b64encode(buf.getvalue()).decode()
    return f"data:image/png;base64,{payload}", orig


def build_prompt(subject: str, aspect_ratio: str) -> str:
    return (
        f"Expand this small image into a complete, full-frame {aspect_ratio} background scene. "
        f"The image shows: {subject}. "
        "Re-create that subject as a rich, fully rendered photographic scene that continues "
        "beyond the original crop - keep the same subject, palette and mood, fill the whole "
        "canvas with related environment, foreground and background detail. "
        f"{STYLE}"
    )


def call_api(
    key: str, model: str, prompt: str, ref: str, aspect_ratio: str, resolution: str
) -> dict:
    payload = {
        "model": model,
        "prompt": prompt,
        "input_references": [{"type": "image_url", "image_url": {"url": ref}}],
        "aspect_ratio": aspect_ratio,
        "n": 1,
    }
    if resolution:
        payload["resolution"] = resolution
    req = urllib.request.Request(
        API_URL,
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=600) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")[:600]
        raise RuntimeError(f"HTTP {e.code}: {body}") from e


def save_image(b64: str, out_file: Path) -> tuple[int, int]:
    """Speichert base64-Bild; Endung richtet sich nach dem echten Format."""
    raw = base64.b64decode(b64)
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    target = out_file
    if img.format == "PNG" and out_file.suffix.lower() in {".jpg", ".jpeg"}:
        target = out_file.with_suffix(".png")
    img.save(target, quality=92) if target.suffix.lower() in {".jpg", ".jpeg"} else img.save(target)
    return img.size, target


def credit_balance(key: str) -> tuple[float, float] | None:
    """(verbraucht, frei) in USD laut OpenRouter; None wenn nicht abfragbar."""
    try:
        req = urllib.request.Request(
            "https://openrouter.ai/api/v1/credits",
            headers={"Authorization": f"Bearer {key}"},
        )
        with urllib.request.urlopen(req, timeout=30) as resp:
            data = json.load(resp)["data"]
        total = float(data["total_credits"])
        used = float(data["total_usage"])
        return used, total - used
    except Exception:  # noqa: BLE001
        return None


def pad_to_aspect(ref_data_url: str, aspect_ratio: str) -> str:
    """Referenz auf das Ziel-Seitenverhaeltnis auffuellen (Rand, nicht Verzerrung).

    Noetig, weil Gemini sehr flache Referenzen (z.B. 900x60) mit IMAGE_OTHER
    ablehnt. Der aufgefuellte Rand ist neutral dunkel und stoert die Szene nicht.
    """
    ratios = {"16:9": 16 / 9, "21:9": 21 / 9, "4:3": 4 / 3, "3:2": 3 / 2, "1:1": 1.0}
    target = ratios.get(aspect_ratio, 16 / 9)
    raw = base64.b64decode(ref_data_url.split(",", 1)[1])
    img = Image.open(io.BytesIO(raw)).convert("RGB")
    w, h = img.size
    if w / h > target:
        new_w, new_h = int(h * target), h
    else:
        new_w, new_h = w, int(w / target)
    canvas = Image.new("RGB", (new_w, new_h), (12, 12, 14))
    canvas.paste(img, ((new_w - w) // 2, (new_h - h) // 2))
    buf = io.BytesIO()
    canvas.save(buf, format="PNG")
    return "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()


def describe_with_vision_hint(path: Path) -> str:
    """Platzhalter-Beschreibung; optional durch ein Vision-Modell ersetzen."""
    return path.stem.replace("_", " ")


def describe_image(key: str, path: Path, model: str) -> str:
    """Bildinhalt per Vision-Modell beschreiben (fuer den Szenen-Prompt).

    Nutzt ein kostenloses Multimodell-Modell; faellt bei Fehlern auf den
    Dateinamen zurueck, damit der Lauf nicht abbricht.
    """
    ref, _ = prepare_reference(path, min_pixels=256)
    payload = {
        "model": model,
        "messages": [
            {
                "role": "user",
                "content": [
                    {
                        "type": "text",
                        "text": (
                            "Beschreibe dieses Bild in einem Satz auf Deutsch, sachlich, "
                            "ohne Interpretation. Nenne die zentralen Bildinhalte "
                            "(Personen, Objekte, Umgebung)."
                        ),
                    },
                    {"type": "image_url", "image_url": {"url": ref}},
                ],
            }
        ],
    }
    req = urllib.request.Request(
        "https://openrouter.ai/api/v1/chat/completions",
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=180) as resp:
            data = json.load(resp)
        text = (data["choices"][0]["message"]["content"] or "").strip()
        return " ".join(text.split())[:400]
    except Exception:  # noqa: BLE001
        return describe_with_vision_hint(path)


def call_api_text2image(
    key: str, model: str, prompt: str, aspect_ratio: str, resolution: str
) -> dict:
    """Reines Text-to-Image ohne Referenzbild."""
    payload = {
        "model": model,
        "prompt": prompt,
        "aspect_ratio": aspect_ratio,
        "n": 1,
    }
    if resolution:
        payload["resolution"] = resolution
    req = urllib.request.Request(
        API_URL,
        data=json.dumps(payload).encode(),
        headers={"Authorization": f"Bearer {key}", "Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=600) as resp:
            return json.load(resp)
    except urllib.error.HTTPError as e:
        body = e.read().decode(errors="replace")[:600]
        raise RuntimeError(f"HTTP {e.code}: {body}") from e


def run_themes(
    key: str,
    themes: list[dict],
    dst: Path,
    model: str,
    aspect_ratio: str,
    resolution: str,
    max_cost: float,
    min_credit: float,
    sleep: float,
    retries: int,
) -> int:
    """Text-to-Image-Lauf ueber eine Themenliste (JSON mit id/thema/prompt)."""
    per_model = ESTIMATED_COST_USD.get(model)
    if per_model:
        est_per_image = (
            per_model[resolution]
            if resolution and resolution in per_model
            else max(per_model.values())
        )
    else:
        est_per_image = 0.02
    est_total = est_per_image * len(themes)

    balance = credit_balance(key)
    if balance is not None:
        used, free = balance
        print(f"OpenRouter-Guthaben: {free:.2f} USD frei ({used:.2f} USD verbraucht)")
        if free < min_credit:
            print(f"ABBRUCH: Guthaben {free:.2f} USD unter Schwelle {min_credit:.2f} USD.", file=sys.stderr)
            return 3
        if max_cost > 0 and free - est_total < min_credit:
            print(
                f"ABBRUCH: nach dem Lauf waeren noch {free - est_total:.2f} USD frei.",
                file=sys.stderr,
            )
            return 3
    print(
        f"Modell {model} @ {resolution or 'default'}: ~{est_per_image:.4f} USD/Bild | "
        f"{len(themes)} Bilder | geschaetzt ~{est_total:.2f} USD | Limit {max_cost:.2f} USD"
    )
    if max_cost > 0 and est_total > max_cost:
        print(f"ABBRUCH: Schaetzung {est_total:.2f} USD ueberschreitet Limit {max_cost:.2f} USD.", file=sys.stderr)
        return 2

    manifest_path = dst / "generated.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else []
    done = {e.get("source") for e in manifest}

    for i, t in enumerate(themes, 1):
        name = f"{t['id']}.jpg"
        out_file = dst / name
        if t["id"] in done or out_file.exists():
            print(f"[{i}/{len(themes)}] {name} existiert - uebersprungen")
            continue
        for attempt in range(retries + 1):
            try:
                res = call_api_text2image(key, model, t["prompt"], aspect_ratio, resolution)
                b64 = res["data"][0]["b64_json"]
                size, target = save_image(b64, out_file)
                cost = (res.get("usage") or {}).get("cost")
                print(f"[{i}/{len(themes)}] {target.name} {size[0]}x{size[1]}" + (f" - {cost:.4f} USD" if cost else ""))
                manifest.append(
                    {
                        "source": t["id"],
                        "thema": t.get("thema", ""),
                        "output": target.name,
                        "cost": cost,
                        "model": model,
                        "resolution": resolution,
                        "size": list(size),
                    }
                )
                manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
                break
            except Exception as e:  # noqa: BLE001
                if attempt >= retries:
                    print(f"[{i}/{len(themes)}] {t['id']} FEHLER: {e}", file=sys.stderr)
                else:
                    wait = 5 * (attempt + 1)
                    print(f"   Versuch {attempt + 1} fehlgeschlagen ({e}), {wait}s Pause", file=sys.stderr)
                    time.sleep(wait)
        time.sleep(sleep)

    total = sum(float(e.get("cost") or 0.0) for e in manifest)
    print(f"Fertig. Gesamtkosten in diesem Verzeichnis: {total:.4f} USD")
    return 0


def main() -> int:
    ap = argparse.ArgumentParser(description="KI-Hintergrundbilder aus Teilbildern erzeugen")
    ap.add_argument("--in", dest="src", required=True, help="Verzeichnis mit Teilbildern")
    ap.add_argument("--out", dest="dst", required=True, help="Zielverzeichnis")
    ap.add_argument("--model", default="black-forest-labs/flux-3-image-klein")
    ap.add_argument("--aspect-ratio", default="16:9")
    ap.add_argument("--resolution", default="2K", choices=["", "1K", "1.5K", "2K", "4K"])
    ap.add_argument("--limit", type=int, default=0, help="max. Anzahl Teilbilder (0 = alle)")
    ap.add_argument("--sleep", type=float, default=1.0, help="Pause zwischen Aufrufen (Sek.)")
    ap.add_argument("--retries", type=int, default=2)
    ap.add_argument("--prompt-file", default="", help="Textdatei mit festem Motiv-Prompt")
    ap.add_argument(
        "--max-cost",
        type=float,
        default=1.0,
        help="Hartes Kostenlimit je Lauf in USD (0 = unbegrenzt)",
    )
    ap.add_argument(
        "--min-credit",
        type=float,
        default=10.0,
        help="Abbruch, wenn das OpenRouter-Guthaben unter diesen Wert faellt (USD)",
    )
    ap.add_argument("--dry-run", action="store_true", help="Nur Kostenplan ausgeben, keine API-Aufrufe")
    ap.add_argument(
        "--vision-model",
        default="minimax/minimax-m3.1-flash:free",
        help="Modell fuer die Motiv-Beschreibung (kostenloses Multimodell empfohlen)",
    )
    ap.add_argument(
        "--no-describe",
        action="store_true",
        help="Beschreibung ueberspringen, Dateiname als Motiv verwenden",
    )
    ap.add_argument(
        "--themes",
        default="",
        help="JSON-Datei mit Text-to-Image-Themen (id/thema/prompt) - ohne Referenzbild",
    )
    args = ap.parse_args()

    src = Path(args.src)
    dst = Path(args.dst)
    dst.mkdir(parents=True, exist_ok=True)
    key = load_key()

    # Text-to-Image-Modus: Themenliste statt Referenzbildern
    if args.themes:
        themes = json.loads(Path(args.themes).read_text(encoding="utf-8"))
        if not isinstance(themes, list):
            print("Themen-Datei muss eine JSON-Liste sein", file=sys.stderr)
            return 1
        return run_themes(
            key,
            themes,
            dst,
            args.model,
            args.aspect_ratio,
            args.resolution,
            args.max_cost,
            args.min_credit,
            args.sleep,
            args.retries,
        )

    files = sorted(p for p in src.glob("*.png") if p.name != "manifest.json")
    if args.limit:
        files = files[: args.limit]
    if not files:
        print(f"Keine Teilbilder in {src}", file=sys.stderr)
        return 1

    fixed_prompt = Path(args.prompt_file).read_text(encoding="utf-8") if args.prompt_file else ""
    manifest_path = dst / "generated.json"
    manifest = json.loads(manifest_path.read_text()) if manifest_path.exists() else []

    # --- Kostenrahmen -------------------------------------------------
    per_model = ESTIMATED_COST_USD.get(args.model)
    if per_model:
        if args.resolution and args.resolution in per_model:
            est_per_image = per_model[args.resolution]
        else:
            # Aufloesung vom Modell nicht angeboten: konservativ den teuersten
            # bekannten Wert nehmen, damit die Schaetzung nie zu niedrig ist.
            est_per_image = max(per_model.values())
    else:
        est_per_image = 0.02
    spent = sum(float(e.get("cost") or 0.0) for e in manifest)
    remaining = [f for f in files if not list(dst.glob(f"{f.stem}_bg.*"))]
    est_total = est_per_image * len(remaining)
    print(
        f"Modell {args.model} @ {args.resolution or 'default'}: ~{est_per_image:.4f} USD/Bild | "
        f"{len(remaining)} Bilder offen | geschaetzt ~{est_total:.2f} USD | "
        f"Limit {args.max_cost:.2f} USD | bisher in diesem Verzeichnis {spent:.4f} USD"
    )
    # Guthaben-Stand immer anzeigen (kostenlose Leseabfrage)
    balance = credit_balance(key)
    if balance is not None:
        used, free = balance
        print(f"OpenRouter-Guthaben: {free:.2f} USD frei ({used:.2f} USD verbraucht)")
    else:
        free = None

    if args.dry_run:
        print("Dry-Run: keine API-Aufrufe.")
        return 0
    if args.max_cost > 0 and est_total > args.max_cost:
        print(
            f"ABBRUCH: geschaetzte Kosten {est_total:.2f} USD ueberschreiten das Limit "
            f"{args.max_cost:.2f} USD. Weniger Bilder via --limit oder Limit bewusst erhoehen.",
            file=sys.stderr,
        )
        return 2

    # Guthaben-Waechter: nie unter die Schwelle runterlaufen
    if balance is not None:
        used, free = balance
        if free < args.min_credit:
            print(
                f"ABBRUCH: Guthaben {free:.2f} USD liegt unter der Schwelle "
                f"{args.min_credit:.2f} USD.",
                file=sys.stderr,
            )
            return 3
        if args.max_cost > 0 and free - est_total < args.min_credit:
            print(
                f"ABBRUCH: nach diesem Lauf waeren noch {free - est_total:.2f} USD frei — "
                f"unter der Schwelle {args.min_credit:.2f} USD. Erhoehe --min-credit bewusst.",
                file=sys.stderr,
            )
            return 3

    for i, f in enumerate(remaining, start=1):
        if fixed_prompt:
            subject = fixed_prompt
        elif args.no_describe:
            subject = describe_with_vision_hint(f)
        else:
            subject = describe_image(key, f, args.vision_model)
        prompt = build_prompt(subject, args.aspect_ratio)
        ref, orig_size = prepare_reference(f)
        for attempt in range(args.retries + 1):
            try:
                res = call_api(key, args.model, prompt, ref, args.aspect_ratio, args.resolution)
                b64 = res["data"][0]["b64_json"]
                size, target = save_image(b64, dst / f"{f.stem}_bg.jpg")
                cost = (res.get("usage") or {}).get("cost")
                print(
                    f"[{i}/{len(remaining)}] {target.name} {size[0]}x{size[1]} "
                    f"aus {orig_size[0]}x{orig_size[1]}" + (f" - {cost:.4f} USD" if cost else "")
                )
                manifest.append(
                    {
                        "source": f.name,
                        "output": target.name,
                        "cost": cost,
                        "model": args.model,
                        "resolution": args.resolution,
                        "size": list(size),
                    }
                )
                manifest_path.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
                break
            except Exception as e:  # noqa: BLE001
                # IMAGE_OTHER: Gemini lehnt sehr flache Referenzen ab (z.B. 900x60).
                # Solche Schnitte auf 16:9-Ebene anheben, dann klappt es.
                if "IMAGE_OTHER" in str(e):
                    print(
                        "   Hinweis: Bild-KI lehnt die sehr flache Referenz ab — Referenz wird "
                        "auf 16:9 aufgefüllt und erneut versucht.",
                        file=sys.stderr,
                    )
                    ref = pad_to_aspect(ref, args.aspect_ratio)
                if attempt >= args.retries:
                    print(f"[{i}/{len(remaining)}] {f.name} FEHLER: {e}", file=sys.stderr)
                else:
                    wait = 5 * (attempt + 1)
                    print(f"   Versuch {attempt + 1} fehlgeschlagen ({e}), {wait}s Pause", file=sys.stderr)
                    time.sleep(wait)
        time.sleep(args.sleep)

    total = sum(float(e.get("cost") or 0.0) for e in manifest)
    print(f"Fertig. Gesamtkosten in diesem Verzeichnis: {total:.4f} USD")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
