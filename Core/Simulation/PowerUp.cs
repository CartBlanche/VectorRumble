#region File Description
//-----------------------------------------------------------------------------
// PowerUp.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VectorRumble.Networking;
#endregion

namespace VectorRumble
{
    /// <summary>
    /// The kinds of power-up. Sent over the network, so keep the numbers.
    /// </summary>
    enum PowerUpKind : byte
    {
        DoubleLaser = 0,
        TripleLaser = 1,
        Rocket = 2,
    }

    /// <summary>
    /// Base class for all power-ups that exist in the game.
    /// </summary>
    abstract class PowerUp : Actor
    {
        /// <summary>The Vector Rumble world that owns this actor.</summary>
        protected new World world => (World)base.world;

        #region Constants
        /// <summary>
        /// The scalar between time elapsed and the scale period.
        /// </summary>
        protected float timeToScalePeriod = 8f;

        /// <summary>The icons are drawn 1.25 times their design size, so a power-up is 40 px across at rest (before the pulse).</summary>
        const float iconScale = 1.25f;

        /// <summary>The icon turns once every four seconds.</summary>
        const float spinRadiansPerSecond = MathHelper.TwoPi / 4f;

        /// <summary>The collision radius in pixels: half of the 40 px icon, so a power-up is easy to hit.</summary>
        const float pickupRadius = 20f;
        #endregion

        #region Icon
        /// <summary>One line or outline of a power-up's icon, in design units (the icon is about 32 units across).</summary>
        protected sealed class IconPart
        {
            /// <summary>The outline, closed back to its first point, or an open line.</summary>
            public readonly VectorPolygon Shape;
            /// <summary>True for an outline, false for an open line.</summary>
            public readonly bool Closed;
            /// <summary>A fixed colour, or null for the power-up's own colour.</summary>
            public readonly Color? Colour;

            public IconPart(float[] xy, bool closed, Color? colour)
            {
                var points = new Vector2[xy.Length / 2];
                for (int i = 0; i < points.Length; ++i)
                {
                    points[i] = new Vector2(xy[2 * i], xy[2 * i + 1]);
                }
                Shape = new VectorPolygon(points);
                Closed = closed;
                Colour = colour;
            }
        }

        /// <summary>The parts of this power-up's icon, set by each kind.</summary>
        protected IconPart[] icon = Array.Empty<IconPart>();

        /// <summary>The white 16-sided ring (radius 16) the double laser sits in.</summary>
        protected static float[] Ring()
        {
            var circle = VectorPolygon.CreateCircle(Vector2.Zero, 16f, 16).Points;
            var xy = new float[circle.Length * 2];
            for (int i = 0; i < circle.Length; ++i)
            {
                xy[2 * i] = circle[i].X;
                xy[2 * i + 1] = circle[i].Y;
            }
            return xy;
        }

        /// <summary>The spin of the icon, in radians (looks only; the collision does not use it).</summary>
        float spin;
        #endregion

        #region Fields
        /// <summary>
        /// Keeps track of the period of the scale "pulse" of the power-up.
        /// </summary>
        protected float scalePeriodElapsed;

        /// <summary>
        /// Colors for the particle systems created when spawned and touched.
        /// </summary>
        protected Color[] particleColors = 
            {
                Color.White, Color.Lime, Color.Red, Color.CornflowerBlue, Color.Yellow
            };
        #endregion

        #region Initialization
        /// <summary>
        /// Makes a power-up of the given kind.
        /// </summary>
        public static PowerUp Create(World world, PowerUpKind kind)
        {
            switch (kind)
            {
                case PowerUpKind.TripleLaser: return new TripleLaserPowerUp(world);
                case PowerUpKind.Rocket: return new RocketPowerUp(world);
                default: return new DoubleLaserPowerUp(world);
            }
        }

        /// <summary>
        /// Which kind of power-up this is.
        /// </summary>
        public abstract PowerUpKind Kind { get; }

        /// <summary>
        /// The weapon this power-up gives to a ship.
        /// </summary>
        protected abstract Weapon CreateWeapon(Ship ship);

        /// <summary>
        /// Identifies the power-up in a network match, where the host names the power-ups.
        /// </summary>
        public ushort Id { get; set; }

        /// <summary>
        /// Constructs a new power-up.
        /// </summary>
        /// <param name="world">The world that this power-up belongs to.</param>
        public PowerUp(World world) : base(world) 
        {
            this.mass = 500f;
            this.radius = pickupRadius;
            this.polygon = VectorPolygon.CreateCircle(Vector2.Zero, pickupRadius, 16);
        }
        #endregion

