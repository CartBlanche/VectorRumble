using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using VectorEngine.Collections;
using VectorEngine.Rendering;

namespace VectorEngine.Simulation
{
    /// <summary>
    /// The shared physics of a Vector Engine world: the actors and particle systems, and each frame's moving, colliding
    /// and bouncing off walls. A game derives from it and adds what is its own (the arena or level, the managers, the
    /// rules), supplying its walls through <see cref="Walls"/>.
    /// </summary>
    /// <remarks>
    /// The order of an update is fixed: every actor updates, then the world moves and collides, then the particle
    /// systems update, then <see cref="OnUpdated"/> runs, then the lists are cleaned up.
    /// </remarks>
    public abstract class WorldBase
    {
        readonly Vector2 dimensions;
        readonly CollectCollection<Actor> actors = new CollectCollection<Actor>();
        readonly CollectCollection<ParticleSystem> particleSystems = new CollectCollection<ParticleSystem>();
        readonly List<CollisionResult> collisionResults = new List<CollisionResult>();

        /// <summary>The world's random numbers (spawn points and the like). Replace it to make a run repeatable.</summary>
        public Random Random { get; set; } = new Random();

        /// <summary>The size of the world.</summary>
        public Vector2 Dimensions => dimensions;

        /// <summary>The area, inside the edges, where things may be spawned.</summary>
        public Rectangle SafeDimensions { get; set; }

        /// <summary>Every actor in the world.</summary>
        public CollectCollection<Actor> Actors => actors;

        /// <summary>Every particle system in the world.</summary>
        public CollectCollection<ParticleSystem> ParticleSystems => particleSystems;

        /// <summary>
        /// The actor that stands in for the walls: an actor with <see cref="WallBehavior.Touch"/> touches it where it
        /// crosses a wall. When none is set, the first actor in the list is used.
        /// </summary>
        public Actor WallActor { get; set; }

        /// <summary>
        /// The walls: pairs of points (the start and end of each wall) in fractions of <see cref="Dimensions"/>.
        /// Null for none.
        /// </summary>
        protected virtual Vector2[] Walls => null;

        protected WorldBase(Vector2 dimensions)
        {
            this.dimensions = dimensions;
            SafeDimensions = new Rectangle(
                (int)(dimensions.X * 0.05f), (int)(dimensions.Y * 0.05f),
                (int)(dimensions.X * 0.90f), (int)(dimensions.Y * 0.90f));
        }

        /// <summary>
        /// True for the actors that need extra room when they are spawned (the player's own). Their spawn radius is doubled,
        /// others get a tenth more.
        /// </summary>
        protected virtual bool NeedsExtraSpawnRoom(Actor actor) => false;

        /// <summary>Runs in <see cref="Update"/> after the particle systems and before the lists are cleaned up.</summary>
        protected virtual void OnUpdated(float elapsedTime)
        {
        }

        #region Update
        /// <summary>
        /// Update the world.
        /// </summary>
        /// <param name="elapsedTime">The amount of elapsed time, in seconds.</param>
        public virtual void Update(float elapsedTime)
        {
            // update all actors
            for (int i = 0; i < actors.Count; i++)
            {
                actors[i].Update(elapsedTime);
            }

            // update collision
            MoveWorld(elapsedTime);

            // update particle systems
            for (int i = 0; i < particleSystems.Count; i++)
            {
                particleSystems[i].Update(elapsedTime);
                if (particleSystems[i].IsActive == false)
                {
                    particleSystems.Garbage.Add(particleSystems[i]);
                }
            }

            OnUpdated(elapsedTime);

            // clean up the lists
            actors.Collect();
            particleSystems.Collect();
        }
        #endregion

