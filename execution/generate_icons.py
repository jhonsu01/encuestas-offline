#!/usr/bin/env python3
"""Genera los iconos de las apps (Android + Windows) para múltiples pantallas.

- Windows: assets/app.ico multi-resolución (16..256).
- Android: mipmaps ic_launcher.png / ic_launcher_round.png en mdpi..xxxhdpi.

Diseño: portapapeles blanco con check sobre fondo teal (identidad "Encuestas").
Ejecutar:  python execution/generate_icons.py
"""
import os
from PIL import Image, ImageDraw

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

TEAL = (0, 105, 92, 255)
TEAL2 = (0, 137, 123, 255)
WHITE = (255, 255, 255, 255)
GREEN = (46, 125, 50, 255)


def draw_icon(size: int, shape: str = "square") -> Image.Image:
    S = size
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    if shape == "circle":
        d.ellipse([0, 0, S - 1, S - 1], fill=TEAL)
    else:
        d.rounded_rectangle([0, 0, S - 1, S - 1], radius=int(S * 0.22), fill=TEAL)

    # Cuerpo del portapapeles
    bx0, by0, bx1, by1 = int(S * 0.30), int(S * 0.26), int(S * 0.70), int(S * 0.80)
    d.rounded_rectangle([bx0, by0, bx1, by1], radius=int(S * 0.05), fill=WHITE)

    # Pinza superior
    cw = int(S * 0.18)
    cx0, cx1 = int(S * 0.5 - cw / 2), int(S * 0.5 + cw / 2)
    d.rounded_rectangle([cx0, int(S * 0.20), cx1, int(S * 0.30)], radius=int(S * 0.025), fill=TEAL2)

    # Líneas de la lista
    lx0 = int(S * 0.37)
    lh = max(2, int(S * 0.03))
    for i, ly in enumerate([0.40, 0.50, 0.60]):
        y = int(S * ly)
        x1 = int(S * 0.63) if i < 2 else int(S * 0.55)
        d.rounded_rectangle([lx0, y, x1, y + lh], radius=lh // 2, fill=TEAL2)

    # Check de confirmación
    w = max(2, int(S * 0.04))
    d.line(
        [(int(S * 0.55), int(S * 0.62)), (int(S * 0.60), int(S * 0.67)), (int(S * 0.69), int(S * 0.55))],
        fill=GREEN, width=w, joint="curve",
    )
    return img


def gen_windows():
    out_dir = os.path.join(ROOT, "windows-app", "src", "EncuestasCentral", "Assets")
    os.makedirs(out_dir, exist_ok=True)
    base = draw_icon(256, "square")
    sizes = [16, 24, 32, 48, 64, 128, 256]
    base.save(os.path.join(out_dir, "app.ico"), format="ICO",
              sizes=[(s, s) for s in sizes])
    print("Windows: app.ico ->", out_dir)


def gen_android():
    res = os.path.join(ROOT, "android-app", "app", "src", "main", "res")
    densities = {"mdpi": 48, "hdpi": 72, "xhdpi": 96, "xxhdpi": 144, "xxxhdpi": 192}
    for name, px in densities.items():
        d = os.path.join(res, f"mipmap-{name}")
        os.makedirs(d, exist_ok=True)
        draw_icon(px, "square").save(os.path.join(d, "ic_launcher.png"))
        draw_icon(px, "circle").save(os.path.join(d, "ic_launcher_round.png"))
        print(f"Android: mipmap-{name} ({px}px) ->", d)


if __name__ == "__main__":
    gen_windows()
    gen_android()
    print("OK: iconos generados.")
