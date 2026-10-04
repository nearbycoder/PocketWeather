#!/usr/bin/env bash
# Runs the Unity 6000.6.2f1 editor against this project.
#
# The editor links against libxml2.so.2, but CachyOS/Arch ship libxml2.so.16, so the editor
# exits at once with "libxml2.so.2: cannot open shared object file". The proper fix is
# `sudo pacman -S libxml2-legacy`; until then we point the loader at a local copy in Tools/libs
# (falls back to ~/.local/share/ptt-unity-libs, where another project on this machine keeps one).
#
#   Tools/unity.sh                      open the project in the editor (GUI)
#   Tools/unity.sh build                batch-build Builds/Linux/PocketWeather.x86_64
#   Tools/unity.sh webgl                batch-build Builds/WebGL (serve with Tools/serve_web.sh)
#   Tools/unity.sh method <Name>        batch-run a static editor method (e.g. PocketWeather.EditorTools.ProjectSetup.Apply)
#   Tools/unity.sh compile              batch import + compile only, print compiler errors
#   Tools/unity.sh serve                resident batch editor (no -quit) for `unity command`
set -euo pipefail

UNITY="${UNITY:-$HOME/Unity/Hub/Editor/6000.6.2f1/Editor/Unity}"
PROJECT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIBS="$PROJECT/Tools/libs"
[ -e "$LIBS/libxml2.so.2" ] || LIBS="$HOME/.local/share/ptt-unity-libs"
export LD_LIBRARY_PATH="$LIBS${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
LOGDIR="$PROJECT/Logs"; mkdir -p "$LOGDIR"

batch() {
  local log="$1"; shift
  set +e
  "$UNITY" -batchmode -nographics -quit -projectPath "$PROJECT" -logFile "$log" "$@"
  local rc=$?
  set -e
  grep -E "error CS|Compilation failed|Scripts have compiler errors|\[PW\]|Exception|BuildResult|Build (succeeded|failed)" "$log" | grep -v "^Rebuilding" | head -80 || true
  return $rc
}

case "${1:-open}" in
  open)
    exec "$UNITY" -projectPath "$PROJECT"
    ;;
  build)
    batch "$LOGDIR/build.log" -buildTarget Linux64 -executeMethod PocketWeather.EditorTools.BuildScript.BuildLinux
    ;;
  webgl)
    batch "$LOGDIR/build-webgl.log" -buildTarget WebGL -executeMethod PocketWeather.EditorTools.BuildScript.BuildWebGL
    ;;
  method)
    batch "$LOGDIR/method.log" -executeMethod "$2"
    ;;
  compile)
    batch "$LOGDIR/compile.log"
    ;;
  serve)
    exec "$UNITY" -batchmode -projectPath "$PROJECT" -logFile "$LOGDIR/serve.log"
    ;;
  *)
    echo "usage: $0 [open|build|webgl|method <Name>|compile|serve]" >&2
    exit 2
    ;;
esac