        #region Collision
        private void MoveWorld(float elapsedTime)
        {
            Vector2 point = Vector2.Zero;
            Vector2 startLine;
            Vector2 endLine;

            // move each actor
            for (int i = 0; i < actors.Count; ++i)
            {
                if (actors[i].Dead)
                {
                    continue;
                }

                // determine how far they are going to move
                Vector2 movement = actors[i].Velocity * elapsedTime;

                // only allow actors that have not collided yet this frame to collide
                // -- otherwise, objects can "double-hit" and trade their momentum fast
                if (actors[i].CollidedThisFrame == false)
                {
                    movement = MoveAndCollide(actors[i], movement);
                }

                // determine the new position
                actors[i].Position += movement;

                // determine if their new position takes them through a wall
                Vector2[] walls = Walls;
                for (int wall = 0; wall < walls?.Length / 2; ++wall)
                {
                    startLine = new Vector2(walls[wall * 2].X * dimensions.X, walls[wall * 2].Y * dimensions.Y);
                    endLine = new Vector2(walls[wall * 2 + 1].X * dimensions.X, walls[wall * 2 + 1].Y * dimensions.Y);

                    if (actors[i].WallBehavior == WallBehavior.Touch)
                    {
                        if (Collision.LineLineIntersect(actors[i].Position, actors[i].Position - movement, startLine, endLine, out point))
                        {
                            actors[i].Touch(WallActor ?? actors[0]);
                        }
                    }
                    else
                    {
                        Collision.CircleLineCollisionResult result = new Collision.CircleLineCollisionResult();
                        if (Collision.CircleLineCollide(actors[i].Position, actors[i].Radius, startLine, endLine, ref result))
                        {
                            // if an actor hits a wall, bounce slightly
                            float vn = Vector2.Dot(actors[i].Velocity, result.Normal);
                            actors[i].Velocity -= (2.0f * vn) * result.Normal;
                            actors[i].Position += result.Normal * result.Distance;
                        }
                    }
                }
            }
        }

        private Vector2 MoveAndCollide(Actor actor, Vector2 movement)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            // make sure we care about where this actor goes
            if (actor.Dead || (actor.Collidable == false))
            {
                return movement;
            }

            // make sure the movement is significant
            if (movement.LengthSquared() <= 0f)
            {
                return movement;
            }

            // generate the list of collisions
            Collide(actor, movement);

            // determine if we had any collisions
            if (collisionResults.Count > 0)
            {
                collisionResults.Sort(CollisionResult.Compare);
                foreach (CollisionResult collision in collisionResults)
                {
                    // let the two actors touch each other, and see what happens
                    if (actor.Touch(collision.Actor) && collision.Actor.Touch(actor))
                    {
                        actor.CollidedThisFrame =
                            collision.Actor.CollidedThisFrame = true;
                        // they should react to the other, even if they just died
                        AdjustVelocities(actor, collision.Actor);
                        return Vector2.Zero;
                    }
                }
            }

            return movement;
        }

        /// <summary>
        /// Find the actors the given actor would hit by moving, nearest first, into the world's collision list.
        /// </summary>
        public void Collide(Actor actor, Vector2 movement)
        {
            collisionResults.Clear();

            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (actor.Dead || (actor.Collidable == false))
            {
                return;
            }

            // determine the movement direction and scalar
            float movementLength = movement.Length();
            if (movementLength <= 0f)
            {
                return;
            }

            // check each actor
            foreach (Actor checkActor in actors)
            {
                if ((actor == checkActor) || checkActor.Dead || !checkActor.Collidable)
                {
                    continue;
                }

                // calculate the target vector
                Vector2 checkVector = checkActor.Position - actor.Position;
                float distanceBetween = checkVector.Length() -
                    (checkActor.Radius + actor.Radius);

                // check if they could possibly touch no matter the direction
                if (movementLength < distanceBetween)
                {
                    continue;
                }

                // determine how much of the movement is bringing the two together
                float movementTowards = Vector2.Dot(movement, checkVector);

                // check to see if the movement is away from each other
                if (movementTowards < 0f)
                {
                    continue;
                }

                if (movementTowards < distanceBetween)
                {
                    continue;
                }

                CollisionResult result = new CollisionResult
                {
                    Distance = distanceBetween,
                    Normal = Vector2.Normalize(checkVector),
                    Actor = checkActor
                };
                collisionResults.Add(result);
            }
        }

