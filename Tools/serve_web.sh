#!/usr/bin/env bash
# Serves the WebGL build on the local network, so it can be opened on a phone or tablet on the
# same Wi-Fi (http://<this machine's IP>:8080). Ctrl+C to stop.
set -euo pipefail
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/WebGL"
[ -f "$DIR/index.html" ] || { echo "No WebGL build yet. Run Tools/unity.sh webgl first." >&2; exit 1; }
PORT="${PORT:-8080}"
echo "Serving $DIR on port $PORT. Addresses:"
ip -4 -o addr show scope global 2>/dev/null | awk '{print "  http://" substr($4, 1, index($4, "/") - 1) ":'"$PORT"'"}' || true
cd "$DIR" && exec python3 -m http.server "$PORT"
