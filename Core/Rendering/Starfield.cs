#region File Description
//-----------------------------------------------------------------------------
// Starfield.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
#endregion

namespace VectorRumble
{
    /// <summary>
    /// A starfield represented by single-pixel "stars", with parallax-scrolling, in front of a sky (see <see cref="Sky"/>).
    /// </summary>
    class Starfield
    {
        #region Constants
        /// <summary>
        /// The distance that the stars move per second.
        /// </summary>
        const float starVelocity = 32f;
        #endregion

        #region Fields
        readonly int count;
        readonly Rectangle bounds;

        /// <summary>
        /// The sky behind the stars: nebula, constellations, planets and a comet.
        /// </summary>
        Sky sky;

        /// <summary>
        /// The positions of each star.
        /// </summary>
        Vector2[] starPositions;

        /// <summary>
        /// The depth of each star, used for parallax.
        /// </summary>
        byte[] starDepths;

        /// <summary>
        /// Where the starfield began, and what it is measured from.
        /// </summary>
        readonly Vector2 centre;

        /// <summary>
        /// The relative "target position" of the starfield, compared with the
        /// currentPosition field to create motion.
        /// </summary>
        Vector2 targetPosition;

        /// <summary>
        /// The relative "current position" of the starfield, compared with the
        /// targetPosition field to create motion.
        /// </summary>
        Vector2 currentPosition;
        #endregion

        #region Initialization
        /// <summary>
        /// Construct a new starfield.
        /// </summary>
        /// <param name="count">The number of stars in the starfield.</param>
        /// <param name="bounds">The bounds of star generation.</param>
        /// <param name="skyIndex">The number of the sky behind the stars (see <see cref="SkyPool"/>).</param>
        public Starfield(int count, Rectangle bounds, byte skyIndex = 0)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            this.count = count;
            this.bounds = bounds;
            centre = new Vector2(
                bounds.Left + (bounds.Right - bounds.Left) / 2,
                bounds.Top + (bounds.Bottom - bounds.Top) / 2);
            SetSky(skyIndex);
        }

        /// <summary>
        /// The number of the sky now behind the stars.
        /// </summary>
        public byte SkyIndex { get; private set; }

        /// <summary>
        /// The sky now behind the stars.
        /// </summary>
        public Sky Sky => sky;

        /// <summary>
        /// Choose the sky behind the stars. The stars are made again from the sky's own seed, so every machine with
        /// the same number sees the same picture, and the movement starts from rest.
        /// </summary>
        public void SetSky(byte skyIndex)
        {
            SkyIndex = skyIndex;
            SkyEntry entry = SkyPool.Get(skyIndex);
            sky = new Sky(entry);

            var random = new SkyRandom(entry.Seed ^ 0x5157);
            currentPosition = targetPosition = centre;
            starPositions = new Vector2[count];
            starDepths = new byte[count];
            for (int i = 0; i < count; i++)
            {
                starPositions[i] = new Vector2(
                    random.Next(bounds.Left, bounds.Right),
                    random.Next(bounds.Top, bounds.Bottom));
                starDepths[i] = (byte)random.Next(1, 255);
            }
        }
        #endregion

        #region Update and Draw
        /// <summary>
        /// Update the starfield.
        /// </summary>
        /// <param name="elapsedTime">The amount of elapsed time, in seconds.</param>
        public void Update(float elapsedTime)
        {
            sky.Update(elapsedTime);
            if (targetPosition != currentPosition)
            {
                Vector2 movement = targetPosition - currentPosition;
                // if the movement is within 1 square in each direction, 
                // then we're close enough
                if (movement.LengthSquared() < 1.414f)  // approxmation of sqrt(2)
                {
                    currentPosition = targetPosition;
                    return;
                }
                // move the current position
                movement = Vector2.Normalize(movement) * (starVelocity * elapsedTime);
                currentPosition += movement;
                // move the stars, scaled by their depth
                for (int i = 0; i < starPositions.Length; i++)
                {
                    starPositions[i] += movement * ((float)starDepths[i] / 255f);
                }
            }
        }

        /// <summary>
        /// Render the sky and the stars, behind everything else.
        /// </summary>
        /// <param name="spriteBatch">The spritebatch that renders the stars.</param>
        /// <param name="lineBatch">The line batch that renders the constellation lines.</param>
        /// <param name="assets">The textures the sky is made of.</param>
        /// <param name="font">The font of the name labels (may be null).</param>
        /// <param name="layers">The parts of the sky to draw.</param>
        public void Draw(SpriteBatch spriteBatch, LineBatch lineBatch, SkyAssets assets, SpriteFont font, SkyLayers layers)
        {
            if (spriteBatch == null)
            {
                throw new ArgumentNullException(nameof(spriteBatch));
            }

            if (lineBatch == null)
            {
                throw new ArgumentNullException(nameof(lineBatch));
            }

            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            sky.Draw(spriteBatch, lineBatch, assets, font, layers, currentPosition - centre);

            if ((layers & SkyLayers.Stars) != 0)
            {
                spriteBatch.Begin();
                for (int i = 0; i < starPositions.Length; i++)
                {
                    Color starColor = new Color(starDepths[i], starDepths[i],
                        starDepths[i]);
                    spriteBatch.Draw(assets.Blank, new Rectangle((int)starPositions[i].X,
                        (int)starPositions[i].Y, 1, 1), starColor);
                }

                spriteBatch.End();
            }
        }
        #endregion

        #region Interaction
        /// <summary>
        /// Assign the target position for the starfield to scroll to.
        /// </summary>
        /// <param name="targetPosition">The target position.</param>
        public void SetTargetPosition(Vector2 targetPosition)
        {
            this.targetPosition = targetPosition;
        }
        #endregion
    }
}
