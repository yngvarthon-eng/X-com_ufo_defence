#!/usr/bin/env bash
# Serve a WebGL release folder locally the way Netlify will: gzip-compressed build files are sent
# with "Content-Encoding: gzip" (see scripts/netlify/_headers), so the browser unpacks them.
# Usage: scripts/webgl_serve_release.sh v0.1.0-rc1 [port]
set -euo pipefail

PROJECT_PATH="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RELEASE_TAG="${1:?Usage: $0 <release-tag> [port]}"
PORT="${2:-8090}"
ROOT="$PROJECT_PATH/Builds/WebGL/releases/$RELEASE_TAG"

if [[ ! -f "$ROOT/index.html" ]]; then
  echo "No release build at $ROOT"
  exit 1
fi

echo "Serving $ROOT at http://localhost:$PORT"
cd "$ROOT"
exec python3 -I - "$PORT" <<'PY'
import http.server, sys

TYPES = {".wasm": "application/wasm", ".js": "application/javascript", ".data": "application/octet-stream", ".json": "application/octet-stream"}

class Handler(http.server.SimpleHTTPRequestHandler):
    def guess_type(self, path):
        if path.endswith(".gz") or path.endswith(".br"):
            inner = path[:-3]
            for ext, kind in TYPES.items():
                if inner.endswith(ext):
                    return kind
        return super().guess_type(path)

    def end_headers(self):
        path = self.path.split("?", 1)[0]
        if path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        elif path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

http.server.ThreadingHTTPServer(("127.0.0.1", int(sys.argv[1])), Handler).serve_forever()
PY
