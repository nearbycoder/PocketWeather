#!/bin/sh
# Starts Pocket Weather. On a Wayland desktop it asks Unity for its native Wayland window backend:
# Unity's default X11 one, through XWayland, can hang before the window appears (it does on KDE
# Plasma under Wayland). Arguments are passed on to the game.
#   PW_X11=1 ./PocketWeather.sh      use Unity's default (X11) backend anyway
#   PW_DRY_RUN=1 ./PocketWeather.sh  print the command instead of running it
DIR=$(dirname "$(readlink -f "$0")")
if [ -n "${WAYLAND_DISPLAY:-}" ] && [ "${PW_X11:-0}" != 1 ]; then set -- -force-wayland "$@"; fi
if [ "${PW_DRY_RUN:-0}" = 1 ]; then echo "$DIR/PocketWeather.x86_64" "$@"; exit 0; fi
exec "$DIR/PocketWeather.x86_64" "$@"
