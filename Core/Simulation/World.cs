#region File Description
//-----------------------------------------------------------------------------
// World.cs
//
// Microsoft XNA Community Game Platform
// Copyright (C) Microsoft Corporation. All rights reserved.
//-----------------------------------------------------------------------------
#endregion

#region Using Statements
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using VectorRumble.Networking;
#endregion

namespace VectorRumble
{
    /// <summary>
    /// Owns all game state and executes all game-wide logic.
    /// </summary>
    class World : WorldBase
    {
        #region Constants
        /// <summary>
        /// The number of seconds before the first power-up appears in a game.
        /// </summary>
        const float initialPowerUpDelay = 10f;

        /// <summary>
        /// The time between each power-up spawn.
        /// </summary>
        const float powerUpDelay = 20f;

        /// <summary>
        /// The number of stars to generate in the starfield.
        /// </summary>
        const int starCount = 2048;

        /// <summary>
        /// How far starfield should generate outside the dimensions of the game field.
        /// </summary>
        const int starfieldBuffer = 512;
        #endregion

        #region Fields
        /// <summary>
        /// The timer to see if another power-up can arrive.
        /// </summary>
        float powerUpTimer;

        /// <summary>
        /// The audio manager that all objects in the world will use.
        /// </summary>
        private AudioManager audioManager;

        /// <summary>
        /// The starfield effect behind the game-board.
        /// </summary>
        Starfield starfield;

        /// <summary>
        /// Which Arena are we playing on
        /// </summary>
        private Arena selectedArena;
        public Arena SelectedArena {
            get {
                if (selectedArena == null)
                    CreateWalls();
                return selectedArena; 
            }
            set { selectedArena = value; } 
        }
        #endregion

        #region Properties
        /// <summary>
        /// The network match this world is part of, or null in a game on this machine alone.
        /// </summary>
        internal NetworkMatch Match { get; set; }

        public AudioManager AudioManager
        {
            get { return audioManager; }
            set { audioManager = value; }
        }

        public Starfield Starfield
        {
            get { return starfield; }
        }


        /// <summary>
        /// All ships that might enter the game.
        /// </summary>
        [ContentSerializer(SharedResource = true)]
        public Ship[] Ships => ShipManager.Ships;

        public ShipManager ShipManager { get; set; }
        public ArenaManager ArenaManager { get; set; }

        #endregion

        #region Initialization
        /// <summary>
        /// Construct a new World object, holding the game simulation.
        /// </summary>
        /// <param name="dimensions">The size of the world to create.</param>
        public World(Vector2 dimensions)
            : base(dimensions)
        {

            // create the starfield
            starfield = new Starfield(starCount, new Rectangle(
                starfieldBuffer * -1,
                starfieldBuffer  * -1,
                (int)Dimensions.X + starfieldBuffer * 2,
                (int)Dimensions.Y + starfieldBuffer * 2));


            // Get our ShipManager ready
            ShipManager = new ShipManager(this);
            ArenaManager = new ArenaManager(this);
        }

        public void AddCastOfAvailableShips()
        {
            for (int i = 0; i < ShipManager.AvailableShips.Length; i++)
            {
                Actors.Add(ShipManager.AvailableShips[i]);
            }
        }

        public void AddCastOfPlayableShips()
        {
            for (int i = 0; i < ShipManager.SelectedPlayers.Count; i++)
            {
                ShipManager.SelectedPlayers[i].PlayGame();
                Actors.Add(ShipManager.SelectedPlayers[i]);
            }

            // Once selected players become Actors, they are no longer needed.
            ShipManager.SelectedPlayers.Clear();
        }
        #endregion

        #region New Game
        public void StartNewGame()
        {
            // clear out the Actors list
            Actors.Clear();

            // add the world actor
            WorldActor worldActor = new WorldActor(this);
            Actors.Add(worldActor);
            WallActor = worldActor;

            // In a network match every machine makes the same asteroids, from the host's seed (the
            // host keeps them in step afterwards). They come before the ships, which are put
            // wherever there is room, so that the ships do not change where the asteroids go.
            if (Match != null)
            {
                Random = new Random(Match.Seed);
            }

            // spawn asteroids
            switch (MatchRules.Current.AsteroidDensity)
            {
                case AsteroidDensity.None:
                    break;
                case AsteroidDensity.Low:
                    SpawnAsteroids(4, 3, 0);
                    break;
                case AsteroidDensity.Medium:
                    SpawnAsteroids(6, 4, 1);
                    break;
                case AsteroidDensity.High:
                    SpawnAsteroids(8, 6, 2);
                    break;
            }

            if (Match != null)
            {
                Random = new Random();
            }

            // add the players to the actor list - they won't be removed
            AddCastOfPlayableShips();

            // set up the power-up timer for the initial delay
            powerUpTimer = initialPowerUpDelay;

            // set up the starfield
            starfield.SetTargetPosition(Dimensions * 0.5f);
        }


