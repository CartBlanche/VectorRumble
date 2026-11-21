#!/bin/zsh
# Builds itch/menu-to-dogfight-640x360.gif: the main menu, a hard cut to a four-ship dogfight, then a ship explodes in quarter-speed slow motion; it fades in and out so it loops softly.
# Needs ffmpeg and gifsicle. caffeinate keeps the display awake: a game cannot draw while the Mac's screen is asleep or locked. Run from the repo root after building with:  dotnet build Desktop -c Release -p:DevShots=true
# VR_LOCK=1 makes the game run one 60 Hz tick per drawn frame, so the pictures have none missing.
G=${1:-/tmp/vr-gif}; mkdir -p $G; cd "$(dirname "$0")/../.."
run(){ ( caffeinate -d -i env "$@" dotnet run --project Desktop -c Release --no-build >/dev/null 2>&1 & p=$!; for i in $(seq 1 1500); do sleep 1; kill -0 $p 2>/dev/null || break; done; kill $p 2>/dev/null ) }
# 1) the main menu with its fireworks, 30 fps, 4 s
run VR_LOCK=1 VR_BURST=7,120,0.033333 VR_BURST_DIR=$G/menu
# 2) a four-AI dogfight, 60 fps, 40 s. The same seed gives the same fight if the game is unchanged; look for the explosion with a contact sheet.
run VR_LOCK=1 VR_OPEN=pick VR_AUTOSTART=1 VR_AI=3 VR_ALLAI=1 VR_SEED=11 VR_PICK=0,2,2 VR_BURST=14,2400,0.0166667 VR_BURST_DIR=$G/fight
# 3) the sequence: 2 s of menu (every 2nd frame of 30 fps), 2.5 s of fight (every 4th of 60 fps), then 48 frames of the explosion at 4x slow motion
rm -rf $G/seq; mkdir $G/seq; n=0; link(){ n=$((n+1)); ln "$1" $G/seq/$(printf '%04d.png' $n); }
for i in $(seq 0 2 59); do link $G/menu/$(printf 'frame%03d.png' $i); done
for i in $(seq 2064 4 2213); do link $G/fight/$(printf 'frame%03d.png' $i); done
for i in $(seq 2214 1 2261); do link $G/fight/$(printf 'frame%03d.png' $i); done
ffmpeg -v error -y -framerate 15 -i $G/seq/%04d.png -vf "fade=t=in:st=0:d=0.2,fade=t=out:st=7.33:d=0.4,scale=640:-1:flags=lanczos,split[a][b];[a]palettegen=max_colors=128:stats_mode=diff[p];[b][p]paletteuse=dither=none:diff_mode=rectangle" -loop 0 $G/raw.gif
gifsicle -O3 --lossy=30 $G/raw.gif -o itch/menu-to-dogfight-640x360.gif
ls -la itch/menu-to-dogfight-640x360.gif
