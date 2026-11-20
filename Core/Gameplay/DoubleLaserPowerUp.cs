#region File Description
//-----------------------------------------------------------------------------
// DoubleLaserPowerUp.cs
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
    class DoubleLaserPowerUp : PowerUp
    {
        #region Fields
        #endregion

        #region Initialization
        /// <summary>
        /// Constructs a new DoubleLaserPowerUp.
        /// </summary>
        /// <param name="world">The world that this power-up belongs to.</param>
        public DoubleLaserPowerUp(World world) 
            : base(world) 
        { 
            this.color = Color.Lime;
            // twin bolts in the white ring
            icon = new[]
            {
                new IconPart(Ring(), true, Color.White),
                new IconPart(new float[] { -11, -7, 5, -7, 10, -5, 5, -3, -11, -3 }, true, null),
                new IconPart(new float[] { -11, 3, 5, 3, 10, 5, 5, 7, -11, 7 }, true, null),
            };
        }
        #endregion

        #region Interaction
        public override PowerUpKind Kind => PowerUpKind.DoubleLaser;

        /// <summary>
        /// The weapon this power-up gives to a ship.
        /// </summary>
        protected override Weapon CreateWeapon(Ship ship)
        {
            return new DoubleLaserWeapon(ship);
        }
        #endregion
    }
}
