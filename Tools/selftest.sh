#!/usr/bin/env bash
# Runs every automated check against the current Linux build and prints a summary.
#
#   Tools/selftest.sh            static validator, audio loop seams, keyboard/gamepad/touch self-tests, UI audit at
#                                eight window shapes (two landscape phones, three portrait), and the expert AutoPilot over all 12 levels
#   Tools/selftest.sh --quick    skips the AutoPilot campaign
#
# Exit code is non-zero if anything failed. Logs go to Recordings/selftest/ (gitignored; /tmp is a
# shared RAM disk on the machine this was built on).
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# every window opens in a private KWin on a virtual screen (Tools/nested.sh), never on the desktop;
# PW_NESTED=0 opens ordinary windows instead
if [ -z "${PW_IN_NESTED:-}" ] && [ "${PW_NESTED:-1}" != 0 ] && command -v kwin_wayland > /dev/null; then
  exec "$ROOT/Tools/nested.sh" "$ROOT/Tools/selftest.sh" "$@"
fi
OUT="$ROOT/Recordings/selftest"
rm -rf "$OUT"; mkdir -p "$OUT"
PLAY="$ROOT/Tools/play.sh"
# a fresh save and settings for this run, never the real ones in ~/.config/unity3d (play.sh
# would sandbox these runs anyway; this makes the sandbox per-run)
export PW_CONFIG="$OUT/config"
unset PW_REAL_PREFS
fail=0
row() { printf '  %-34s %s\n' "$1" "$2"; }

echo "Pocket Weather self-test ($(git -C "$ROOT" log --oneline -1 2>/dev/null))"

if python3 "$ROOT/Tools/validate_levels.py" > "$OUT/validate.txt" 2>&1; then row "level validator" "PASS"; else row "level validator" "FAIL (see $OUT/validate.txt)"; fail=1; fi

if "$ROOT/Tools/.venv/bin/python" "$ROOT/Tools/audio/check_loops.py" > "$OUT/loops.txt" 2>&1; then row "audio loop seams" "PASS"; else row "audio loop seams" "FAIL (see $OUT/loops.txt)"; fail=1; fi

run_test() {   # name flag tag
  local name=$1 flag=$2 tag=$3 log="$OUT/$1.log"
  timeout 400 "$PLAY" -logFile "$log" "$flag" > /dev/null 2>&1
  local done; done=$(grep -m1 "\[$tag\] done:" "$log" | sed 's/.*done: //')
  if [ -n "$done" ] && echo "$done" | grep -q " 0 failed"; then row "$name" "PASS ($done)"; else row "$name" "FAIL (${done:-no result}; see $log)"; fail=1; fi
}
run_test keyboard -pwKeyTest KeyTest
run_test gamepad -pwPadTest PadTest
run_test touch -pwTouchTest TouchTest

for r in 1600x900 1600x720 1200x900 844x390 740x360 720x1280 390x844 360x800; do
  log="$OUT/ui-$r.log"
  PW_W=${r%x*} PW_H=${r#*x} timeout 400 "$PLAY" -logFile "$log" -pwUiAudit > /dev/null 2>&1
  done=$(grep -m1 "\[UiAudit\] done:" "$log" | sed 's/.*done: //')
  if [ -n "$done" ] && echo "$done" | grep -q " 0 failed"; then row "UI audit $r" "PASS ($done)"; else row "UI audit $r" "FAIL (${done:-no result}; see $log)"; fail=1; fi
done

if [ "${1:-}" != "--quick" ]; then
  log="$OUT/autopilot.log"
  timeout 2400 "$PLAY" -logFile "$log" -pwAutopilot "$OUT/autopilot" -pwFreshSave -pwDelights -pwSpeed 2 > /dev/null 2>&1
  done=$(grep -m1 "\[AutoPilot\] done:" "$log" | sed 's/.*done: //')
  delights=$(grep "\[AutoPilot\] PASS" "$log" | sort -u | grep -c "delight found")
  ex=$(grep -c "Exception" "$log")
  if [ -n "$done" ] && echo "$done" | grep -q " 0 failed" && [ "$ex" = 0 ]; then row "AutoPilot campaign" "PASS ($done, delights $delights/12)"; else row "AutoPilot campaign" "FAIL (${done:-no result}, exceptions $ex; see $log)"; fail=1; fi
fi

# every run above kept its prefs in the game's own folder of the sandbox, not unity3d/unknown/unknown
appinfo="$ROOT/Builds/Linux/PocketWeather_Data/app.info"
own="$PW_CONFIG/unity3d/$(sed -n 1p "$appinfo")/$(sed -n 2p "$appinfo")/prefs"
if [ -f "$own" ] && [ ! -e "$PW_CONFIG/unity3d/unknown" ]; then row "prefs in the game's own folder" "PASS"; else row "prefs in the game's own folder" "FAIL ($(ls "$PW_CONFIG/unity3d" 2>/dev/null | tr '\n' ' '))"; fail=1; fi

# with a private KWin, every game window went there and none to the desktop's X server
if [ -n "${PW_IN_NESTED:-}" ]; then
  shown=$(grep -h "\[PW\] display: " "$OUT"/*.log 2>/dev/null | sed 's/.*display: //' | sort | uniq -c | sed 's/^ *//' | tr '\n' ';')
  if [ -n "$shown" ] && ! grep -h "\[PW\] display: " "$OUT"/*.log | grep -qv "display: Wayland $PW_IN_NESTED, X11 -"; then row "windows in a private KWin" "PASS ($shown)"; else row "windows in a private KWin" "FAIL (${shown:-no display logged})"; fail=1; fi
fi

[ $fail = 0 ] && echo "all checks passed" || echo "SOME CHECKS FAILED"
exit $fail
