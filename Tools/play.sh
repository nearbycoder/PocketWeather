#!/usr/bin/env bash
# Runs the built Linux player (windowed 1600x900). Extra args are passed through, e.g.
#   Tools/play.sh -pwCapture /tmp/shots       scripted screenshot tour, then quit
#   Tools/play.sh -pwAutopilot /tmp/auto      the bot plays every level and prints PASS/FAIL
# On this machine the XWayland path can hang at startup, so use the native Wayland backend
# when a Wayland session is available.
#
# With any -pw automation flag, the player's save and settings go to Recordings/config/
# (gitignored) instead of ~/.config/unity3d, so bots and self-tests (some start with
# -pwFreshSave) never touch your real progress. PW_REAL_PREFS=1 opts out; PW_CONFIG=<dir> picks
# another sandbox. A plain Tools/play.sh plays with your real save.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="$ROOT/Builds/Linux/PocketWeather.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build first." >&2; exit 1; }
args=(-screen-fullscreen 0 -screen-width "${PW_W:-1600}" -screen-height "${PW_H:-900}")
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
if [ "${PW_REAL_PREFS:-0}" != 1 ]; then
  for a in "$@"; do
    case "$a" in
      -pwAutopilot|-pwCapture|-pwKeyTest|-pwPadTest|-pwTouchTest|-pwUiAudit|-pwPerf|-pwVideo|-pwTrailer|-pwFreshSave)
        export XDG_CONFIG_HOME="${PW_CONFIG:-$ROOT/Recordings/config}"
        mkdir -p "$XDG_CONFIG_HOME"
        break ;;
    esac
  done
fi
exec "$GAME" "${args[@]}" "$@"
