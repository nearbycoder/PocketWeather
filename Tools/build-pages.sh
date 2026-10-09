#!/usr/bin/env bash
# Builds the site GitHub Pages serves at https://nearbycoder.github.io/PocketWeather/ into
# Builds/pages (gitignored): the WebGL player with index.html at the root, a .nojekyll file, and
# the music and ambience in StreamingAssets/. Pages serves static files from a subpath and can't
# set headers, so everything is relative and the Brotli files decompress themselves in the browser
# (Decompression Fallback; see docs/HOSTING.md). Nothing is pushed or deployed.
#
#   Tools/build-pages.sh               build the WebGL player (Tools/unity.sh webgl), then stage Builds/pages
#   Tools/build-pages.sh --no-build    stage Builds/pages from the existing Builds/WebGL
#   PAGES_URL=https://example.org/pw/ Tools/build-pages.sh   another address for the link-preview tags
#
# Then: node Tools/check-pages.mjs <url> (see its header) to check a served copy.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEB="$ROOT/Builds/WebGL"
OUT="$ROOT/Builds/pages"
URL="${PAGES_URL:-https://nearbycoder.github.io/PocketWeather/}"
case "${1:-}" in
  --no-build) ;;
  "") nice -n 10 "$ROOT/Tools/unity.sh" webgl ;;
  *) echo "usage: $0 [--no-build]" >&2; exit 2 ;;
esac
[ -f "$WEB/index.html" ] || { echo "No WebGL build yet. Run Tools/unity.sh webgl first." >&2; exit 1; }
grep -q "pw-loader" "$WEB/index.html" || { echo "Builds/WebGL wasn't built with the PocketWeather page template; rebuild it." >&2; exit 1; }
for d in Build TemplateData StreamingAssets/Music StreamingAssets/Ambience; do
  [ -d "$WEB/$d" ] || { echo "Builds/WebGL has no $d; rebuild it." >&2; exit 1; }
done
# Pages can't send Content-Encoding, so compressed files must carry the loader's own decompressor
# (Unity names those .unityweb); plain .br/.gz names would need the header
if ls "$WEB/Build" | grep -qE '\.(br|gz)$'; then
  echo "Builds/WebGL/Build has .br/.gz files, which need a Content-Encoding header Pages can't send. Rebuild with Decompression Fallback on (BuildScript.BuildWebGL does)." >&2; exit 1
fi

rm -rf "$OUT"
mkdir -p "$OUT"
cp -r "$WEB/index.html" "$WEB/Build" "$WEB/TemplateData" "$WEB/StreamingAssets" "$OUT/"
touch "$OUT/.nojekyll"   # serve the files as they are, no Jekyll pass
chmod -R a+rX "$OUT"     # Unity writes the compressed files owner-only
# link previews want an absolute image URL, now that the address is known
sed -i "s|<meta property=\"og:image\" content=\"TemplateData/og.jpg\">|<meta property=\"og:url\" content=\"$URL\">\n    <meta property=\"og:image\" content=\"${URL}TemplateData/og.jpg\">|" "$OUT/index.html"

# every URL the page itself names must be relative, so it works under /PocketWeather/
if grep -nE '(src|href)="/|Url: *"/|streamingAssetsUrl: *"/' "$OUT/index.html"; then
  echo "index.html names a root-relative URL (above); it would miss the /PocketWeather/ subpath." >&2; exit 1
fi
# GitHub refuses files over 100 MB; keep each under 50 MB and the site well under 1 GB
big=$(find "$OUT" -type f -size +50M -printf '%s %P\n')
if [ -n "$big" ]; then
  echo "Files over 50 MB:" >&2; echo "$big" >&2
  if find "$OUT" -type f -size +95M | grep -q .; then echo "A file is near or over GitHub's 100 MB limit." >&2; exit 1; fi
fi
total=$(du -sb "$OUT" | cut -f1)
[ "$total" -lt 1000000000 ] || { echo "The site is over 1 GB." >&2; exit 1; }

echo "First visit, before the title screen:"
(cd "$OUT" && find index.html Build TemplateData -type f -printf '%s %p\n' | sort -k2 |
  awk '{ t += $1; printf "  %-44s %9.1f KB\n", $2, $1 / 1024 } END { printf "  %-44s %9.1f MB\n", "total", t / 1048576 }')
echo "Fetched in the background once the game runs:"
(cd "$OUT" && find StreamingAssets -type f -printf '%s %p\n' |
  awk '{ t += $1; n++ } END { printf "  %d files, %.1f MB\n", n, t / 1048576 }')
largest=$(find "$OUT" -type f -printf '%s %P\n' | sort -n | tail -1)
echo "Largest file: ${largest#* } ($(awk -v b="${largest%% *}" 'BEGIN { printf "%.1f MB", b / 1048576 }'))"
echo "Wrote $OUT ($(awk -v b="$total" 'BEGIN { printf "%.1f MB", b / 1048576 }'), $(find "$OUT" -type f | wc -l) files) for $URL"