        #region Drawing
        /// <summary>
        /// Render the power-up: its icon, pulsing and turning, with a trail when it moves fast.
        /// </summary>
        /// <param name="elapsedTime">The amount of elapsed time, in seconds.</param>
        /// <param name="lineBatch">The LineBatch to render to.</param>
        public override void Draw(float elapsedTime, LineBatch lineBatch)
        {
            if (lineBatch == null)
            {
                throw new ArgumentNullException(nameof(lineBatch));
            }

            // update the scale period and the spin
            scalePeriodElapsed += elapsedTime * timeToScalePeriod;
            while (scalePeriodElapsed < 0)
            {
                scalePeriodElapsed += MathHelper.TwoPi;
            }
            while (scalePeriodElapsed > MathHelper.TwoPi)
            {
                scalePeriodElapsed -= MathHelper.TwoPi;
            }
            spin = (spin + elapsedTime * spinRadiansPerSecond) % MathHelper.TwoPi;

            Matrix shape = Matrix.CreateScale(iconScale * (0.8f + 0.1f * (float)Math.Cos(scalePeriodElapsed))) *
                Matrix.CreateRotationZ(spin);
            DrawIcon(lineBatch, shape * Matrix.CreateTranslation(position.X, position.Y, 0f), 255);

            // draw the motion blur: copies behind the icon, fainter and fainter
            if (useMotionBlur && velocity.LengthSquared() > 1024f)
            {
                Vector2 backwards = Vector2.Normalize(position - lastPosition);
                float speed = velocity.Length();
                for (int i = 1; i < speed / 16; ++i)
                {
                    int alpha = 160 / (i + 1);
                    if (alpha < 1)
                        break;
                    Vector2 blurPosition = this.position - backwards * (i * 4);
                    DrawIcon(lineBatch, shape * Matrix.CreateTranslation(blurPosition.X, blurPosition.Y, 0), alpha);
                }
            }
        }

        /// <summary>Draw every part of the icon with the given transform and alpha (255 is solid).</summary>
        void DrawIcon(LineBatch lineBatch, Matrix world, int alpha)
        {
            foreach (IconPart part in icon)
            {
                part.Shape.Transform(world);
                Color c = part.Colour ?? color;
                if (alpha < 255)
                {
                    c = new Color(c.R, c.G, c.B, alpha);
                }
                if (part.Closed)
                {
                    lineBatch.DrawEmptyPolygon(part.Shape, c);
                }
                else
                {
                    lineBatch.DrawPolygonSegments(part.Shape, c, false, 0, part.Shape.TransformedPoints.Length - 1);
                }
            }
        }
        #endregion

        #region Interaction
        /// <summary>
        /// Defines the interaction between this power-up and a target actor
        /// when they touch.
        /// </summary>
        /// <param name="target">The actor that is touching this object.</param>
        /// <returns>True if the objects meaningfully interacted.</returns>
        public override bool Touch(Actor target)
        {
            Ship ship = target as Ship;
            if (ship != null)
            {
                // in a network match the host decides who picks a power-up up, and tells the others
                NetworkMatch match = world.Match;
                if (match != null)
                {
                    if (!match.IsHost)
                    {
                        return false;
                    }
                    match.OnPowerUpTaken(this, ship);
                }

                GiveTo(ship);
                return false;
            }

            return base.Touch(target);
        }


        /// <summary>
        /// A ship picks this power-up up: it gets the weapon, and the power-up is gone.
        /// </summary>
        internal void GiveTo(Ship ship)
        {
            // give it the weapon
            ship.SetWeapon(CreateWeapon(ship));
            // tickle the ship's vibration motors
            ship.FireGamepadMotors(0f, 0.25f);
            // add a particle system for effect
            world.Particles.Emit(ParticleEffects.Burst(24, 32f, 64f, 2f, 0.1f, particleColors), this.Position);
            // play the "power-up picked up" cue
            world.AudioManager.PlayCue("powerUpTouch");
            // kill the power-up
            Die(ship);
        }

        /// <summary>
        /// Damage this power-up by the amount provided.
        /// </summary>
        /// <remarks>
        /// Power-ups cannot be damaged.
        /// </remarks>
        /// <param name="source">The actor responsible for the damage.</param>
        /// <param name="damageAmount">The amount of damage.</param>
        /// <returns>If true, this object was damaged.</returns>
        public override bool Damage(Actor source, float damageAmount)
        {
            return false;
        }


        /// <summary>
        /// Place this power-up in the world.
        /// </summary>
        /// <param name="findSpawnPoint">
        /// If true, the power-up's position is changed to a valid, non-colliding point.
        /// </param>
        public override void Spawn(bool findSpawnPoint)
        {
            // spawn the power-up
            base.Spawn(findSpawnPoint);
            // recreate the particle array using the current color
            this.particleColors = new Color[] { this.color, Color.White };
            // add a particle effect and play a sound effect
            world.Particles.Emit(ParticleEffects.Burst(24, 32f, 64f, 2f, 0.1f, particleColors), this.position);
            world.AudioManager.PlayCue("powerUpSpawn");
        }
        #endregion
    }
}
