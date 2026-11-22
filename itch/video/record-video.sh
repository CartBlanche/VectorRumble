#!/usr/bin/env bash
# Records the Vector Rumble video: a pretend LAN match. The main menu, Play, Multiplayer, Host a game, then three more
# players join (John, Jane and Alison join Dominique, the host), they all get ready and a four ship dogfight begins.
# It is a real network match: four copies of the game on this Mac, the host's window big (this is what is recorded)
# and the other three small along the bottom of the screen. The computer flies every ship (VR_ALLAI), and two of the
# pilots hop now and then (VR_DEMOHOP), timed to clear a shot when one is coming.
#
#   itch/video/record-video.sh [--obs] [--no-build] [--seed N] [--arena N] [--sky N] [--seconds N]
#
#   --obs          also drive OBS: switch to the "Vector Rumble" scene, point its window source at the host's window,
#                  record, and stop when the game quits. Needs the OBS_PASSWORD environment variable (OBS WebSocket
#                  password; never stored here) and optionally OBS_HOST / OBS_PORT (default localhost:4455). See itch/video/obs.py.
#   --no-build     skip building the game (it is built with the DevShots switch: dotnet build Desktop -c Release -p:DevShots=true)
#   --seed N       the AI pilots' seed (default 3); the same seed plays the same match if the game is unchanged
#   --arena N      the arena, 0 to 3 in the pick screen's list (default 0, the H arena)
#   --sky N        the sky behind the arena, 0 to 4 (default 0)
#   --seconds N    how long the battle lasts before the game quits (default 150)
#
# Keep all four windows fully visible: a window that is covered or minimised is throttled and its ship stalls.
# Run it from a Terminal, not a subshell, with the display awake (this script uses caffeinate). The game quits by itself.
# Files go in video/ (git-ignored): host.log (with the unix time the match started), timing.txt and, with --obs, the recording.
set -euo pipefail

BUILD=1; OBS=0; SEED=3; ARENA=0; SKY=0; SECONDS_OF_BATTLE=150
while [[ $# -gt 0 ]]; do
    case "$1" in
        --obs) OBS=1; shift ;;
        --no-build) BUILD=0; shift ;;
        --seed) SEED="$2"; shift 2 ;;
        --arena) ARENA="$2"; shift 2 ;;
        --sky) SKY="$2"; shift 2 ;;
        --seconds) SECONDS_OF_BATTLE="$2"; shift 2 ;;
        -h|--help) sed -n '2,20p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
        *) echo "Unknown option: $1" >&2; exit 1 ;;
    esac
done

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
OUT="$ROOT/video"
mkdir -p "$OUT"
DLL="$ROOT/Desktop/bin/Release/net10.0/VectorRumble.dll"
obs() { python3 -I "$ROOT/itch/video/obs.py" "$@"; }

if [[ $BUILD -eq 1 ]]; then
    dotnet build "$ROOT/Desktop" -c Release -p:DevShots=true -v q --nologo
fi

if [[ $OBS -eq 1 && -z "${OBS_PASSWORD:-}" ]]; then
    echo "--obs needs the OBS_PASSWORD environment variable (OBS WebSocket password)" >&2
    exit 1
fi

PIDS=(); RECORDING=0
stop_recording() {
    if [[ $RECORDING -eq 1 ]]; then
        sleep 2
        FILE="$(obs stop || true)"
        echo "Recording: $FILE"
        echo "recording_file=$FILE" >> "$OUT/timing.txt"
        RECORDING=0
    fi
}
cleanup() {
    stop_recording
    for p in "${PIDS[@]:-}"; do kill "$p" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

if pkill -f "[V]ectorRumble.dll" 2>/dev/null; then echo "Stopped a leftover game window" >&2; sleep 2; fi

if [[ $OBS -eq 1 ]]; then
    obs status >/dev/null     # fail early if OBS cannot be reached
    obs scene "Vector Rumble"
fi

# What every copy shares. VR_CLEANSETTINGS: the default sky layers and volumes, whatever this Mac's settings file says.
COMMON=(VR_CLEANSETTINGS=1 VR_ALLAI=1 VR_AI=3 VR_SEED="$SEED" VR_SKY="$SKY" VR_FIREWORKS=1)
# The timeline, in seconds since each copy started. The host walks the menus (5 s on, about 4 s a step), rests on the
# rules for 3 s and opens the lobby at about 20 s. The others start later, so each is seen joining; the host starts the
# match at 44 s, when the last of them is ready.
HOST_GO=44
ENDS=$(( SECONDS_OF_BATTLE ))

start() {   # name, delay, window, extra environment...
    local name="$1" delay="$2" win="$3"; shift 3
    sleep "$delay"
    env "${COMMON[@]}" VR_NAME="$name" VR_WIN="$win" "$@" \
        caffeinate -d -i dotnet "$DLL" >> "$OUT/$name.log" 2>&1 &
    PIDS+=($!)
}
: > "$OUT/Dominique.log"; : > "$OUT/John.log"; : > "$OUT/Jane.log"; : > "$OUT/Alison.log"

# the host: the recorded window
start Dominique 0 "0,30,1024,576" VR_OPEN=main VR_TOUR="Play,Multiplayer,Host a game" VR_TOURSTART=5 VR_RULESHOLD=3 VR_NETGO="$HOST_GO" VR_ENDAFTER="$ENDS" VR_DEMOHOP=1
if [[ $OBS -eq 1 ]]; then
    sleep 4
    obs bind
    START="$(python3 -I -c 'import time; print("%.3f" % time.time())')"
    obs start
    RECORDING=1
    echo "recording_started_unix=$START" > "$OUT/timing.txt"
else
    sleep 4
fi

# the others, small, along the bottom; each looks for the game when it starts and joins at about 13 s, then gets ready at 16 s
start John   8 "0,640,320,180"   VR_OPEN=browse VR_NETGO=16 VR_DEMOHOP=1
start Jane   6 "340,640,320,180"  VR_OPEN=browse VR_NETGO=16
start Alison 6 "680,640,320,180"  VR_OPEN=browse VR_NETGO=16 VR_DEMOHOP=1

# wait for the host to finish (it quits ENDS seconds after the match starts), then stop the rest
wait "${PIDS[0]}" || true
sleep 2
cleanup
grep -h "match-start" "$OUT/Dominique.log" | tail -1 | tee -a "$OUT/timing.txt" || true
echo "Done. Logs and timing are in $OUT"