        /// <summary>
        /// Initialize the walls based on the current world rules.
        /// </summary>
        private void CreateWalls()
        {
            var ws = MatchRules.Current.Arena;
			if (ws == Strings.Arena_Random) {
                var arenas = ArenaManager.Arenas;
                ws = arenas[Random.Next(0, arenas.Length - 1)].Name;
			}
            selectedArena = ArenaManager.Arenas.First(a => a.Name == ws);
        }


        /// <summary>
        /// Create many asteroids and add them to the game world.
        /// </summary>
        /// <param name="smallCount">The number of "small" asteroids to create.</param>
        /// <param name="mediumCount">The number of "medium" asteroids to create.
        /// </param>
        /// <param name="largeCount">The number of "large" asteroids to create.</param>
        private void SpawnAsteroids(int smallCount, int mediumCount, int largeCount)
        {
            // create small asteroids
            for (int i = 0; i < smallCount; ++i)
            {
                float radius = 8.0f + 16.0f * (float)Random.NextDouble();

                Asteroid asteroid = new Asteroid(this, radius, Random);
                asteroid.Spawn(true);
            }
            // create medium-sized asteroids
            for (int i = 0; i < mediumCount; ++i)
            {
                float radius = 24.0f + 16.0f * (float)Random.NextDouble();

                Asteroid asteroid = new Asteroid(this, radius, Random);
                asteroid.Spawn(true);
            }
            // create large asteroids
            for (int i = 0; i < largeCount; ++i)
            {
                float radius = 32.0f + 64.0f * (float)Random.NextDouble();

                Asteroid asteroid = new Asteroid(this, radius, Random);
                asteroid.Spawn(true);
            }
        }
        
        
        /// <summary>
        /// Create a new power-up in the world, if possible
        /// </summary>
        void SpawnPowerUp()
        {
            // check if there is a powerup in the world
            for (int i = 0; i < Actors.Count; ++i)
            {
                if (Actors[i] is PowerUp)
                {
                    return;
                }
            }
            // create the new power-up
            PowerUp powerup = PowerUp.Create(this, (PowerUpKind)Random.Next(3));
            // add the new power-up to the world
            powerup.Spawn(true);
            // in a network match, the other machines get the same power-up
            Match?.OnPowerUpSpawned(powerup);
        }        
        #endregion

        /// <summary>
        /// The explosions and other particle effects of this world. Headless by default (tests, a server); the gameplay screen puts a
        /// ParticleManager here while it is showing the world, so the same effects are drawn.
        /// </summary>
        public ParticleField Particles { get; set; } = new ParticleField();

        #region Update and Draw
        /// <summary>
        /// Update the world simulation.
        /// </summary>
        /// <param name="elapsedTime">The amount of elapsed time, in seconds.</param>
        protected override void OnUpdated(float elapsedTime)
        {
            // the explosions and other effects (after the actors, as the old particle systems were)
            Particles.Update(elapsedTime);

            // update the starfield
            Vector2 starfieldTarget = Vector2.Zero;
            int playingPlayers = 0;
            Ship[] allShips = ShipManager.Ships;
            for (int i = 0; i < allShips.Length; i++)
            {
                if (allShips[i].Playing)
                {
                    starfieldTarget += allShips[i].Position;
                    playingPlayers++;
                }
            }
            if (playingPlayers > 0)
            {
                starfield.SetTargetPosition(starfieldTarget / playingPlayers);
            }
            starfield.Update(elapsedTime);

            // check if we can create a new power-up yet
            if (powerUpTimer > 0f)
            {
                powerUpTimer = Math.Max(powerUpTimer - elapsedTime, 0f);
            }
            // (in a network match only the host makes power-ups, and the others hear of them)
            if (powerUpTimer <= 0.0f && playingPlayers > 0 && (Match == null || Match.IsHost))
            {
                SpawnPowerUp();
                powerUpTimer = powerUpDelay;
            }

        }
        #endregion

        #region Collision
        /// <summary>The walls of the selected arena (the engine moves and collides the Actors against them).</summary>
        protected override Vector2[] Walls => SelectedArena?.Walls;

        /// <summary>Ships need more room than the rest when they are spawned.</summary>
        protected override bool NeedsExtraSpawnRoom(Actor actor) => actor is Ship;
        #endregion
    }
}
