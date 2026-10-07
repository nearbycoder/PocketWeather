#!/bin/sh
# Starts Pocket Weather. On a Wayland desktop it asks Unity for its native Wayland window backend:
# Unity's default X11 one, through XWayland, can hang before the window appears (it does on KDE
# Plasma under Wayland). Arguments are passed on to the game.
#
# Unity's Wayland backend also crashes now and then while the window is being made (about one
# launch in 45 on the machine it was built on); starting again works. So if the game dies of a
# signal within 20 s of starting, it's started once more. Any other exit, or a crash later on, ends
# this script with the game's own status.
#
#   PW_X11=1 ./PocketWeather.sh      use Unity's default (X11) backend anyway
#   PW_DRY_RUN=1 ./PocketWeather.sh  print the command instead of running it
DIR=$(dirname "$(readlink -f "$0")")
GAME="${PW_GAME:-$DIR/PocketWeather.x86_64}"   # PW_GAME and PW_RETRY_WINDOW are for Tools/linux/test_launcher.sh
WINDOW="${PW_RETRY_WINDOW:-20}"
if [ -n "${WAYLAND_DISPLAY:-}" ] && [ "${PW_X11:-0}" != 1 ]; then set -- -force-wayland "$@"; fi
if [ "${PW_DRY_RUN:-0}" = 1 ]; then echo "$GAME" "$@"; exit 0; fi

# the game runs as a child, so a request to stop this script (a desktop, Steam, a terminal's
# Ctrl+C) is passed on to it. SIGINT goes on as SIGTERM: a background child in a non-interactive
# shell starts with SIGINT ignored.
child=""
stopping=""
stop() { stopping=1; [ -n "$child" ] && kill -TERM "$child" 2> /dev/null; }
trap stop TERM INT HUP

tries=0
while :; do
  started=$(date +%s)
  "$GAME" "$@" &
  child=$!
  # a signal caught by this script interrupts wait; keep waiting until the game itself has gone
  while :; do
    wait "$child"
    rc=$?
    kill -0 "$child" 2> /dev/null || break
  done
  child=""
  tries=$((tries + 1))
  if [ -z "$stopping" ] && [ "$tries" -lt 2 ] && [ "$rc" -gt 128 ] && [ $(($(date +%s) - started)) -lt "$WINDOW" ]; then
    echo "PocketWeather.sh: the game stopped with status $rc while starting; starting it again" >&2
    continue
  fi
  exit "$rc"
done
