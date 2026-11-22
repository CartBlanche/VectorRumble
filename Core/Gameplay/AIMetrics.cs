using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// Measurements of how the computer's pilots fly, printed to the console as "[AI-METRICS]" lines. Off unless VR_AIDEBUG is
    /// set (<see cref="AIPilot.ShowDebug"/>), so a normal game pays nothing. Used to measure before and after a change to the AI.
    /// </summary>
    static class AIMetrics
    {
        sealed class Tally
        {
            public float Seconds;           // time alive and flying
            public float Distance;          // pixels travelled
            public float Turned;            // radians the nose has swung, added up
            public float Conflict;          // seconds pulled one way by the goal and pushed back by walls or asteroids
            public float NearWall;          // seconds within the wall margin
            public int Stuck, Deaths, Pickups, AsteroidContacts;
            public bool InContact;
            public Vector2 LastPosition;
            public float LastRotation;
            public bool HasLast, WasDead;
        }

        static readonly Dictionary<string, Tally> tallies = new Dictionary<string, Tally>();
        static int spawned;
        static float lastReport;
        static float clock;
        static float spawnClock;

        static bool On => AIPilot.ShowDebug;

        static Tally Of(Ship ship)
        {
            string name = ship.PilotName ?? ship.Name;
            if (!tallies.TryGetValue(name, out Tally t))
            {
                tallies[name] = t = new Tally();
            }

            return t;
        }

        public static void PowerUpSpawned()
        {
            if (On)
            {
                spawned++;
                spawnClock = clock;
            }
        }

        public static void PowerUpTaken(Ship ship)
        {
            if (On)
            {
                Of(ship).Pickups++;
                Console.WriteLine("[AI-METRICS] power-up taken by " + (ship.PilotName ?? ship.Name) + " " + (clock - spawnClock).ToString("0.0") + " s after it appeared");
            }
        }

        public static void Stuck(Ship ship)
        {
            if (On)
            {
                Of(ship).Stuck++;
            }
        }

        /// <summary>Once per update of a pilot. <paramref name="conflict"/>: the goal points into what the pilot is keeping away from.</summary>
        public static void Frame(Ship ship, float elapsed, bool conflict, bool nearWall, IList<Actor> actors)
        {
            if (!On)
            {
                return;
            }

            Tally t = Of(ship);
            if (ship.Dead)
            {
                if (!t.WasDead)
                {
                    t.Deaths++;
                }

                t.WasDead = true;
                t.HasLast = false;
                return;
            }

            t.WasDead = false;
            t.Seconds += elapsed;
            if (t.HasLast)
            {
                t.Distance += Vector2.Distance(ship.Position, t.LastPosition);
                t.Turned += Math.Abs(MathHelper.WrapAngle(ship.Rotation - t.LastRotation));
            }

            t.LastPosition = ship.Position;
            t.LastRotation = ship.Rotation;
            t.HasLast = true;
            if (conflict)
            {
                t.Conflict += elapsed;
            }

            if (nearWall)
            {
                t.NearWall += elapsed;
            }

            bool touching = false;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] is Asteroid a && !a.Dead && Vector2.Distance(a.Position, ship.Position) < a.Radius + ship.Radius)
                {
                    touching = true;
                    break;
                }
            }

            if (touching && !t.InContact)
            {
                t.AsteroidContacts++;
            }

            t.InContact = touching;

            clock += elapsed / Math.Max(1, tallies.Count);
            if (clock - lastReport >= 30f)
            {
                lastReport = clock;
                Report();
            }
        }

        static void Report()
        {
            Console.WriteLine("[AI-METRICS] t=" + clock.ToString("0") + "s power-ups spawned " + spawned);
            foreach (var pair in tallies)
            {
                Tally t = pair.Value;
                float speed = t.Seconds > 0f ? t.Distance / t.Seconds : 0f;
                float spin = t.Seconds > 0f ? t.Turned / t.Seconds : 0f;
                Console.WriteLine("[AI-METRICS]   " + pair.Key + ": alive " + t.Seconds.ToString("0") + "s, speed " + speed.ToString("0")
                    + " px/s, turning " + spin.ToString("0.0") + " rad/s, goal-vs-avoidance " + (t.Seconds > 0f ? t.Conflict / t.Seconds * 100f : 0f).ToString("0")
                    + "%, near wall " + (t.Seconds > 0f ? t.NearWall / t.Seconds * 100f : 0f).ToString("0")
                    + "%, stuck " + t.Stuck + ", deaths " + t.Deaths + ", power-ups " + t.Pickups + ", asteroid touches " + t.AsteroidContacts);
            }
        }
    }
}
