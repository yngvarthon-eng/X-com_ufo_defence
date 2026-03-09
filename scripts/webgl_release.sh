#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RC_OUTPUT_DIR="$PROJECT_PATH/Builds/WebGL/rc"
RELEASE_ROOT_DIR="$PROJECT_PATH/Builds/WebGL/releases"

RELEASE_TAG="${1:-}"

if [[ -z "$RELEASE_TAG" ]]; then
  echo "Usage: $0 <release-tag>"
  echo "Example: $0 v0.1.0-rc1"
  exit 1
fi

if [[ "$RELEASE_TAG" =~ [[:space:]] ]]; then
  echo "Release tag must not contain spaces: $RELEASE_TAG"
  exit 1
fi

TARGET_DIR="$RELEASE_ROOT_DIR/$RELEASE_TAG"

"$PROJECT_PATH/scripts/webgl_build.sh" rc

if [[ ! -d "$RC_OUTPUT_DIR" ]]; then
  echo "RC output directory missing after build: $RC_OUTPUT_DIR"
  exit 1
fi

mkdir -p "$RELEASE_ROOT_DIR"
rm -rf "$TARGET_DIR"
mkdir -p "$TARGET_DIR"

# Mirror RC artifacts into a versioned release folder.
cp -a "$RC_OUTPUT_DIR/." "$TARGET_DIR/"

echo "Release artifact prepared: $TARGET_DIR"
