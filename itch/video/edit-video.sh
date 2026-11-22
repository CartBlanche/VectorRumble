#!/usr/bin/env bash
# Makes the YouTube video from the OBS recording that itch/video/record-video.sh --obs made.
#
#   itch/video/edit-video.sh <recording> [--kill-at S] [--out FILE] [--card TEXT | --no-card] [--no-cut]
#
#   <recording>   the file OBS wrote (it is also on the "recording_file=" line of video/timing.txt)
#   --kill-at S   the second of the recording where a ship explodes (the "kill"): the video opens with a 3.3 s clip of the
#                 build-up to it (2.55 s before, 0.75 s after), as a teaser. Without it there is no teaser, and the loudest
#                 explosions after the battle starts are listed so that you can pick one (look at the frames around it).
#   --card TEXT   the title card shown for 1 s before the teaser (default "COMING UP...", drawn in headless Chrome with the
#                 game's own font; set CHROME to a Chrome binary if it is not in /Applications); --no-card leaves it out
#   --no-cut      keep the wait between the last player getting ready and the host pressing Start
#   --out FILE    default: "<the recording's name> final.mp4" next to the recording
#
# The recipe, in order:
#   1. COMING UP... for 1 s (fades in and out, silent)
#   2. the kill teaser (fades in, dips to black at the end, its sound fades out)
#   3. the recording itself, with the wait cut out: from 0.85 s after the last player is ready to 0.05 s before the host
#      presses Start (which keeps the press sound and stops just before the Loading screen); the sound is joined with an 80 ms
#      crossfade. The times come from the cue lines in video/<Name>.log and video/timing.txt.
# Everything is made in one pass: 1080p, 60 fps, H.264 (crf 18) and AAC. Needs ffmpeg, ffprobe and python3 (Chrome for the card).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
exec python3 -I - "$ROOT" "$@" <<'PY'
import os, re, subprocess, sys, tempfile, array, math

root, args = sys.argv[1], sys.argv[2:]
rec_file = None; kill_at = None; out = None; card = "COMING UP..."; cut = True
it = iter(args)
for a in it:
    if a == "--kill-at": kill_at = float(next(it))
    elif a == "--out": out = next(it)
    elif a == "--card": card = next(it)
    elif a == "--no-card": card = None
    elif a == "--no-cut": cut = False
    elif a.startswith("--"): sys.exit("Unknown option: " + a)
    else: rec_file = a
if not rec_file:
    header = open(os.path.join(root, "itch/video/edit-video.sh")).read().split("set -euo")[0].splitlines()[1:]
    print("\n".join(re.sub(r"^# ?", "", line) for line in header))
    sys.exit(1)
video = os.path.join(root, "video")
out = out or re.sub(r"\.[^.]+$", "", rec_file) + " final.mp4"

def probe(*a):
    return subprocess.check_output(a).decode().strip()

# the cue times, as seconds into the recording
timing = open(os.path.join(video, "timing.txt")).read()
rec_start = float(re.search(r"recording_started_unix=([\d.]+)", timing).group(1))
def cues(name):
    path = os.path.join(video, name + ".log")
    found = {}
    if os.path.exists(path):
        for line in open(path, errors="replace"):
            m = re.search(r"cue (\S+) unix-ms (\d+)", line)
            if m: found.setdefault(m.group(1), []).append(int(m.group(2)) / 1000.0 - rec_start)
    return found
host = cues("Dominique")
if "match-start" not in host: sys.exit("No match-start cue in video/Dominique.log: was this the recording that video/ describes?")
match_start = host["match-start"][0]
ready = [t for n in ("John", "Jane", "Alison") for t in cues(n).get("ready", [])]
duration = float(probe("ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", rec_file))

