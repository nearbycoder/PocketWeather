#!/usr/bin/env bash
# Runs the built Linux player (windowed 1600x900). Extra args are passed through, e.g.
#   Tools/play.sh -pwCapture /tmp/shots       scripted screenshot tour, then quit
#   Tools/play.sh -pwAutopilot /tmp/auto      the bot plays every level and prints PASS/FAIL
# On this machine the XWayland path can hang at startup, so use the native Wayland backend
# when a Wayland session is available.
set -euo pipefail
GAME="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/Builds/Linux/PocketWeather.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build first." >&2; exit 1; }
args=(-screen-fullscreen 0 -screen-width "${PW_W:-1600}" -screen-height "${PW_H:-900}")
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
exec "$GAME" "${args[@]}" "$@"
