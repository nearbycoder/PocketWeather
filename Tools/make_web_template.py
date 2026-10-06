#!/usr/bin/env python3
"""Builds the images for the web page template (Assets/WebGLTemplates/PocketWeather/TemplateData)
from the game's own media, so nothing in the template is hand-made:

  logo.png      the title logo (docs/media/logo.png, made by Tools/make_trailer.py), web-sized
  pip.png       Pip's icon (Assets/Resources/Icons/pip.png, rendered by ArtSource/icons.py)
  favicon.png   64 px Pip; icon-180.png for home screens
  og.jpg        1200x630 link-preview card cropped from the trailer poster

Needs ImageMagick 7 (`magick`).   python3 Tools/make_web_template.py
"""
import os
import subprocess

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "Assets", "WebGLTemplates", "PocketWeather", "TemplateData")
LOGO = os.path.join(ROOT, "docs", "media", "logo.png")
PIP = os.path.join(ROOT, "Assets", "Resources", "Icons", "pip.png")
POSTER = os.path.join(ROOT, "docs", "media", "trailer-poster.jpg")


def magick(*args):
    subprocess.run(["magick", *args], check=True)


def main():
    os.makedirs(OUT, exist_ok=True)
    png = ["-strip", "-depth", "8", "-define", "png:compression-level=9"]
    magick(LOGO, "-resize", "640x", *png, os.path.join(OUT, "logo.png"))
    magick(PIP, "-resize", "192x192", *png, os.path.join(OUT, "pip.png"))
    magick(PIP, "-resize", "64x64", *png, os.path.join(OUT, "favicon.png"))
    # home-screen icon: Pip on the game's sky colour (iOS doesn't do transparency)
    magick("-size", "180x180", "xc:#BFD9F2", "(", PIP, "-resize", "150x150", ")", "-gravity", "center",
           "-composite", *png, os.path.join(OUT, "icon-180.png"))
    magick(POSTER, "-resize", "1200x675^", "-gravity", "center", "-extent", "1200x630", "-strip",
           "-quality", "85", os.path.join(OUT, "og.jpg"))
    for f in sorted(os.listdir(OUT)):
        print(f"{f:14s} {os.path.getsize(os.path.join(OUT, f)) / 1024:7.1f} KB")


if __name__ == "__main__":
    main()
