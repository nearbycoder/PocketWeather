#!/usr/bin/env bash
# Runs every automated check against the current Linux build and prints a summary.
#
#   Tools/selftest.sh            static validator, keyboard/gamepad/touch self-tests, UI audit at
#                                four window shapes, and the expert AutoPilot over all 12 levels
#   Tools/selftest.sh --quick    skips the AutoPilot campaign
#
# Exit code is non-zero if anything failed. Logs go to /tmp/pw-selftest/.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
OUT=/tmp/pw-selftest
rm -rf "$OUT"; mkdir -p "$OUT"
PLAY="$ROOT/Tools/play.sh"
fail=0
row() { printf '  %-34s %s\n' "$1" "$2"; }

echo "Pocket Weather self-test ($(git -C "$ROOT" log --oneline -1 2>/dev/null))"

if python3 "$ROOT/Tools/validate_levels.py" > "$OUT/validate.txt" 2>&1; then row "level validator" "PASS"; else row "level validator" "FAIL (see $OUT/validate.txt)"; fail=1; fi

run_test() {   # name flag tag
  local name=$1 flag=$2 tag=$3 log="$OUT/$1.log"
  timeout 400 "$PLAY" -logFile "$log" "$flag" > /dev/null 2>&1
  local done; done=$(grep -m1 "\[$tag\] done:" "$log" | sed 's/.*done: //')
  if [ -n "$done" ] && echo "$done" | grep -q " 0 failed"; then row "$name" "PASS ($done)"; else row "$name" "FAIL (${done:-no result}; see $log)"; fail=1; fi
}
run_test keyboard -pwKeyTest KeyTest
run_test gamepad -pwPadTest PadTest
run_test touch -pwTouchTest TouchTest

for r in 1600x900 1600x720 1200x900 720x1280; do
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

[ $fail = 0 ] && echo "all checks passed" || echo "SOME CHECKS FAILED"
exit $fail
