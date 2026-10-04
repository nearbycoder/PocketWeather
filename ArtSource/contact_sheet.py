"""Tiles preview PNGs into a labelled contact sheet with ffmpeg.

    python3 ArtSource/contact_sheet.py /tmp/prev /tmp/prev/sheet.png [cols]
"""
import glob
import os
import subprocess
import sys

src, out = sys.argv[1], sys.argv[2]
cols = int(sys.argv[3]) if len(sys.argv) > 3 else 5
files = sorted(f for f in glob.glob(os.path.join(src, "*.png")) if not os.path.basename(f).startswith("sheet"))
if not files:
    sys.exit("no previews")
font = "/usr/share/fonts/TTF/DejaVuSans.ttf"
if not os.path.exists(font):
    font = next(iter(glob.glob("/usr/share/fonts/**/DejaVuSans.ttf", recursive=True)), "")
inputs, filters = [], []
for i, f in enumerate(files):
    inputs += ["-i", f]
    label = os.path.splitext(os.path.basename(f))[0].replace(":", "")
    filters.append(f"[{i}:v]scale=320:320,drawtext=fontfile={font}:text='{label}':x=8:y=8:fontsize=18:fontcolor=0x333344[v{i}]")
rows = (len(files) + cols - 1) // cols
pad = rows * cols - len(files)
chain = "".join(f"[v{i}]" for i in range(len(files)))
if pad:
    filters.append(f"color=c=white:s=320x320:d=1[blank]")
    filters.append(f"[blank]split={pad}" + "".join(f"[b{k}]" for k in range(pad)))
    chain += "".join(f"[b{k}]" for k in range(pad))
layout = "|".join(f"{(k % cols) * 320}_{(k // cols) * 320}" for k in range(rows * cols))
filters.append(f"{chain}xstack=inputs={rows * cols}:layout={layout}[out]")
cmd = ["ffmpeg", "-y", "-loglevel", "error"] + inputs + ["-filter_complex", ";".join(filters), "-map", "[out]",
                                                          "-frames:v", "1", out]
subprocess.run(cmd, check=True)
print(out)
