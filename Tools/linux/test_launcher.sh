#!/usr/bin/env bash
# Checks Tools/linux/PocketWeather.sh with stand-in games (small shell scripts), so no window opens:
# a crash at start is tried again once with the same arguments, two crashes stop with the crash's
# status, a clean or failed exit and a crash late in play aren't retried, and stopping the launcher
# stops the game. Prints PASS/FAIL per case and "done: N passed, M failed"; exits non-zero on any
# failure. Works in the gitignored Recordings/ (not the shared /tmp) and cleans up after itself.
set -uo pipefail
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# PW_SH=dash runs the launcher under another POSIX shell (Debian and Ubuntu's /bin/sh)
LAUNCHER="$ROOT/Tools/linux/PocketWeather.sh"
if [ -n "${PW_SH:-}" ]; then
  LAUNCHER_SH="$LAUNCHER"; LAUNCHER="$ROOT/Recordings/launcher-under-$PW_SH-$$"
  printf '#!/bin/sh\nexec %s %s "$@"\n' "$PW_SH" "$LAUNCHER_SH" > "$LAUNCHER"; chmod +x "$LAUNCHER"
fi
WORK="$ROOT/Recordings/launcher-test-$$"
mkdir -p "$WORK"
trap 'rm -rf "$WORK"; [ -n "${LAUNCHER_SH:-}" ] && rm -f "$LAUNCHER"' EXIT
passes=0 fails=0
check() {   # name ok detail
  if [ "$2" = 1 ]; then echo "[launcher] PASS $1 ($3)"; passes=$((passes + 1)); else echo "[launcher] FAIL $1 ($3)"; fails=$((fails + 1)); fi
}

# a stand-in game: logs each start and its arguments, then does what $1 says
#   crash-once  dies of SIGSEGV the first time, exits 0 the second
#   crash       always dies of SIGSEGV          ok    exits 0          fail  exits 1
#   late-crash  dies of SIGSEGV after 3 s       sleep runs until it's stopped
stub() {
  local d="$WORK/$1"; mkdir -p "$d"
  cat > "$d/game" <<EOF
#!/bin/sh
ulimit -c 0   # no core dumps from the stand-ins' crashes
printf '%s|' "\$@" >> "$d/starts"; echo >> "$d/starts"
echo \$\$ > "$d/pid"
case "$1" in
  crash-once) [ -e "$d/crashed" ] && exit 0; touch "$d/crashed"; kill -SEGV \$\$ ;;
  crash) kill -SEGV \$\$ ;;
  ok) exit 0 ;;
  fail) exit 1 ;;
  late-crash) sleep 3; kill -SEGV \$\$ ;;
  sleep) trap 'echo stopped > "$d/stopped"; exit 143' TERM; while :; do sleep 0.1; done ;;
esac
EOF
  chmod +x "$d/game"
  echo "$d"
}
starts() { [ -f "$1/starts" ] && wc -l < "$1/starts" | tr -d ' ' || echo 0; }
run() {   # mode [env...] -- launcher args
  local d; d=$(stub "$1"); shift
  env PW_GAME="$d/game" WAYLAND_DISPLAY=stub-wayland "$@" > "$d/out" 2>&1
  echo "$? $d"
}

read -r rc d < <(run crash-once "$LAUNCHER" -logFile "a b.log" -pwKeyTest)
args=$(sed -n 2p "$d/starts")
check "a crash at start is tried again once" "$([ "$rc" = 0 ] && [ "$(starts "$d")" = 2 ] && echo 1)" "status $rc, $(starts "$d") starts"
check "the second start gets the same arguments" "$([ "$args" = "-force-wayland|-logFile|a b.log|-pwKeyTest|" ] && [ "$(sed -n 1p "$d/starts")" = "$args" ] && echo 1)" "$args"
check "it says why it starts again" "$(grep -q "starting it again" "$d/out" && echo 1)" "$(head -c 120 "$d/out")"

read -r rc d < <(run crash "$LAUNCHER")
check "two crashes stop with the crash's status" "$([ "$rc" = 139 ] && [ "$(starts "$d")" = 2 ] && echo 1)" "status $rc, $(starts "$d") starts"

read -r rc d < <(run ok "$LAUNCHER")
check "a clean exit isn't retried" "$([ "$rc" = 0 ] && [ "$(starts "$d")" = 1 ] && echo 1)" "status $rc, $(starts "$d") starts"

read -r rc d < <(run fail "$LAUNCHER")
check "an exit with status 1 isn't retried" "$([ "$rc" = 1 ] && [ "$(starts "$d")" = 1 ] && echo 1)" "status $rc, $(starts "$d") starts"

read -r rc d < <(run late-crash PW_RETRY_WINDOW=2 "$LAUNCHER")
check "a crash after the start-up window isn't retried" "$([ "$rc" = 139 ] && [ "$(starts "$d")" = 1 ] && echo 1)" "status $rc, $(starts "$d") starts"

# stopping the launcher stops the game, and isn't taken for a crash
d=$(stub sleep)
PW_GAME="$d/game" "$LAUNCHER" > "$d/out" 2>&1 &
launcher=$!
for _ in $(seq 50); do [ -f "$d/pid" ] && break; sleep 0.1; done
sleep 0.3
kill -TERM "$launcher"
for _ in $(seq 50); do kill -0 "$launcher" 2> /dev/null || break; sleep 0.1; done
wait "$launcher" 2> /dev/null; rc=$?
game=$(cat "$d/pid" 2> /dev/null)
alive=$(kill -0 "$game" 2> /dev/null && echo yes || echo no)
[ "$alive" = yes ] && kill -9 "$game" 2> /dev/null
check "SIGTERM to the launcher stops the game" "$([ "$alive" = no ] && [ -f "$d/stopped" ] && [ "$(starts "$d")" = 1 ] && [ "$rc" = 143 ] && echo 1)" "launcher status $rc, game still running: $alive, $(starts "$d") starts"

# no Wayland session: no -force-wayland
out=$(env -u WAYLAND_DISPLAY PW_DRY_RUN=1 PW_GAME=/x/game "$LAUNCHER" -a)
check "without Wayland it adds no flag" "$([ "$out" = "/x/game -a" ] && echo 1)" "$out"

echo "[launcher] done: $passes passed, $fails failed"
[ "$fails" = 0 ]
