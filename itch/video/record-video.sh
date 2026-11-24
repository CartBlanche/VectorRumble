#!/usr/bin/env bash
# Records the Vector Rumble video with the generic game-trailer-recorder toolkit (a separate repo).
#
#   itch/video/record-video.sh [--obs] [--no-build] [--work DIR] [--cap S] [--env VR_SEED=5 ...]
#
# The story, the four players and the game's settings are in itch/video/trailer.json. The old options map to --env:
#   --seed N -> --env VR_SEED=N     --arena N -> --env VR_PICK=,,N (also edit the host's VR_PICK in trailer.json)
#   --sky N  -> --env VR_SKY=N      --kills N -> --env VR_SCORELIMIT=N
# --obs needs OBS_PASSWORD exported in your shell (never stored). Dry runs (no --obs) need no OBS.
# The toolkit is found in $TRAILER_SKILL, or in ../game-trailer-recorder next to this repo.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
SKILL="${TRAILER_SKILL:-$ROOT/../game-trailer-recorder}"
[[ -f "$SKILL/scripts/record.py" ]] || { echo "game-trailer-recorder not found at $SKILL (set TRAILER_SKILL)" >&2; exit 1; }
cd "$ROOT"
exec python3 -I "$SKILL/scripts/record.py" itch/video/trailer.json "$@"
