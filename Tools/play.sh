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
#
# Tool runs also open their window in a private KWin on a virtual screen when kwin_wayland is
# installed (Tools/nested.sh), so a test can't cover or go fullscreen on the desktop. PW_NESTED=0
# opens an ordinary window instead.
#
# No -screen-fullscreen: given that flag, the Unity player opens its prefs before it has read the
# game's name and keeps them in unity3d/unknown/unknown/ (a file other Unity games share). Tool
# runs start windowed because their sandbox's prefs are seeded with Unity's window-mode keys; a
# plain Tools/play.sh uses your own fullscreen setting, like a double-clicked build.
set -euo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
GAME="$ROOT/Builds/Linux/PocketWeather.x86_64"
[ -x "$GAME" ] || { echo "No build yet. Run Tools/unity.sh build first." >&2; exit 1; }
args=(-screen-width "${PW_W:-1600}" -screen-height "${PW_H:-900}")
[ -n "${WAYLAND_DISPLAY:-}" ] && args+=(-force-wayland)
# Sets "Screenmanager Fullscreen mode" to windowed (3) in a sandbox's prefs. Unity only honours it
# while "... Default" matches the project's fullscreenMode, so that's written too.
seed_windowed() {
  python3 - "$1" "$GAME" "$ROOT/ProjectSettings/ProjectSettings.asset" <<'PY'
import os, re, sys
cfg, game, settings = sys.argv[1:]
company, product = open(os.path.join(os.path.dirname(game), "PocketWeather_Data", "app.info")).read().splitlines()[:2]
default = re.search(r"^\s*fullscreenMode: (\d+)", open(settings).read(), re.M).group(1)
d = os.path.join(cfg, "unity3d", company, product); os.makedirs(d, exist_ok=True)
f = os.path.join(d, "prefs")
xml = open(f).read() if os.path.exists(f) else '<unity_prefs version_major="1" version_minor="1">\n</unity_prefs>\n'
for k, v in (("Screenmanager Fullscreen mode Default", default), ("Screenmanager Fullscreen mode", "3")):
    line = f'\t<pref name="{k}" type="int">{v}</pref>\n'
    xml, n = re.subn(rf'[ \t]*<pref name="{re.escape(k)}" type="int">[^<]*</pref>\n?', line, xml)
    if not n: xml = xml.replace("</unity_prefs>", line + "</unity_prefs>")
open(f, "w").write(xml)
PY
}
# Tool runs open their window in a private KWin on a virtual screen (Tools/nested.sh), so they can't
# reach the desktop; PW_NESTED=0 opens an ordinary window instead. A plain Tools/play.sh is untouched.
is_tool_run() {
  for a in "$@"; do
    case "$a" in -pwAutopilot|-pwCapture|-pwKeyTest|-pwPadTest|-pwTouchTest|-pwUiAudit|-pwPerf|-pwVideo|-pwTrailer|-pwFreshSave) return 0 ;; esac
  done
  return 1
}
if [ -z "${PW_IN_NESTED:-}" ] && [ "${PW_NESTED:-1}" != 0 ] && command -v kwin_wayland > /dev/null && is_tool_run "$@"; then
  exec "$ROOT/Tools/nested.sh" "$ROOT/Tools/play.sh" "$@"
fi
if [ "${PW_REAL_PREFS:-0}" != 1 ]; then
  if is_tool_run "$@"; then
    export XDG_CONFIG_HOME="${PW_CONFIG:-$ROOT/Recordings/config}"
    mkdir -p "$XDG_CONFIG_HOME"
    seed_windowed "$XDG_CONFIG_HOME"
  fi
fi
exec "$GAME" "${args[@]}" "$@"