if kill_at is None:
    # help to choose one: the loudest sounds after the battle starts
    pcm = subprocess.check_output(["ffmpeg", "-v", "error", "-i", rec_file, "-vn", "-ac", "1", "-ar", "8000", "-f", "s16le", "-"])
    a = array.array("h"); a.frombytes(pcm)
    win = 400
    env = [(i * 0.05, math.sqrt(sum(x * x for x in a[i * win:(i + 1) * win]) / win)) for i in range(len(a) // win)]
    loud = sorted((e for e in env if e[0] > match_start + 2), key=lambda e: -e[1])
    picked = []
    for t, v in loud:
        if all(abs(t - p) > 2 for p, _ in picked): picked.append((t, v))
        if len(picked) == 8: break
    print("No --kill-at, so no teaser. The loudest sounds after the battle starts (seconds, level):")
    for t, v in sorted(picked): print("  %6.2f s  %d" % (t, v))
    print("Look at the frames around one (ffmpeg -ss T -i FILE -frames:v 1 x.png): a ship exploding with rings spreading, with a chase before it.")

workdir = tempfile.mkdtemp(prefix="vr-edit-")

if card:
    chrome = os.environ.get("CHROME", "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome")
    if not os.path.exists(chrome):
        sys.exit("Chrome not found for the title card (set CHROME, or use --no-card)")
    html = open(os.path.join(root, "itch/video/card.html")).read()
    font = "file://" + os.path.join(root, "Content/Fonts/Orbitron.ttf")
    open(os.path.join(workdir, "card.html"), "w").write(html.replace("{{FONT}}", font).replace("{{TEXT}}", card))
    subprocess.run([chrome, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--force-device-scale-factor=1", "--window-size=1920,1080",
                    "--virtual-time-budget=2000", "--screenshot=" + os.path.join(workdir, "card.png"), "file://" + os.path.join(workdir, "card.html")],
                   check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

cmd = ["ffmpeg", "-v", "error", "-y"]
idx = 0
chain = []
def inp(*a):
    global idx
    cmd.extend(a); idx += 1; return idx - 1

final = []
if card:
    c = inp("-loop", "1", "-framerate", "60", "-t", "1.0", "-i", os.path.join(workdir, "card.png"))
    s = inp("-f", "lavfi", "-t", "1.0", "-i", "anullsrc=r=48000:cl=stereo")
    chain.append(f"[{c}:v]fps=60,format=yuv420p,fade=t=in:st=0:d=0.25,fade=t=out:st=0.75:d=0.25,setpts=PTS-STARTPTS[cv]")
    chain.append(f"[{s}:a]aformat=channel_layouts=stereo,asetpts=PTS-STARTPTS[ca]")
    final += ["[cv]", "[ca]"]
if kill_at is not None:
    start = max(0.0, kill_at - 2.55)
    t = inp("-ss", "%.3f" % start, "-t", "3.3", "-i", rec_file)
    chain.append(f"[{t}:v]fps=60,format=yuv420p,fade=t=in:st=0:d=0.2,fade=t=out:st=2.9:d=0.4,setpts=PTS-STARTPTS[tv]")
    chain.append(f"[{t}:a]aresample=48000,aformat=channel_layouts=stereo,afade=t=in:st=0:d=0.1,afade=t=out:st=2.6:d=0.7,asetpts=PTS-STARTPTS[ta]")
    final += ["[tv]", "[ta]"]
m = inp("-i", rec_file)
if cut and ready:
    a_cut = max(ready) + 0.85; b_cut = match_start - 0.05
    if b_cut > a_cut + 0.5:
        print("Cutting %.2f s to %.2f s (%.2f s): the last player is ready at %.2f s, the host presses Start at %.2f s" % (a_cut, b_cut, b_cut - a_cut, max(ready), match_start))
        chain.append(f"[{m}:v]split=2[m1][m2]")
        chain.append(f"[m1]trim=0:{a_cut:.3f},setpts=PTS-STARTPTS[m1v]")
        chain.append(f"[m2]trim=start={b_cut:.3f},setpts=PTS-STARTPTS[m2v]")
        chain.append("[m1v][m2v]concat=n=2:v=1:a=0,fps=60,format=yuv420p,fade=t=in:st=0:d=0.3[mv]")
        chain.append(f"[{m}:a]asplit=2[a1][a2]")
        chain.append(f"[a1]atrim=0:{a_cut:.3f},asetpts=PTS-STARTPTS[a1t]")
        chain.append(f"[a2]atrim=start={b_cut:.3f},asetpts=PTS-STARTPTS[a2t]")
        chain.append("[a1t][a2t]acrossfade=d=0.08,aresample=48000,aformat=channel_layouts=stereo[ma]")
    else:
        cut = False
if not (cut and ready and any("[mv]" in c for c in chain)):
    chain.append(f"[{m}:v]fps=60,format=yuv420p,fade=t=in:st=0:d=0.3,setpts=PTS-STARTPTS[mv]")
    chain.append(f"[{m}:a]aresample=48000,aformat=channel_layouts=stereo,asetpts=PTS-STARTPTS[ma]")
final += ["[mv]", "[ma]"]
chain.append("".join(final) + f"concat=n={len(final)//2}:v=1:a=1[v][a]")
cmd += ["-filter_complex", ";".join(chain), "-map", "[v]", "-map", "[a]", "-c:v", "libx264", "-crf", "18", "-preset", "medium",
        "-pix_fmt", "yuv420p", "-r", "60", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart", out]
subprocess.run(cmd, check=True)
length = probe("ffprobe", "-v", "error", "-show_entries", "format=duration", "-of", "csv=p=0", out)
print("Wrote %s (%.1f s)" % (out, float(length)))
PY
