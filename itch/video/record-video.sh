#!/usr/bin/env bash
# Records the Vector Rumble video: a pretend LAN match between four players, cut between their screens.
#
#   John, Jane and Alison start first and wait on the Multiplayer screen. Then Dominique's copy starts, the recording starts,
#   and Dominique walks: Play, Multiplayer, Host a game, and waits in the lobby.
#   Cut to John, whose Multiplayer screen shows Dominique's game; he selects it and joins.
#   Quick cuts to Jane and Alison doing the same.
#   Back to Dominique's lobby with all four ships (Destroyer 3 thrusters, Defender 2, Blaster 1, Bat-ter Up 1).
#   The rest is Dominique's window: the battle, then the victory screen.
#
# It is a real network match: four copies of the game on this Mac as four windows, one scene each in OBS
# (a window capture of that copy), and a director (itch/video/director.py) that cuts between the scenes from the games'
# own cue lines. The computer flies every ship (VR_ALLAI) and two of the pilots hop now and then (VR_DEMOHOP).
#
#   itch/video/record-video.sh [--obs] [--no-build] [--seed N] [--arena N] [--sky N] [--kills N]
#
#   --obs          drive OBS: make the four scenes, record, cut, and stop when the game quits. Needs the OBS_PASSWORD
#                  environment variable (OBS WebSocket password; never stored here) and optionally OBS_HOST / OBS_PORT
#                  (default localhost:4455). Without it the four games just play the script (to check it).
#   --no-build     skip building the game (it is built with the DevShots switch: dotnet build Desktop -c Release -p:DevShots=true)
#   --seed N       the AI pilots' seed (default 3); the same seed plays the same match if the game is unchanged
#   --arena N      the arena, 0 to 3 in the pick screen's list (default 2, the one with two bars, which keeps the pilots calmer than the H)
#   --sky N        the sky behind the arena, 0 to 4 (default 0)
#   --kills N      kills to win, 5 to 25 (default 5): the match, and so the video, ends when somebody gets there
#
# Keep all four windows fully visible: a window that is covered or minimised is throttled and its ship stalls.
# Run it from a Terminal, with the display awake (this script uses caffeinate). The games quit by themselves.
# Files go in video/ (git-ignored): a log per player (with the cue lines), timing.txt and, with --obs, OBS writes the recording
# to its own folder (the script prints the path and checks it with ffprobe).
set -euo pipefail

BUILD=1; OBS=0; SEED=3; ARENA=2; SKY=0; KILLS=5; CAP=420
while [[ $# -gt 0 ]]; do
    case "$1" in
        --obs) OBS=1; shift ;;
        --no-build) BUILD=0; shift ;;
        --seed) SEED="$2"; shift 2 ;;
        --arena) ARENA="$2"; shift 2 ;;
        --sky) SKY="$2"; shift 2 ;;
        --kills) KILLS="$2"; shift 2 ;;
        -h|--help) sed -n '2,27p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
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

PIDS=(); RECORDING=0; WATCHDOG=""; DIRECTOR=""
stop_recording() {
    if [[ $RECORDING -eq 1 ]]; then
        sleep 2
        FILE="$(obs stop || true)"
        echo "Recording: $FILE"
        echo "recording_file=$FILE" >> "$OUT/timing.txt"
        RECORDING=0
        if [[ -n "$FILE" ]]; then
            # OBS finishes writing the file a moment after "stop": try for up to 30 s
            for _ in $(seq 1 15); do
                if ffprobe -v error -show_entries stream=codec_type,codec_name,width,height -show_entries format=duration -of compact "$FILE" 2>/dev/null; then
                    break
                fi
                sleep 2
            done
        fi
    fi
}
cleanup() {
    [[ -n "$WATCHDOG" ]] && kill "$WATCHDOG" 2>/dev/null || true
    [[ -n "$DIRECTOR" ]] && kill "$DIRECTOR" 2>/dev/null || true
    stop_recording
    for p in "${PIDS[@]:-}"; do kill "$p" 2>/dev/null || true; done
}
trap cleanup EXIT INT TERM

if pkill -f "[V]ectorRumble.dll" 2>/dev/null; then echo "Stopped a leftover game window" >&2; sleep 2; fi

if [[ $OBS -eq 1 ]]; then
    # fail early if OBS cannot be reached, and never touch a recording somebody else started
    if obs status | grep -q "^recording"; then
        echo "OBS is already recording: stop that first (this script only stops a recording it started)" >&2
        exit 1
    fi
    # the mic stays out of the recording
    obs call SetInputMute '{"inputName":"Mic/Aux","inputMuted":true}' >/dev/null 2>&1 || true
