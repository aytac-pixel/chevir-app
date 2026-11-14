#!/usr/bin/env bash
# Downloads the prebuilt MediaPipe Unity Plugin package (homuler/MediaPipeUnityPlugin) into Packages/
# and registers it in Packages/manifest.json. Usage: bash Tools/setup/install_mediapipe_plugin.sh [tag]
# Without a tag the latest release is used. Set GH_TOKEN to avoid GitHub API rate limits (CI does).
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
REPO="homuler/MediaPipeUnityPlugin"
TAG="${1:-latest}"
API="https://api.github.com/repos/${REPO}/releases/${TAG}"
[ "$TAG" != "latest" ] && API="https://api.github.com/repos/${REPO}/releases/tags/${TAG}"

AUTH=()
[ -n "${GH_TOKEN:-}" ] && AUTH=(-H "Authorization: Bearer ${GH_TOKEN}")

echo "Looking up ${REPO} release '${TAG}'..."
URL="$(curl -fsSL ${AUTH[@]+"${AUTH[@]}"} "$API" | python3 -c '
import json, re, sys
release = json.load(sys.stdin)
for asset in release.get("assets", []):
    if re.match(r"com\.github\.homuler\.mediapipe-.*\.tgz$", asset["name"]):
        print(asset["browser_download_url"]); break
')"

if [ -z "$URL" ]; then
  echo "No com.github.homuler.mediapipe-*.tgz asset in that release. Download it manually from" >&2
  echo "https://github.com/${REPO}/releases and put it in Packages/." >&2
  exit 1
fi

FILE="$(basename "$URL")"
echo "Downloading ${FILE}..."
curl -fL --retry 3 -o "${ROOT}/Packages/${FILE}" "$URL"

python3 - "$ROOT/Packages/manifest.json" "$FILE" <<'PY'
import json, sys
path, file = sys.argv[1], sys.argv[2]
with open(path, encoding="utf-8") as f:
    manifest = json.load(f)
manifest["dependencies"]["com.github.homuler.mediapipe"] = "file:" + file
with open(path, "w", encoding="utf-8") as f:
    json.dump(manifest, f, indent=2)
    f.write("\n")
PY
echo "Done: Packages/manifest.json now references ${FILE}."