        private static void AdjustVelocities(Actor actor1, Actor actor2)
        {
            // don't adjust velocities if at least one has negative mass
            if ((actor1.Mass <= 0f) || (actor2.Mass <= 0f))
            {
                return;
            }

            // determine the vectors normal and tangent to the collision
            Vector2 collisionNormal = Vector2.Normalize(
                actor2.Position - actor1.Position);
            Vector2 collisionTangent = new Vector2(
                -collisionNormal.Y, collisionNormal.X);

            // determine the velocity components along the normal and tangent vectors
            float velocityNormal1 = Vector2.Dot(actor1.Velocity, collisionNormal);
            float velocityTangent1 = Vector2.Dot(actor1.Velocity, collisionTangent);
            float velocityNormal2 = Vector2.Dot(actor2.Velocity, collisionNormal);
            float velocityTangent2 = Vector2.Dot(actor2.Velocity, collisionTangent);

            // determine the new velocities along the normal
            float velocityNormal1New = ((velocityNormal1 * (actor1.Mass - actor2.Mass))
                + (2f * actor2.Mass * velocityNormal2)) / (actor1.Mass + actor2.Mass);
            float velocityNormal2New = ((velocityNormal2 * (actor2.Mass - actor1.Mass))
                + (2f * actor1.Mass * velocityNormal1)) / (actor1.Mass + actor2.Mass);

            // determine the new total velocities
            actor1.Velocity = (velocityNormal1New * collisionNormal) +
                (velocityTangent1 * collisionTangent);
            actor2.Velocity = (velocityNormal2New * collisionNormal) +
                (velocityTangent2 * collisionTangent);
        }

        /// <summary>
        /// Find a point inside <see cref="SafeDimensions"/> where the actor fits without touching a wall or another actor.
        /// </summary>
        public virtual Vector2 FindSpawnPoint(Actor actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            Vector2 spawnPoint;
            float radius = actor.Radius;

            // fudge the radius slightly so we're not right on top of another actor
            if (NeedsExtraSpawnRoom(actor))
            {
                radius *= 2f;
            }
            else
            {
                radius *= 1.1f;
            }
            radius = (float)Math.Ceiling(radius);

            Vector2 spawnMinimum = new Vector2(
                SafeDimensions.X + radius,
                SafeDimensions.Y + radius);
            Vector2 spawnDimensions = new Vector2(
                (float)Math.Floor(SafeDimensions.Width - 2f * radius),
                (float)Math.Floor(SafeDimensions.Height - 2f * radius));
            Vector2 spawnMaximum = spawnMinimum + spawnDimensions;

            Collision.CircleLineCollisionResult result =
                new Collision.CircleLineCollisionResult();

            bool valid = true;
            while (true)
            {
                valid = true;

                // generate a new spawn point
                spawnPoint = new Vector2(
                    spawnMinimum.X + spawnDimensions.X * (float)Random.NextDouble(),
                    spawnMinimum.Y + spawnDimensions.Y * (float)Random.NextDouble());

                if ((spawnPoint.X < spawnMinimum.X) ||
                    (spawnPoint.Y < spawnMinimum.Y) ||
                    (spawnPoint.X > spawnMaximum.X) ||
                    (spawnPoint.Y > spawnMaximum.Y))
                {
                    continue;
                }

                // if we don't collide, then one is good enough
                if (actor.Collidable == false)
                {
                    break;
                }

                // check against the walls
                Vector2[] walls = Walls;
                if (valid == true && walls != null)
                {
                    Vector2 startLine;
                    Vector2 endLine;
                    for (int wall = 0; wall < walls.Length / 2; wall++)
                    {
                        startLine = new Vector2(walls[wall * 2].X * dimensions.X, walls[wall * 2].Y * dimensions.Y);
                        endLine = new Vector2(walls[wall * 2 + 1].X * dimensions.X, walls[wall * 2 + 1].Y * dimensions.Y);
                        if (Collision.CircleLineCollide(spawnPoint, radius, startLine, endLine, ref result))
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                // check against all other actors
                if (valid == true)
                {
                    foreach (Actor checkActor in actors)
                    {
                        if ((actor == checkActor) || checkActor.Dead)
                        {
                            continue;
                        }

                        if (Collision.CircleCircleIntersect(checkActor.Position,
                            checkActor.Radius, spawnPoint, radius))
                        {
                            valid = false;
                            break;
                        }
                    }
                }

                // if we have gotten this far, then the spawn point is good
                if (valid == true)
                {
                    break;
                }
            }

            return spawnPoint;
        }
        #endregion
    }
}
