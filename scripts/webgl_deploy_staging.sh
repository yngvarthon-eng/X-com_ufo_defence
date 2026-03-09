#!/usr/bin/env bash
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RELEASE_ROOT_DIR="$PROJECT_PATH/Builds/WebGL/releases"

RELEASE_TAG="${1:-}"
STAGING_TARGET="${2:-}"

if [[ -z "$RELEASE_TAG" || -z "$STAGING_TARGET" ]]; then
  echo "Usage: $0 <release-tag> <user@host:/path/>"
  echo "Example: $0 v0.1.0-rc1 deploy@staging.example.com:/var/www/xcon/"
  exit 1
fi

SOURCE_DIR="$RELEASE_ROOT_DIR/$RELEASE_TAG"

if [[ ! -d "$SOURCE_DIR" ]]; then
  echo "Release folder not found: $SOURCE_DIR"
  echo "Run scripts/webgl_release.sh $RELEASE_TAG first."
  exit 1
fi

echo "Deploying $SOURCE_DIR to $STAGING_TARGET"
rsync -av --delete "$SOURCE_DIR/" "$STAGING_TARGET"
echo "Deploy complete."
