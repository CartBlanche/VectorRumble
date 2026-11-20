#region File Description
//-----------------------------------------------------------------------------
// RocketPowerUp.cs
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
    /// A power-up that gives a player a rocket-launching weapon.
    /// </summary>
    class RocketPowerUp : PowerUp
    {
        #region Initialization
        /// <summary>
        /// Constructs a new rocket-launcher power-up.
        /// </summary>
        /// <param name="world">The world that this power-up belongs to.</param>
        public RocketPowerUp(World world)
            : base(world) 
        { 
            this.color = Color.Orange;
            // a rocket flying up and to the right (an upright one turned 45 degrees: wider base than flame, blue flame), in white corner brackets
            icon = new[]
            {
                new IconPart(new float[] { -14, -8, -14, -14, -8, -14 }, false, Color.White),
                new IconPart(new float[] { 8, -14, 14, -14, 14, -8 }, false, Color.White),
                new IconPart(new float[] { 14, 8, 14, 14, 8, 14 }, false, Color.White),
                new IconPart(new float[] { -8, 14, -14, 14, -14, 8 }, false, Color.White),
                new IconPart(new float[] { 7.07f, -7.07f, 4.38f, -1.27f, -1.56f, 5.52f, -5.52f, 1.56f, 1.27f, -4.38f }, true, null),
                new IconPart(new float[] { -4.81f, 2.26f, -7.42f, 7.42f, -2.26f, 4.81f }, false, new Color(77, 184, 255)),
            };
        }
        #endregion

        #region Interaction
        public override PowerUpKind Kind => PowerUpKind.Rocket;

        /// <summary>
        /// The weapon this power-up gives to a ship.
        /// </summary>
        protected override Weapon CreateWeapon(Ship ship)
        {
            return new RocketWeapon(ship);
        }
        #endregion
    }
}
