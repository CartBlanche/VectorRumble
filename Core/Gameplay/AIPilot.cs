using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>How well the computer's pilots fly. Saved in the settings, so the numbers are part of the file: keep them.</summary>
    public enum AIDifficulty
    {
        Easy = 0,
        Medium = 1,
        Hard = 2,
    }

    /// <summary>What a pilot does when its shields are gone.</summary>
    enum FleeStyle
    {
        /// <summary>Straight away from the threat.</summary>
        Straight,
        /// <summary>Away, weaving left and right.</summary>
        Zigzag,
        /// <summary>To the nearest place the threat cannot see (behind a wall or an asteroid).</summary>
        Cover,
    }

    /// <summary>What a pilot is doing now. Shown by the debug overlay (VR_AIDEBUG) as a colour on the ship.</summary>
    enum AIState
    {
        /// <summary>Nobody in sight: wandering the arena, picking up power-ups on the way.</summary>
        Patrol,
        /// <summary>A rival in clear sight: closing in, circling and shooting.</summary>
        Chase,
        /// <summary>A rival just lost from sight: going to where it was last seen.</summary>
        Search,
        /// <summary>Shields gone: getting away.</summary>
        Flee,
    }

    /// <summary>
    /// What an <see cref="AIDifficulty"/> means in numbers. A pilot is the same code at every level; only these change,
    /// so a level is a row in a table, not a branch in the flying.
    /// </summary>
    sealed class AIProfile
    {
        /// <summary>How often (seconds) the pilot looks again: who is in sight, which state it is in. Longer is slower to react.</summary>
        public float ReactionTime;

        /// <summary>How far (pixels) the pilot can see a rival, when nothing is in the way.</summary>
        public float SightRange;

        /// <summary>How long (seconds) a rival has to stay in sight before a patrolling pilot notices it.</summary>
        public float SpotDelay;

        /// <summary>How long (seconds) without sight of the rival before the pilot gives up searching and patrols again.</summary>
        public float LoseSightTime;

        /// <summary>By how much (radians) the first shot at a newly spotted rival misses: a warning shot.</summary>
        public float WarningMiss;

        /// <summary>How long (seconds) the pilot holds its fire after the warning shot.</summary>
        public float WarningPause;

        /// <summary>The widest a shot can miss its aim by (radians); each look picks a new error up to this.</summary>
        public float AimError;

        /// <summary>How far off the target's heading (radians) the nose may be and still fire.</summary>
        public float FireCone;

        /// <summary>The longest shot the pilot takes (pixels).</summary>
        public float FireRange;

        /// <summary>How much of the target's movement the pilot aims ahead for, from 0 (at where it is) to 1 (at where it will be).</summary>
        public float Lead;

        /// <summary>How hard the stick is pushed, 0 to 1: a slow pilot is easier to catch and to outrun.</summary>
        public float Throttle;

        /// <summary>How far (pixels) the pilot sees a power-up it will go for; 0 ignores them.</summary>
        public float PowerUpRange;

        /// <summary>How eagerly (0 to 1) the pilot steps out of the way of a shot coming at it.</summary>
        public float Dodge;

        /// <summary>How far away (pixels of the shot's travel) the pilot sees a shot coming.</summary>
        public float DodgeHorizon;

        /// <summary>The chance each second of laying a mine when the rival is close behind.</summary>
        public float MineChance;

        /// <summary>How far off (pixels) the pilot's idea is of where a shot that hit it came from.</summary>
        public float HitLocateError;

        /// <summary>The shield (0 to 1) at or below which the pilot runs.</summary>
        public float FleeShield;

        public FleeStyle Flee;

        public static AIProfile For(AIDifficulty difficulty)
        {
            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    return new AIProfile
                    {
                        ReactionTime = 0.9f, SightRange = 450f, SpotDelay = 1.2f, LoseSightTime = 1.5f,
                        WarningMiss = 0.5f, WarningPause = 0.9f,
                        AimError = 0.55f, FireCone = 0.45f, FireRange = 380f, Lead = 0f, Throttle = 0.6f,
                        PowerUpRange = 150f, Dodge = 0.3f, DodgeHorizon = 150f, MineChance = 0.1f,
                        FleeShield = 0.05f, Flee = FleeStyle.Straight, HitLocateError = 220f,
                    };
                case AIDifficulty.Hard:
                    return new AIProfile
                    {
                        ReactionTime = 0.2f, SightRange = 1000f, SpotDelay = 0.25f, LoseSightTime = 3f,
                        WarningMiss = 0.3f, WarningPause = 0.35f,
                        AimError = 0.05f, FireCone = 0.2f, FireRange = 640f, Lead = 1f, Throttle = 1f,
                        PowerUpRange = 450f, Dodge = 1f, DodgeHorizon = 450f, MineChance = 1.2f,
                        FleeShield = 0.25f, Flee = FleeStyle.Cover, HitLocateError = 0f,
                    };
                default:
                    return new AIProfile
                    {
                        ReactionTime = 0.45f, SightRange = 700f, SpotDelay = 0.6f, LoseSightTime = 2f,
                        WarningMiss = 0.4f, WarningPause = 0.6f,
                        AimError = 0.2f, FireCone = 0.3f, FireRange = 500f, Lead = 0.7f, Throttle = 0.85f,
                        PowerUpRange = 300f, Dodge = 0.6f, DodgeHorizon = 300f, MineChance = 0.5f,
                        FleeShield = 0.1f, Flee = FleeStyle.Zigzag, HitLocateError = 90f,
                    };
            }
        }
    }

    /// <summary>
    /// The computer's pilot: looks at the world and says what a player would ask of the ship, as a <see cref="ShipInput"/>.
    /// The ship does the rest with the code it uses for people, so an AI ship can do nothing a person cannot.
    ///
    /// It is a small state machine (<see cref="AIState"/>). It patrols the arena along routes found on the arena's
    /// <see cref="NavGrid"/> until a rival is in clear sight for long enough; then it chases, with a warning shot first. Lose
    /// sight and it searches where the rival was, then goes back to patrolling. With its shields gone it runs, in the way its
    /// level allows. It only fires with a clear line to the rival (no walls or asteroids between), keeps clear of walls,
    /// asteroids and incoming shots, aims ahead of a moving rival, and lays a mine when chased.
    /// </summary>
    sealed class AIPilot
    {
        // the laser's speed (LaserProjectile), for aiming ahead
        const float ShotSpeed = 640f;

        // where the pilot likes to fight from, and how close it comes before it backs off
        const float PreferredRange = 260f;
        const float TooClose = 120f;

        const float WallClearance = 110f;
        const float AsteroidClearance = 90f;
        const float MineRange = 220f;

        // how far a ship is from a wall for a line of sight, a shot, and a route
        const float ShotRadius = 4f;
        const float ShipRadius = 26f;

        const float ArriveDistance = 45f;
        const float RecoveredShield = 0.7f;
        const float MinFleeTime = 1.5f;
        const float ZigzagTime = 0.45f;

        /// <summary>Shows what each pilot is doing: its state as a colour, and its route. Set by the VR_AIDEBUG environment variable.</summary>
        internal static bool ShowDebug = Environment.GetEnvironmentVariable("VR_AIDEBUG") != null;

        readonly Ship ship;
        readonly World world;
        readonly AIProfile profile;
        readonly Random random;
        readonly List<Vector2> path = new List<Vector2>();

        AIState state = AIState.Patrol;
        Ship rival;                     // the nearest rival in the match, seen or not
        bool sees;                      // the rival is in clear sight, as of the last look
        Vector2 lastKnown;              // where the rival was last seen
        float lookTimer;
        float time;
        float aimError;

        float spotTimer;
        float lostTimer;
        bool warningPending;
        float warningDeadline;
        float holdFireUntil;

        float fleeUntil;
        float zigzagTimer;
        float zigzagSide = 1f;
        bool hasCover;
        Vector2 cover;

        PowerUp powerUp;
        Vector2 patrolPoint;
        bool hasPatrolPoint;

        Vector2 pathGoal;
        int pathIndex;
        float replanTimer;
        Vector2 stuckPosition;
        float stuckTimer;
        float unstickUntil;
        Vector2 unstickDirection;
        float slideSide = 1f;           // which way round an obstacle the pilot slides; swapped when it gets stuck
        int unseenLooks;

        public AIPilot(Ship ship, World world, AIDifficulty difficulty, int seed)
        {
            this.ship = ship ?? throw new ArgumentNullException(nameof(ship));
            this.world = world ?? throw new ArgumentNullException(nameof(world));
            profile = AIProfile.For(difficulty);
            random = new Random(seed);
        }

        public AIState State => state;

        /// <summary>What the pilot asks of the ship for this update.</summary>
        public ShipInput Think(float elapsedTime)
        {
            var input = new ShipInput();
            if (ship.Dead)
            {
                // a new life starts with a fresh mind
                Switch(AIState.Patrol);
                hasPatrolPoint = false;
                hasCover = false;
                path.Clear();
                lookTimer = 0f;
                return input;
            }

            time += elapsedTime;
            CheckStuck(elapsedTime);
            lookTimer -= elapsedTime;
            replanTimer -= elapsedTime;
            if (lookTimer <= 0f)
            {
                Look();
            }

            // steering: the things to keep away from come first, then where the pilot wants to be
            Vector2 away = WallAvoidance() * 2f + AsteroidAvoidance() * 1.5f;
            Vector2 shot = ShotAvoidance();
            Vector2 want = time < unstickUntil ? unstickDirection : Want(elapsedTime);

            want = SlideAlong(away, want);
            Vector2 move = away + shot * profile.Dodge * 1.5f + want;
            if (move.LengthSquared() > 0.0001f)
            {
                float throttle = state == AIState.Patrol ? profile.Throttle * 0.8f : profile.Throttle;
                input.Move = Vector2.Normalize(move) * throttle;
            }

            Fight(elapsedTime, ref input);
            return input;
        }

        // not got anywhere for two seconds while it meant to (a corner, an asteroid in the way): think again
        void CheckStuck(float elapsedTime)
        {
            stuckTimer += elapsedTime;
            if (stuckTimer < 2f)
            {
                return;
            }

            stuckTimer = 0f;
            bool holding = state == AIState.Flee && profile.Flee == FleeStyle.Cover && hasCover
                && Vector2.Distance(ship.Position, cover) < ArriveDistance;
            if (!holding && Vector2.DistanceSquared(ship.Position, stuckPosition) < 25f * 25f)
            {
                // wedged between an obstacle and where it wants to go: pick somewhere else, and break out sideways for a moment
                hasPatrolPoint = false;
                path.Clear();
                slideSide = -slideSide;
                if (ShowDebug)
                {
                    Console.WriteLine("[AI] " + (ship.PilotName ?? ship.Name) + " stuck in " + state + " at " + ship.Position);
                }

                Vector2 push = WallAvoidance() + AsteroidAvoidance();
                Vector2 outward = push.LengthSquared() > 0.01f ? Vector2.Normalize(push) : Forward((float)random.NextDouble() * MathHelper.TwoPi);
                unstickDirection = Rotate(outward, slideSide * MathHelper.PiOver2 * 0.8f);
                unstickUntil = time + 1f;
            }

            stuckPosition = ship.Position;
        }

        /// <summary>
        /// Where the pilot wants to go, without the part of it that pushes into what it is keeping away from. A pilot that is
        /// pulled one way and pushed back the other stands still and swings its nose to and fro; this makes it slide along
        /// the obstacle instead (and the same way each time).
        /// </summary>
        Vector2 SlideAlong(Vector2 away, Vector2 want) => Slide(away, want, slideSide);

        internal static Vector2 Slide(Vector2 away, Vector2 want, float side)
        {
            float length = away.Length();
            if (length < 0.05f)
            {
                return want;
            }

            Vector2 outward = away / length;
            float against = Vector2.Dot(want, outward);
            if (against >= 0f)
            {
                return want;
            }

            want -= outward * against;
            Vector2 along = new Vector2(-outward.Y, outward.X) * side;
            return want + along * -against;
        }

        /// <summary>The pilot has been shot: it turns to where the shot came from, if it was not already fighting.</summary>
        internal void Hurt(Ship attacker)
        {
            if (attacker == null || attacker == ship || attacker.Dead)
            {
                return;
            }

            // not exact, except on Hard: the pilot only knows the direction it came from
            Vector2 jitter = new Vector2((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * profile.HitLocateError;
            lastKnown = attacker.Position + jitter;
            rival = attacker;

            if (state == AIState.Patrol || state == AIState.Search)
            {
                Switch(AIState.Search);
                lostTimer = 0f;
                path.Clear();
            }
        }

        void Switch(AIState next)
        {
            if (next != state && ShowDebug)
            {
                Console.WriteLine("[AI] " + (ship.PilotName ?? ship.Name) + ": " + state + " -> " + next
                    + " (sees " + sees + ", shield " + ship.ShieldFraction.ToString("0.00") + ")");
            }

            state = next;
        }

        #region Looking: who is in sight, and what to do about it

        void Look()
        {
            lookTimer = profile.ReactionTime;
            aimError = ((float)random.NextDouble() * 2f - 1f) * profile.AimError;

            FindRival();
            FindPowerUp();

            float distance = rival != null ? Vector2.Distance(ship.Position, rival.Position) : float.MaxValue;
            sees = rival != null && distance <= profile.SightRange && ClearBetween(ship.Position, rival.Position, ShotRadius);
            if (sees)
            {
                lastKnown = rival.Position;
            }

            // shields gone: run, in whatever state, as long as there is someone to run from
            if (state != AIState.Flee && rival != null && ship.ShieldFraction <= profile.FleeShield
                && (sees || state == AIState.Chase || state == AIState.Search))
            {
                Switch(AIState.Flee);
                fleeUntil = time + MinFleeTime;
                hasCover = false;
            }

            switch (state)
            {
                case AIState.Patrol:
                    if (sees)
                    {
                        spotTimer += profile.ReactionTime;
                        if (spotTimer >= profile.SpotDelay)
                        {
                            Switch(AIState.Chase);
                            unseenLooks = 0;
                            warningPending = true;
                            warningDeadline = time + 0.6f;
                        }
                    }
                    else
                    {
                        spotTimer = 0f;
                    }
                    break;

                case AIState.Chase:
                    // two looks in a row without sight (not one: the edge of an asteroid would make it flicker)
                    unseenLooks = sees ? 0 : unseenLooks + 1;
                    if (unseenLooks >= 2)
                    {
                        Switch(AIState.Search);
                        lostTimer = 0f;
                        path.Clear();
                    }
                    break;

                case AIState.Search:
                    if (sees)
                    {
                        Switch(AIState.Chase);
                        unseenLooks = 0;
                    }
                    else
                    {
                        lostTimer += profile.ReactionTime;
                        if (lostTimer >= profile.LoseSightTime)
                        {
                            Switch(AIState.Patrol);
                            hasPatrolPoint = false;
                            path.Clear();
                        }
                    }
                    break;

                case AIState.Flee:
                    if (rival == null || (time >= fleeUntil && ship.ShieldFraction >= RecoveredShield))
                    {
                        Switch(sees ? AIState.Chase : AIState.Search);
                        unseenLooks = 0;
                        lostTimer = 0f;
                        spotTimer = 0f;
                        path.Clear();
                    }
                    else if (profile.Flee == FleeStyle.Cover && !hasCover)
                    {
                        ChooseCover();
                    }
                    break;
            }
        }

        void FindRival()
        {
            rival = null;
            float best = float.MaxValue;
            var actors = world.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] is Ship other && other != ship && other.Playing && !other.Dead)
                {
                    float d = Vector2.DistanceSquared(ship.Position, other.Position);
                    if (d < best)
                    {
                        best = d;
                        rival = other;
                    }
                }
            }
        }

        void FindPowerUp()
        {
            powerUp = null;
            if (profile.PowerUpRange <= 0f)
            {
                return;
            }

            float best = profile.PowerUpRange * profile.PowerUpRange;
            var actors = world.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] is PowerUp found && !found.Dead)
                {
                    float d = Vector2.DistanceSquared(ship.Position, found.Position);
                    if (d < best)
                    {
                        best = d;
                        powerUp = found;
                    }
                }
            }
        }

        // the nearest place the rival cannot see, among some open places near the ship
        void ChooseCover()
        {
            hasCover = false;
            NavGrid nav = world.Navigation;
            if (nav == null || rival == null)
            {
                return;
            }

            float best = float.MaxValue;
            for (int i = 0; i < 30; i++)
            {
                if (!nav.TryRandomOpen(random, out Vector2 candidate))
                {
                    break;
                }

                float distance = Vector2.Distance(ship.Position, candidate);
                if (distance < 60f || distance > 700f || distance >= best || ClearBetween(rival.Position, candidate, ShotRadius))
                {
                    continue;
                }

                best = distance;
                cover = candidate;
                hasCover = true;
            }
        }

        #endregion

        #region Where to go

        Vector2 Want(float elapsedTime)
        {
            switch (state)
            {
                case AIState.Chase:
                    return ChaseWant();

                case AIState.Search:
                    return GoTo(lastKnown);

                case AIState.Flee:
                    return FleeWant(elapsedTime);

                default:
                    return PatrolWant();
            }
        }

        Vector2 ChaseWant()
        {
            if (rival == null || rival.Dead)
            {
                return Vector2.Zero;
            }

            Vector2 toRival = rival.Position - ship.Position;
            float distance = toRival.Length();
            if (distance <= 1f)
            {
                return Vector2.Zero;
            }

            Vector2 direction = toRival / distance;
            if (distance > PreferredRange)
            {
                return direction;
            }

            if (distance < TooClose)
            {
                return -direction;
            }

            // circle the rival at a fair distance, which is harder to hit than standing still
            return new Vector2(-direction.Y, direction.X) * 0.6f;
        }

        Vector2 PatrolWant()
        {
            // a power-up in reach is worth a detour
            if (powerUp != null && !powerUp.Dead)
            {
                return GoTo(powerUp.Position);
            }

            if (!hasPatrolPoint || Vector2.Distance(ship.Position, patrolPoint) < ArriveDistance)
            {
                ChoosePatrolPoint();
            }

            return GoTo(patrolPoint);
        }

        void ChoosePatrolPoint()
        {
            hasPatrolPoint = false;
            NavGrid nav = world.Navigation;
            if (nav == null)
            {
                return;
            }

            for (int tries = 0; tries < 12; tries++)
            {
                if (nav.TryRandomOpen(random, out Vector2 candidate) && Vector2.Distance(ship.Position, candidate) > 250f)
                {
                    patrolPoint = candidate;
                    hasPatrolPoint = true;
                    path.Clear();
                    return;
                }
            }
        }

        Vector2 FleeWant(float elapsedTime)
        {
            if (rival == null)
            {
                return Vector2.Zero;
            }

            Vector2 fromRival = ship.Position - rival.Position;
            Vector2 awayDirection = fromRival.LengthSquared() > 1f ? Vector2.Normalize(fromRival) : Forward(ship.Rotation);

            if (profile.Flee == FleeStyle.Cover && hasCover)
            {
                // in cover: stay there until the shields are back
                if (Vector2.Distance(ship.Position, cover) < ArriveDistance)
                {
                    return Vector2.Zero;
                }

                return GoTo(cover);
            }

            if (profile.Flee == FleeStyle.Straight)
            {
                return awayDirection;
            }

            // zigzag: away, but weaving
            zigzagTimer -= elapsedTime;
            if (zigzagTimer <= 0f)
            {
                zigzagTimer = ZigzagTime;
                zigzagSide = -zigzagSide;
            }

            return Rotate(awayDirection, zigzagSide * 0.9f);
        }

        // the direction to steer to reach the goal, round the walls if need be; zero once there
        Vector2 GoTo(Vector2 goal)
        {
            Vector2 toGoal = goal - ship.Position;
            if (toGoal.LengthSquared() < ArriveDistance * ArriveDistance)
            {
                return Vector2.Zero;
            }

            NavGrid nav = world.Navigation;
            if (nav == null || nav.LineClear(ship.Position, goal, ShipRadius))
            {
                path.Clear();
                return Vector2.Normalize(toGoal);
            }

            // a new goal, or the old route has run out, or it has been a while: find the way again
            if (path.Count == 0 || Vector2.DistanceSquared(goal, pathGoal) > 60f * 60f || replanTimer <= 0f)
            {
                replanTimer = 0.7f;
                pathGoal = goal;
                pathIndex = 0;
                if (!nav.FindPath(ship.Position, goal, path, ShipRadius))
                {
                    // no way there (a sealed pocket): forget the goal
                    hasPatrolPoint = false;
                    return Vector2.Normalize(toGoal);
                }
            }

            // on to the next point once this one is reached, or once the one after it is in clear view
            while (pathIndex < path.Count - 1
                && (Vector2.DistanceSquared(ship.Position, path[pathIndex]) < ArriveDistance * ArriveDistance
                    || nav.LineClear(ship.Position, path[pathIndex + 1], ShipRadius)))
            {
                pathIndex++;
            }

            Vector2 toPoint = path[Math.Min(pathIndex, path.Count - 1)] - ship.Position;
            return toPoint.LengthSquared() > 1f ? Vector2.Normalize(toPoint) : Vector2.Zero;
        }

        #endregion

        #region Fighting

        void Fight(float elapsedTime, ref ShipInput input)
        {
            if (rival == null || rival.Dead)
            {
                return;
            }

            Vector2 toRival = rival.Position - ship.Position;
            float distance = toRival.Length();
            if (distance <= 1f)
            {
                return;
            }

            bool canShoot = state == AIState.Chase && distance < profile.FireRange && time >= holdFireUntil
                && ClearBetween(ship.Position, rival.Position, ShotRadius);
            Vector2 forward = Forward(ship.Rotation);

            if (canShoot)
            {
                Vector2 aim = Aim(rival.Position, rival.Velocity, ship.Position, profile.Lead);

                if (warningPending)
                {
                    // the first shot is a warning: it goes wide, and the pilot waits a moment after it
                    Vector2 wide = Rotate(aim, (random.Next(2) == 0 ? 1f : -1f) * profile.WarningMiss);
                    if (MatchRules.Current.ControllersCanShootInAllDirections)
                    {
                        input.Aim = wide;
                        EndWarning();
                    }
                    else if (!WithinCone(forward, aim, profile.WarningMiss * 0.6f))
                    {
                        // the nose is already off the rival, so the shot will miss
                        input.FireAhead = true;
                        EndWarning();
                    }
                    else if (time > warningDeadline)
                    {
                        // the nose will not leave the rival: skip the warning rather than fire a hit
                        warningPending = false;
                    }
                }
                else
                {
                    aim = Rotate(aim, aimError);
                    if (MatchRules.Current.ControllersCanShootInAllDirections)
                    {
                        // free aim: the nose does not have to point at the rival
                        input.Aim = aim;
                    }
                    else if (WithinCone(forward, aim, profile.FireCone))
                    {
                        input.FireAhead = true;
                    }
                }
            }

            // a mine for anyone close behind, whether the pilot is chasing or running
            if (distance < MineRange && Vector2.Dot(toRival / distance, forward) < -0.5f
                && random.NextDouble() < profile.MineChance * elapsedTime)
            {
                input.Mine = true;
            }
        }

        void EndWarning()
        {
            warningPending = false;
            holdFireUntil = time + profile.WarningPause;
        }

        // true if nothing a shot would hit lies between the two points: no wall, no asteroid
        bool ClearBetween(Vector2 a, Vector2 b, float radius)
        {
            NavGrid nav = world.Navigation;
            if (nav != null && !nav.LineClear(a, b, radius))
            {
                return false;
            }

            var actors = world.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] is Asteroid asteroid && !asteroid.Dead
                    && NavGrid.PointSegmentDistance(asteroid.Position, a, b) < asteroid.Radius + radius)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region What to keep away from

        Vector2 WallAvoidance()
        {
            Vector2 push = Vector2.Zero;
            Rectangle safe = world.SafeDimensions;
            Vector2 p = ship.Position;

            push.X += Falloff(p.X - safe.Left, WallClearance) - Falloff(safe.Right - p.X, WallClearance);
            push.Y += Falloff(p.Y - safe.Top, WallClearance) - Falloff(safe.Bottom - p.Y, WallClearance);

            // the arena's own walls, which stand inside the screen
            Vector2[] walls = world.SelectedArena?.Walls;
            if (walls != null)
            {
                Vector2 size = world.Dimensions;
                for (int i = 0; i + 1 < walls.Length; i += 2)
                {
                    push += Repulsion(p, walls[i] * size, walls[i + 1] * size, WallClearance + ship.Radius);
                }
            }

            return push;
        }

        Vector2 AsteroidAvoidance()
        {
            Vector2 push = Vector2.Zero;
            var actors = world.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                if (actors[i] is Asteroid asteroid && !asteroid.Dead)
                {
                    Vector2 offset = ship.Position - asteroid.Position;
                    float reach = AsteroidClearance + asteroid.Radius;
                    float d = offset.Length();
                    if (d > 0.001f && d < reach)
                    {
                        push += offset / d * (1f - d / reach);
                    }
                }
            }

            return push;
        }

        // a step to the side of the nearest shot that is coming at the ship
        Vector2 ShotAvoidance()
        {
            if (profile.Dodge <= 0f)
            {
                return Vector2.Zero;
            }

            Vector2 best = Vector2.Zero;
            float bestTime = float.MaxValue;
            var actors = world.Actors;
            for (int i = 0; i < actors.Count; i++)
            {
                if (!(actors[i] is Projectile shot) || shot.Dead || shot.Owner == ship || shot is MineProjectile)
                {
                    continue;
                }

                if (Incoming(ship.Position, shot.Position, shot.Velocity, ship.Radius + 35f, profile.DodgeHorizon, out float arrives, out Vector2 side)
                    && arrives < bestTime)
                {
                    bestTime = arrives;
                    best = side;
                }
            }

            return best;
        }

        #endregion

        #region Debug

        /// <summary>The state as a colour on the ship, and the route it is following (when VR_AIDEBUG is set).</summary>
        internal void DebugDraw(LineBatch lineBatch)
        {
            Color colour;
            switch (state)
            {
                case AIState.Chase: colour = Color.Red; break;
                case AIState.Search: colour = Color.Yellow; break;
                case AIState.Flee: colour = Color.Cyan; break;
                default: colour = Color.LimeGreen; break;
            }

            Vector2 p = ship.Position;
            lineBatch.DrawLineList(p + new Vector2(-30, -30), p + new Vector2(30, -30), colour, 2);
            lineBatch.DrawLineList(p + new Vector2(30, -30), p + new Vector2(30, 30), colour, 2);
            lineBatch.DrawLineList(p + new Vector2(30, 30), p + new Vector2(-30, 30), colour, 2);
            lineBatch.DrawLineList(p + new Vector2(-30, 30), p + new Vector2(-30, -30), colour, 2);

            Vector2 at = p;
            for (int i = Math.Min(pathIndex, path.Count); i < path.Count; i++)
            {
                lineBatch.DrawLineList(at, path[i], colour * 0.6f, 1);
                at = path[i];
            }
        }

        #endregion

        #region The arithmetic (static, so that it can be tested)

        /// <summary>Forward as a ship faces it: rotation 0 is up the screen.</summary>
        internal static Vector2 Forward(float rotation) => new Vector2((float)Math.Sin(rotation), -(float)Math.Cos(rotation));

        internal static Vector2 Rotate(Vector2 v, float radians)
        {
            float cos = (float)Math.Cos(radians), sin = (float)Math.Sin(radians);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        /// <summary>1 at the wall, 0 at the clearance and beyond.</summary>
        internal static float Falloff(float distance, float clearance) =>
            distance >= clearance ? 0f : 1f - Math.Max(distance, 0f) / clearance;

        /// <summary>
        /// The unit direction to fire in from <paramref name="from"/> to hit a target moving at <paramref name="velocity"/>,
        /// by <paramref name="lead"/> of the way from aiming at it (0) to aiming where it will be (1).
        /// </summary>
        internal static Vector2 Aim(Vector2 targetPosition, Vector2 velocity, Vector2 from, float lead)
        {
            Vector2 toTarget = targetPosition - from;
            float time = toTarget.Length() / ShotSpeed;
            Vector2 aimAt = targetPosition + velocity * (time * lead);
            Vector2 aim = aimAt - from;
            return aim.LengthSquared() > 0.0001f ? Vector2.Normalize(aim) : Forward(0f);
        }

        /// <summary>True if the direction <paramref name="aim"/> is within the cone (radians) either side of <paramref name="forward"/>.</summary>
        internal static bool WithinCone(Vector2 forward, Vector2 aim, float cone)
        {
            float dot = MathHelper.Clamp(Vector2.Dot(Vector2.Normalize(forward), Vector2.Normalize(aim)), -1f, 1f);
            return Math.Acos(dot) <= cone;
        }

        /// <summary>The push away from a wall segment, 0 beyond the clearance and 1 touching it.</summary>
        internal static Vector2 Repulsion(Vector2 point, Vector2 a, Vector2 b, float clearance)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            float t = lengthSquared > 0f ? MathHelper.Clamp(Vector2.Dot(point - a, ab) / lengthSquared, 0f, 1f) : 0f;
            Vector2 offset = point - (a + ab * t);
            float d = offset.Length();
            if (d >= clearance || d < 0.001f)
            {
                return Vector2.Zero;
            }

            return offset / d * (1f - d / clearance);
        }

        /// <summary>
        /// Whether a shot at <paramref name="shotPosition"/> moving at <paramref name="shotVelocity"/> will pass within
        /// <paramref name="hitRadius"/> of the ship in the next moments (within <paramref name="horizon"/> pixels of travel),
        /// when, and which way to step to get out of its path.
        /// </summary>
        internal static bool Incoming(Vector2 ship, Vector2 shotPosition, Vector2 shotVelocity, float hitRadius, float horizon, out float time, out Vector2 side)
        {
            time = 0f;
            side = Vector2.Zero;

            float speedSquared = shotVelocity.LengthSquared();
            if (speedSquared < 1f)
            {
                return false;
            }

            Vector2 toShip = ship - shotPosition;
            float along = Vector2.Dot(toShip, shotVelocity) / speedSquared;      // seconds until it is level with the ship
            if (along <= 0f)
            {
                return false;
            }

            float speed = (float)Math.Sqrt(speedSquared);
            if (along * speed > horizon)
            {
                return false;
            }

            Vector2 closest = shotPosition + shotVelocity * along;
            Vector2 miss = ship - closest;
            if (miss.LengthSquared() > hitRadius * hitRadius)
            {
                return false;
            }

            // step across the shot's path, away from the line it is on (either side if it is dead centre)
            Vector2 path = shotVelocity / speed;
            Vector2 perpendicular = new Vector2(-path.Y, path.X);
            side = Vector2.Dot(miss, perpendicular) >= 0f ? perpendicular : -perpendicular;
            time = along;
            return true;
        }

        #endregion
    }
}
