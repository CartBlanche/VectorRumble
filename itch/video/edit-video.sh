#!/usr/bin/env bash
# Cuts the YouTube draft from an OBS recording made by itch/video/record-video.sh --obs.
#
#   itch/video/edit-video.sh <recording> [--from S] [--to S] [--out FILE] [--fade S]
#
#   <recording>   the file OBS wrote (also on the "recording_file=" line of video/timing.txt)
#   --from S      first second to keep (default 0). The game starts a few seconds after OBS: look at the first frames
#                 and set this to just before the main menu appears. "--from match" keeps from 2 s before the battle starts.
#   --to S        last second to keep (default: the end)
#   --out FILE    default video/vector-rumble.mp4
#   --fade S      fade in and out of this length (default 0.6)
#
# 1080p H.264 with AAC sound, ready for YouTube and itch.io. Needs ffmpeg.
set -euo pipefail
IN="${1:-}"
if [[ -z "$IN" || "$IN" == --* ]]; then sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 1; fi
shift
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
FROM=0; TO=""; OUT="$ROOT/video/vector-rumble.mp4"; FADE=0.6
while [[ $# -gt 0 ]]; do
    case "$1" in
        --from) FROM="$2"; shift 2 ;;
        --to) TO="$2"; shift 2 ;;
        --out) OUT="$2"; shift 2 ;;
        --fade) FADE="$2"; shift 2 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

if [[ "$FROM" == "match" ]]; then
    TIMING="$ROOT/video/timing.txt"
    REC="$(grep recording_started_unix "$TIMING" | tail -1 | cut -d= -f2)"
    MS="$(grep match-start "$TIMING" | tail -1 | sed 's/.*unix-ms //')"
    FROM="$(python3 -I -c "print(max(0, $MS / 1000.0 - $REC - 2))")"
    echo "The battle starts at about ${FROM}s (2 s of lead-in kept)"
fi

LENGTH="$(ffprobe -v error -show_entries format=duration -of csv=p=0 "$IN")"
END="${TO:-$LENGTH}"
DURATION="$(python3 -I -c "print($END - $FROM)")"
FADEOUT="$(python3 -I -c "print(max(0, $DURATION - $FADE))")"
ffmpeg -v error -y -ss "$FROM" -to "$END" -i "$IN" \
    -vf "scale=1920:1080:flags=lanczos,fade=t=in:st=0:d=$FADE,fade=t=out:st=$FADEOUT:d=$FADE,format=yuv420p" \
    -af "afade=t=in:st=0:d=$FADE,afade=t=out:st=$FADEOUT:d=$FADE" \
    -c:v libx264 -crf 18 -preset slow -c:a aac -b:a 192k -movflags +faststart "$OUT"
echo "Wrote $OUT ($DURATION s)"
