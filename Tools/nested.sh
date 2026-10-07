#!/usr/bin/env bash
# Runs a command with its windows in a private KWin on a virtual screen: its own Wayland socket and
# its own D-Bus session, drawn on the real GPU but shown nowhere. Test and capture windows then
# can't reach the desktop (or go fullscreen on it), however they're started. The KWin is stopped
# when the command ends.
#
#   Tools/nested.sh Tools/selftest.sh
#   Tools/nested.sh Tools/play.sh -pwCapture "$PWD/Recordings/tour"
#   PW_NESTED_SIZE=2600x1500 Tools/nested.sh ...     the virtual screen's size (default 2600x1500,
#                                                    room for the largest window the tools open)
#
# Tools/play.sh (with any -pw automation flag) and Tools/selftest.sh use it by themselves when
# kwin_wayland is installed; PW_NESTED=0 makes them open ordinary windows instead.
set -euo pipefail
[ $# -gt 0 ] || { echo "usage: $0 <command> [args...]" >&2; exit 2; }
command -v kwin_wayland > /dev/null || { echo "nested.sh: kwin_wayland isn't installed" >&2; exit 3; }
command -v dbus-run-session > /dev/null || { echo "nested.sh: dbus-run-session isn't installed" >&2; exit 3; }
: "${XDG_RUNTIME_DIR:?nested.sh needs XDG_RUNTIME_DIR}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
size="${PW_NESTED_SIZE:-2600x1500}"
socket="pw-nested-$$"
work="$ROOT/Recordings/nested-$$"   # gitignored; not the shared /tmp
mkdir -p "$work"
pidfile="$work/kwin.pid"

# dbus-run-session gives the KWin a bus of its own, so it never meets the desktop's KWin on the
# session bus; the inner shell records the KWin's own PID, which is what gets stopped
dbus-run-session -- sh -c 'echo $$ > "$1"; shift; exec "$@"' sh "$pidfile" \
  kwin_wayland --virtual --no-lockscreen --socket "$socket" --width "${size%x*}" --height "${size#*x}" \
  > "$work/kwin.log" 2>&1 &
runner=$!

stop() {
  local kwin; kwin=$(cat "$pidfile" 2>/dev/null || true)
  if [ -n "$kwin" ] && kill -0 "$kwin" 2> /dev/null; then
    kill "$kwin" 2> /dev/null || true
    for _ in $(seq 50); do kill -0 "$kwin" 2> /dev/null || break; sleep 0.1; done
    kill -9 "$kwin" 2> /dev/null || true
  fi
  wait "$runner" 2> /dev/null || true
  rm -f "$XDG_RUNTIME_DIR/$socket" "$XDG_RUNTIME_DIR/$socket.lock"
  rm -rf "$work"
}
trap stop EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

for _ in $(seq 100); do [ -S "$XDG_RUNTIME_DIR/$socket" ] && break; sleep 0.1; done
if [ ! -S "$XDG_RUNTIME_DIR/$socket" ]; then
  echo "nested.sh: the private KWin didn't start:" >&2; tail -5 "$work/kwin.log" >&2; exit 3
fi
echo "nested.sh: private KWin on $socket (${size}, pid $(cat "$pidfile"))" >&2
# no DISPLAY: nothing can fall back to the desktop's X server. PW_IN_NESTED tells play.sh and
# selftest.sh they're already inside one.
set +e
env -u DISPLAY WAYLAND_DISPLAY="$socket" PW_IN_NESTED="$socket" "$@"
rc=$?
exit $rc
