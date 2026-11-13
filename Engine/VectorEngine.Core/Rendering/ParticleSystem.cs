using System;
using Microsoft.Xna.Framework;

namespace VectorEngine.Rendering;

/// <summary>
/// A system for maintaining and rendering particles in this game.
/// </summary>
public class ParticleSystem
{
    /// <summary>
    /// The amount that the alpha on each particle diminishes per second.
    /// </summary>
    const float alphaReductionPerSecond = 45f;

    /// <summary>
    /// The percent that the velocity on each particle diminishes per second.
    /// </summary>
    const float velocityPercentReductionPerSecond = 0.98f;


    /// <summary>
    /// Shared random source for particle jitter. Visual only: do not use it for anything the simulation depends on.
    /// Games may replace it (for example with a seeded one in tests).
    /// </summary>
    public static Random Random { get; set; } = new Random();

    /// <summary>
    /// The data for a single particle in a system.
    /// </summary>
    struct Particle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
    }

    /// <summary>
    /// The amount of time left before this particle system disappears.
    /// </summary>
    float lifeRemaining;

    /// <summary>
    /// The number of particles in this particle system.
    /// </summary>
    int count;

    /// <summary>
    /// The total lifetime of the particle system.
    /// </summary>
    float life;

    /// <summary>
    /// The list of particles in this system.
    /// </summary>
    Particle[] particles = null;

    /// <summary>
    /// The position of the particle system.
    /// </summary>
    Vector2 position;

    /// <summary>
    /// The direction that this particle system is moving in.
    /// </summary>
    Vector2 direction;

    /// <summary>
    /// The minimum velocity of particles when the system starts.
    /// </summary>
    float minVelocity;

    /// <summary>
    /// The maximum velocity of particles when the system starts.
    /// </summary>
    float maxVelocity;

    /// <summary>
    /// The length of the lines drawn for each particle.
    /// </summary>
    float tailLength;

    /// <summary>
    /// The colors used for the particles.
    /// </summary>
    Color[] colors;


    private VectorPolygon innerPolygon = VectorPolygon.CreateCircle(Vector2.Zero, 10f, 16);
    private Color color;


    /// <summary>
    /// If true, the particle system is still updating and rendering.
    /// </summary>
    public bool IsActive
    {
        get
        {
            return (lifeRemaining > 0f);
        }
    }


    /// <summary>
    /// Constructs a new particle system object using the given parameters.
    /// </summary>
    /// <param name="position">The position of the system.</param>
    /// <param name="direction">The direction that the system is moving in.</param>
    /// <param name="count">The number of particles in the system.</param>
    /// <param name="minVelocity">The minimum velocity of particles.</param>
    /// <param name="maxVelocity">The maximum velocity of particles.</param>
    /// <param name="life">The lifetime of the system.</param>
    /// <param name="tailLength">The length of the tails of the particles.</param>
    /// <param name="colors">The colors of the particles.</param>
    public ParticleSystem(Vector2 position, Vector2 direction, int count, 
        float minVelocity, float maxVelocity, float life, float tailLength, 
        params Color[] colors)
    {
        Reset(position, direction, count, minVelocity, maxVelocity, life,
            tailLength, colors);
    }

    /// <summary>
    /// Resets the particle system object using the new values.
    /// </summary>
    /// <param name="position">The position of the system.</param>
    /// <param name="direction">The direction that the system is moving in.</param>
    /// <param name="count">The number of particles in the system.</param>
    /// <param name="minVelocity">The minimum velocity of particles.</param>
    /// <param name="maxVelocity">The maximum velocity of particles.</param>
    /// <param name="life">The lifetime of the system.</param>
    /// <param name="tailLength">The length of the tails of the particles.</param>
    /// <param name="colors">The colors of the particles.</param>
    public void Reset(Vector2 position, Vector2 direction, int count,
        float minVelocity, float maxVelocity, float life, float tailLength,
        params Color[] colors)
    {
        // assign the parameters
        this.count = Math.Max(0, count);
        this.life = life;
        this.position = position;
        this.direction = direction;
        this.minVelocity = minVelocity;
        this.maxVelocity = maxVelocity;
        this.tailLength = tailLength;
        this.colors = colors;
        if ((this.colors == null) || (this.colors.Length < 1))
        {
            colors = new Color[1];
            colors[0] = Color.White;
        }

        // recreate the particle array if necessary
        if ((particles == null) || (particles.Length != this.count))
        {
            particles = new Particle[this.count];
        }

        Reset();
    }

    /// <summary>
    /// Reset the particle system to it's initial state, using the last set of 
    /// parameters.
    /// </summary>
    public void Reset()
    {
        // set each particle to it's default starting state
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Position = this.position;
            particles[i].Velocity = new Vector2(
                1.0f - 2.0f * (float)Random.NextDouble(), 
                1.0f - 2.0f * (float)Random.NextDouble());
            particles[i].Velocity.Normalize();
            particles[i].Velocity *= this.minVelocity + 
                this.maxVelocity * (float)Random.NextDouble();
            particles[i].Velocity += this.direction;
            particles[i].Color = this.colors[Random.Next(this.colors.Length)];
        }

        // reset the remaining lifetime on the particle system
        lifeRemaining = life;
    }


    /// <summary>
    /// Update the particle system.
    /// </summary>
    /// <param name="elapsedTime">The amount of elapsed time, in seconds.</param>
    public virtual void Update(float elapsedTime)
    {
        if (lifeRemaining > 0f)
        {
            // update each particle
            for (int i = 0; i < particles.Length; i++)
            {
                particles[i].Position += particles[i].Velocity * elapsedTime;
                particles[i].Color = new Color(
                    particles[i].Color.R,
                    particles[i].Color.G,
                    particles[i].Color.B,
                    (byte)((float)particles[i].Color.A - 
                        alphaReductionPerSecond * elapsedTime)
                    );
                particles[i].Velocity -= particles[i].Velocity * 
                    (velocityPercentReductionPerSecond * elapsedTime);
            }
            this.lifeRemaining -= elapsedTime;
        }
    }

    /// <summary>
    /// Render the particle system.
    /// </summary>
    /// <param name="lineBatch">The line batch which draws all the particles</param>
    public virtual void DrawExplosions(LineBatch lineBatch)
    {
        if (lineBatch == null) {
            throw new ArgumentNullException (nameof (lineBatch));
        }

        if (lifeRemaining > 0f)
        {
            for (int i = 0; i < particles.Length; i++)
            {
                lineBatch.DrawLineList (particles[i].Position,
                    particles[i].Position - particles[i].Velocity * tailLength,
                    particles[i].Color);
            }
        }
    }

    /// <summary>
    /// Render the particle system.
    /// </summary>
    /// <param name="lineBatch">The line batch which draws all the particles</param>
    public virtual void DrawCircle (LineBatch lineBatch)
    {
        if (lineBatch == null) {
            throw new ArgumentNullException (nameof (lineBatch));
        }

        if (lifeRemaining > 0f) {
            this.color = new Color (Color.WhiteSmoke.R, Color.WhiteSmoke.G, Color.WhiteSmoke.B, (byte)(255f * lifeRemaining /  0.5f));
            // calculate the transformation
            Matrix scaleMatrix = Matrix.CreateScale (/*0.8f + 0.1f 2 * (float)Math.Cos (lifeRemaining)*/ 0.25f / lifeRemaining) ;
            Matrix world = scaleMatrix * Matrix.CreateTranslation (position.X, position.Y, 0f);
            // Clone the polygon to avoid cumulative transformation corruption
            var circle = innerPolygon.Clone();
            circle.Transform (world);
            lineBatch.DrawEmptyPolygon(circle, color);
        }
    }
}
