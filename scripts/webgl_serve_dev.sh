#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WEBGL_DEV_DIR="$PROJECT_PATH/Builds/WebGL/dev"
PORT="${1:-8080}"

if [[ ! -d "$WEBGL_DEV_DIR" ]]; then
  echo "Dev WebGL build output not found: $WEBGL_DEV_DIR"
  echo "Run scripts/webgl_build.sh dev first."
  exit 1
fi

cd "$WEBGL_DEV_DIR"

echo "Serving $WEBGL_DEV_DIR on http://localhost:$PORT"
python3 -m http.server "$PORT"
