#region File Description
//-----------------------------------------------------------------------------
// TripleLaserPowerUp.cs
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
    /// A power-up that gives a player a triple-laser-shooting weapon.
    /// </summary>
    class TripleLaserPowerUp : PowerUp
    {
        #region Initialization
        /// <summary>
        /// Constructs a new triple-laser power-up.
        /// </summary>
        /// <param name="world">The world that this power-up belongs to.</param>
        public TripleLaserPowerUp(World world)
            : base(world) 
        {
            this.color = Color.CornflowerBlue;
            // three bolts from an emitter, in a white triangle fan with its point down
            icon = new[]
            {
                new IconPart(new float[] { 0, 15, -15, -12, 15, -12 }, true, Color.White),
                new IconPart(new float[] { 0, 6, 2, 8.5f, 0, 11, -2, 8.5f }, true, Color.White),
                new IconPart(new float[] { 0, -10, 2.5f, -3, 0, 4, -2.5f, -3 }, true, null),
                new IconPart(new float[] { -8, -8, -4, -4, -1.5f, 3, -6, -1 }, true, null),
                new IconPart(new float[] { 8, -8, 4, -4, 1.5f, 3, 6, -1 }, true, null),
            };
        }
        #endregion

        #region Interaction
        public override PowerUpKind Kind => PowerUpKind.TripleLaser;

        /// <summary>
        /// The weapon this power-up gives to a ship.
        /// </summary>
        protected override Weapon CreateWeapon(Ship ship)
        {
            return new TripleLaserWeapon(ship);
        }
        #endregion
    }
}
