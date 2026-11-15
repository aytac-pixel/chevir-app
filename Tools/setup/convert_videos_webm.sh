#!/usr/bin/env bash
# Converts the language-pack MP4 clips to WebM (VP8, silent) in place, for builds on the Linux editor,
# which cannot import MP4. Used by CI only; the repository keeps the MP4 originals.
# Usage: bash Tools/setup/convert_videos_webm.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
command -v ffmpeg >/dev/null || { echo "ffmpeg is required" >&2; exit 1; }

convert() {
  src="$1"
  dst="${src%.*}.webm"
  ffmpeg -nostdin -loglevel error -y -i "$src" -an -c:v libvpx -b:v 1200k -crf 10 \
    -deadline good -cpu-used 4 -pix_fmt yuv420p "$dst"
  rm -f "$src" "$src.meta"
}
export -f convert

count="$(find "$ROOT/Assets/LanguagePacks" -type f -iname '*.mp4' | wc -l)"
echo "Converting ${count} videos to WebM..."
find "$ROOT/Assets/LanguagePacks" -type f -iname '*.mp4' -print0 | xargs -0 -P "$(nproc)" -I{} bash -c 'convert "$1"' _ {}
echo "Done."
