#!/usr/bin/env bash
set -euo pipefail

UNITY_BIN_DEFAULT="/home/yngvar/Unity/Hub/Editor/6000.3.10f1/Editor/Unity"
UNITY_BIN="${UNITY_BIN:-$UNITY_BIN_DEFAULT}"
PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
MODE="${1:-dev}"

case "$MODE" in
  dev)
    METHOD="WebGLBuildRunner.BuildDevelopmentWebGL"
    LOG_FILE="$PROJECT_PATH/Logs/WebGL_DevBuild.log"
    ;;
  rc)
    METHOD="WebGLBuildRunner.BuildReleaseCandidateWebGL"
    LOG_FILE="$PROJECT_PATH/Logs/WebGL_RCBuild.log"
    ;;
  *)
    echo "Usage: $0 [dev|rc]"
    exit 1
    ;;
esac

if [[ ! -x "$UNITY_BIN" ]]; then
  echo "Unity binary not found or not executable: $UNITY_BIN"
  echo "Set UNITY_BIN env var, for example:"
  echo "UNITY_BIN=/path/to/Unity $0 $MODE"
  exit 1
fi

mkdir -p "$PROJECT_PATH/Logs"

echo "Running $MODE build with method: $METHOD"
"$UNITY_BIN" \
  -batchmode \
  -quit \
  -projectPath "$PROJECT_PATH" \
  -executeMethod "$METHOD" \
  -logFile "$LOG_FILE"

echo "Build finished. Log: $LOG_FILE"