fi

# fresh signals for the director: the joining copies wait for their go file, the host for the start file
rm -f "$OUT/go-John" "$OUT/go-Jane" "$OUT/go-Alison" "$OUT/start" "$OUT/tour"
for n in Dominique John Jane Alison; do : > "$OUT/$n.log"; done
echo "script=pretend LAN match" > "$OUT/timing.txt"

# What every copy shares. VR_CLEANSETTINGS: the default sky layers and volumes, whatever this Mac's settings file says.
COMMON=(VR_CLEANSETTINGS=1 VR_ALLAI=1 VR_AI=3 VR_SEED="$SEED" VR_SKY="$SKY" VR_FIREWORKS=1 VR_SCORELIMIT="$KILLS")

start() {   # name, window, extra environment...
    local name="$1" win="$2"; shift 2
    env "${COMMON[@]}" VR_NAME="$name" VR_WIN="$win" "$@" \
        caffeinate -d -i dotnet "$DLL" >> "$OUT/$name.log" 2>&1 &
    PIDS+=($!)
    sleep 0.8       # so the windows have ascending ids: the host first
}

# Each window is 1280x720 points: the game draws at that size, so the picture is sharp (a small window means a small picture
# stretched up). Dominique's fills the screen; the other three sit below it with only their top edge on the screen (the rest is
# below the bottom of the screen), side by side, so each stays visible (a covered window is throttled) while OBS captures all
# of its picture. Ship and colour: 1 Destroyer (3 thrusters) cyan, 4 Defender (2) magenta, 0 Blaster (1) green, 2 Bat-ter Up (1) yellow
# 1) John, Jane and Alison start first and go to the Multiplayer screen, where they wait
start John "0,790,1280,720" VR_OPEN=browse VR_GOFILE="$OUT/go-John" VR_SHIP=4 VR_COLOUR=2 VR_DEMOHOP=1
start Jane "430,790,1280,720" VR_OPEN=browse VR_GOFILE="$OUT/go-Jane" VR_SHIP=0 VR_COLOUR=3
start Alison "860,790,1280,720" VR_OPEN=browse VR_GOFILE="$OUT/go-Alison" VR_SHIP=2 VR_COLOUR=4 VR_DEMOHOP=1
for n in John Jane Alison; do
    for _ in $(seq 1 120); do
        grep -q "cue browser-open" "$OUT/$n.log" && break
        sleep 0.5
    done
    grep -q "cue browser-open" "$OUT/$n.log" || { echo "$n never reached the Multiplayer screen" >&2; exit 1; }
done

# 2) Dominique starts, on the main menu, and waits for the signal that the recording is running
start Dominique "0,30,1280,720" VR_OPEN=main VR_PICK=",,$ARENA" VR_TOUR="Play,Multiplayer,Host a game" VR_TOURFILE="$OUT/tour" VR_TOURSTART=0.6 VR_RULESHOLD=1.2 \
    VR_STARTFILE="$OUT/start" VR_QUITAFTEROVER=9 VR_ENDAFTER="$CAP" VR_SHIP=1 VR_COLOUR=1 VR_SKYNAMES=1

# Watchdog: whatever goes wrong, the games (and so the recording) end after the cap plus the lobby time
MAX=$(( CAP + 90 ))
( sleep "$MAX"; echo "Took longer than ${MAX}s: stopping" >&2; pkill -f "[V]ectorRumble.dll" ) &
WATCHDOG=$!

sleep 4
# 3) the scenes (the windows are named in the order they were started), then the recording, then the signal that starts Dominique's walk
if [[ $OBS -eq 1 ]]; then
    obs bind-all John Jane Alison Dominique
    obs cut Dominique
    START="$(python3 -I -c 'import time; print("%.3f" % time.time())')"
    obs start
    RECORDING=1
    echo "recording_started_unix=$START" >> "$OUT/timing.txt"
else
    export DIRECTOR_NO_OBS=1
fi
sleep 1.5       # a moment of the main menu on screen first
: > "$OUT/tour"

python3 -I "$ROOT/itch/video/director.py" "$OUT" &
DIRECTOR=$!

# the host (the last copy started) quits 9 s after the game over screen (or at the cap); then stop everything
wait "${PIDS[3]}" || true
sleep 2
grep -h "match-start" "$OUT/Dominique.log" | tail -1 | tee -a "$OUT/timing.txt" || true
cleanup
trap - EXIT
echo "Done. Logs and timing are in $OUT"
