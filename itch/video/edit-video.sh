#!/usr/bin/env bash
# Makes the finished video from an OBS recording with the generic game-trailer-recorder toolkit.
#
#   itch/video/edit-video.sh <recording> [--kill-at S] [--work DIR] [--out FILE] [--card TEXT | --no-card] [--no-cut]
#
# Pass the same --work DIR that record-video.sh used (default video). Without --kill-at it lists the loudest explosions to pick from.
# The toolkit is found in $TRAILER_SKILL, or in ../game-trailer-recorder next to this repo.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SKILL="${TRAILER_SKILL:-$ROOT/../game-trailer-recorder}"
[[ -f "$SKILL/scripts/edit.py" ]] || { echo "game-trailer-recorder not found at $SKILL (set TRAILER_SKILL)" >&2; exit 1; }
cd "$ROOT"
exec python3 -I "$SKILL/scripts/edit.py" itch/video/trailer.json "$@"
