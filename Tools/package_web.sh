#!/usr/bin/env bash
# Zips the WebGL build into a folder any static host can serve as-is (GitHub Pages, itch.io, Netlify,
# a plain web server): no special headers are needed because the build decompresses itself when the
# server doesn't send Content-Encoding. See docs/HOSTING.md. Nothing is uploaded.
#
#   Tools/package_web.sh            -> Builds/PocketWeather-<version>-web.zip (index.html at the zip's root)
#   Tools/package_web.sh --build    runs Tools/unity.sh webgl first
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB="$ROOT/Builds/WebGL"
[ "${1:-}" = "--build" ] && "$ROOT/Tools/unity.sh" webgl
[ -f "$WEB/index.html" ] || { echo "No WebGL build yet. Run Tools/unity.sh webgl first." >&2; exit 1; }
grep -q "pw-loader" "$WEB/index.html" || { echo "Builds/WebGL wasn't built with the PocketWeather page template; rebuild it." >&2; exit 1; }
VERSION=$(grep -m1 "bundleVersion:" "$ROOT/ProjectSettings/ProjectSettings.asset" | awk '{print $2}')
ZIP="$ROOT/Builds/PocketWeather-$VERSION-web.zip"
rm -f "$ZIP"
chmod -R a+rX "$WEB"   # Unity writes the compressed files owner-only; web servers must be able to read them
[ -d "$WEB/StreamingAssets/Music" ] || { echo "Builds/WebGL has no StreamingAssets/Music (the music); rebuild it." >&2; exit 1; }
(cd "$WEB" && python3 -m zipfile -c "$ZIP" index.html Build TemplateData StreamingAssets)   # python's zip: no extra tools needed
echo "Download sizes (what a player fetches on the first visit, before the title screen):"
(cd "$WEB" && find index.html Build TemplateData -type f -printf '%s %p\n' | sort -k2 |
  awk '{ t += $1; printf "  %-44s %9.1f KB\n", $2, $1 / 1024 } END { printf "  %-44s %9.1f MB\n", "total", t / 1048576 }')
echo "Fetched in the background once the game is running (one music track at a time):"
(cd "$WEB" && find StreamingAssets -type f -printf '%s %p\n' | sort -k2 |
  awk '{ t += $1; printf "  %-44s %9.1f KB\n", $2, $1 / 1024 } END { printf "  %-44s %9.1f MB\n", "total", t / 1048576 }')
echo "Wrote $ZIP ($(du -h "$ZIP" | cut -f1))"
