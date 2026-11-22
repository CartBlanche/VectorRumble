#!/usr/bin/env python3
"""Cuts between the four players' scenes while record-video.sh records, following the games' own cue lines.

  OBS_PASSWORD=... itch/video/director.py <folder with the four <Name>.log files and the go files>

The script of the video:
  Dominique opens the Multiplayer screen's host lobby; cut to John, whose Multiplayer screen shows Dominique's game appearing;
  he selects it and joins; quick cuts to Jane and Alison doing the same; cut back to Dominique's lobby, now with all four ships;
  the host starts the match and the rest is Dominique's window: the battle and the victory screen.
Each game prints "[DEVSHOT] cue <name> unix-ms <n>" when something happens (see Core/DevShots.cs); this waits for those, tells a
joining copy to go by creating its <folder>/go-<Name> file, and starts the match by creating <folder>/start. If a cue never comes,
it carries on after a timeout, so a take is never stuck.
"""
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import obs as obsmod

SECONDS_ALONE = 1.2     # the host's lobby, alone, before the cut to John
AFTER_SESSION = 1.2     # John sees the game appear, then the highlight starts
AFTER_JOIN_JOHN = 2.2   # John in the lobby before the cut to Jane
AFTER_JOIN_QUICK = 1.6  # Jane and Alison: quicker
LOBBY_LOOK = 7.0        # back on Dominique's lobby with the four ships, until the host starts


class Logs:
    def __init__(self, folder):
        self.folder = folder
        self.seen = {}

    def cues(self, name):
        try:
            lines = open(os.path.join(self.folder, name + ".log"), errors="replace").read().splitlines()
        except OSError:
            return []
        return [l.split("cue ", 1)[1].split()[0] for l in lines if "[DEVSHOT] cue " in l]

    def wait(self, name, cue, timeout):
        end = time.time() + timeout
        while time.time() < end:
            if cue in self.cues(name):
                return True
            time.sleep(0.1)
        print("director: no %r cue from %s after %ds, carrying on" % (cue, name, timeout), file=sys.stderr)
        return False


def main(folder):
    logs = Logs(folder)
    # DIRECTOR_NO_OBS=1: only make the go and start files (a run to check the games without OBS)
    obs = None if os.environ.get("DIRECTOR_NO_OBS") else obsmod.Obs(os.environ.get("OBS_HOST", "localhost"), int(os.environ.get("OBS_PORT", "4455")), os.environ.get("OBS_PASSWORD", ""))

    def cut(player):
        if obs:
            obs.request("SetCurrentProgramScene", {"sceneName": obsmod.scene_of(player)})
        print("director: cut to %s" % player, flush=True)

    def go(player):
        open(os.path.join(folder, "go-" + player), "w").close()

    try:
        cut("Dominique")
        logs.wait("Dominique", "host-lobby", 90)
        time.sleep(SECONDS_ALONE)
        cut("John")
        logs.wait("John", "session-visible", 20)
        time.sleep(AFTER_SESSION)
        go("John")
        logs.wait("John", "joined", 30)
        time.sleep(AFTER_JOIN_JOHN)
        cut("Jane")
        time.sleep(0.4)
        go("Jane")
        logs.wait("Jane", "joined", 30)
        time.sleep(AFTER_JOIN_QUICK)
        cut("Alison")
        time.sleep(0.4)
        go("Alison")
        logs.wait("Alison", "joined", 30)
        time.sleep(AFTER_JOIN_QUICK)
        cut("Dominique")
        time.sleep(LOBBY_LOOK)
        open(os.path.join(folder, "start"), "w").close()
        logs.wait("Dominique", "match-start", 20)
        logs.wait("Dominique", "game-over", 900)
        print("director: game over", flush=True)
    finally:
        if obs:
            obs.close()


if __name__ == "__main__":
    if len(sys.argv) != 2:
        print(__doc__)
        sys.exit(1)
    try:
        main(sys.argv[1])
    except (RuntimeError, OSError) as e:
        print("director: %s" % e, file=sys.stderr)
        sys.exit(2)
