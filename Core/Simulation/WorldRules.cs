#region File Description
//-----------------------------------------------------------------------------
// WorldRules.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
#endregion

namespace VectorRumble
{
    public enum AsteroidDensity
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// This machine's own display settings. The rules of a match are in <see cref="MatchRules"/>.
    /// </summary>
    public static class WorldRules
    {
        public static int BlurIntensity
        {
            get => VectorEngine.Simulation.Actor.BlurIntensity;
            set => VectorEngine.Simulation.Actor.BlurIntensity = value;
        }

        public static bool MotionBlur
        {
            get => VectorEngine.Simulation.Actor.MotionBlur;
            set => VectorEngine.Simulation.Actor.MotionBlur = value;
        }


        public static bool NeonEffect = true;
    }
}
