using System;
using Microsoft.Xna.Framework;
using VectorEngine.Rendering;

namespace VectorEngine.Effects;

// <summary>
/// The data for a single particle in this game's particle systems.
/// </summary>
public class Particle
{
    /// <summary>
    /// The amount of drag applied to velocity per second, 
    /// Help simulate some air resistance or even gravity
    /// </summary>
    const float dragPerSecond = 0.9f;


    /// <summary>
    /// The colour of the particle.
    /// </summary>
    public Vector2 PreviousPosition;

    /// <summary>
    /// The position of the particle.
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// The direction that this particle is moving in.
    /// </summary>
    public Vector2 Direction;

    /// <summary>
    /// The minimum velocity of particle.
    /// </summary>
    float MinVelocity;

    /// <summary>
    /// The maximum velocity of particle.
    /// </summary>
    float MaxVelocity;

    /// <summary>
    /// The velocity vector of particle.
    /// </summary>
    Vector2 Velocity;

    /// <summary>
    /// The total lifetime of the particle.
    /// </summary>
    public float LifeTime;

    /// <summary>
    /// The original lifetime captured at construction (used for normalized age computations & fades).
    /// </summary>
    public float OriginalLifeTime { get; private set; }

    /// <summary>
    /// The colour of the particle.
    /// </summary>
    public Color Color;

    /// <summary>
    /// The how much to scale the particle.
    /// </summary>
    public float MinScale;

    /// <summary>
    /// The how much to scale the particle.
    /// </summary>
    public float MaxScale;

    /// <summary>
    /// The colour of the particle.
    /// </summary>
    public ParticleRenderType Type;

    /// <summary>
    /// The length of the lines drawn for each particle.
    /// </summary>
    public float TailLength { get; internal set; }

    /// <summary>Optional acceleration in pixels per second squared (a falling drop speeds up). Zero leaves the motion as it was.</summary>
    public Vector2 Gravity { get; set; }

    /// <summary>Optional drag per second in place of the default, e.g. 3 to stop flying chips quickly. Null keeps the default.</summary>
    public float? Drag { get; set; }

    /// <summary>
    /// Optional fixed length in world pixels for a line particle: the streak is drawn back along the particle's velocity,
    /// so it does not shrink at a low frame rate. NaN (unset) draws from the previous position, as before.
    /// </summary>
    public float StreakLength { get; set; } = float.NaN;

    /// <summary>Optional line width in world pixels for a line particle; 0 (unset) derives it from the scale, as before.</summary>
    public int LineWidth { get; set; }

    /// <summary>False keeps the colour's alpha for the whole life, e.g. a falling drop that disappears at the landing.</summary>
    public bool FadesOut { get; set; } = true;

    /// <summary>The current velocity, pixels per second.</summary>
    public Vector2 CurrentVelocity => Velocity;

    /// <summary>
    /// Check if the particle is still alive
    /// </summary>
    public bool IsAlive => LifeTime > 0;

    /// <summary>
    /// Holds a VectorPolygon object vector based particles
    /// </summary>
    public VectorPolygon Polygon { get; set; }

    /// <summary>
    /// Immutable base polygon kept so we can derive a transformed copy each draw without cumulative distortion.
    /// Only set if a polygon instance is provided on construction.
    /// </summary>
    public VectorPolygon BasePolygon { get; private set; }

    /// <summary>
    /// Optional per-particle starting scale for polygon / texture growth (overrides MinScale when set for scaling curves).
    /// </summary>
    public float StartScale { get; set; } = float.NaN; // NaN signals "unused" so legacy MinScale still works

    /// <summary>
    /// Optional per-particle ending scale for polygon / texture growth.
    /// </summary>
    public float EndScale { get; set; } = float.NaN;

    /// <summary>
    /// Determines how scale progresses over the particle lifetime.
    /// </summary>
    public ScaleEasingMode ScaleEasing { get; set; } = ScaleEasingMode.Linear;

    /// <summary>
    /// If true the alpha fade is driven by scale progress (1 - easedProgress) rather than remaining lifetime.
    /// Useful for ripple style effects where opacity dissipates as radius expands.
    /// </summary>
    public bool FadeWithScale { get; set; } = false;

    /// <summary>The opacity (0 to 255) a scale-driven fade starts from; 255 is fully opaque, less gives a faint cloud.</summary>
    public byte PeakAlpha { get; set; } = 255;

    /// <summary>
    /// Triggered when the particle "dies"
    /// Be careful of circular referencing emitters or you'll have endless particles.
    /// </summary>
    public event Action<Vector2> OnDeath;

    public Particle(Vector2 position, Vector2 velocity, float lifeTime, Color color, float scale,
                   ParticleRenderType paricleRenderType = ParticleRenderType.Texture, float tailLength = 0f, VectorPolygon vectorPolygon = null)
    {
        Position = position;
        PreviousPosition = position; // Initialize previous position
        Velocity = velocity;
        LifeTime = lifeTime;
        OriginalLifeTime = lifeTime;
        Color = color;
        MinScale = scale;
        Type = paricleRenderType;
        TailLength = tailLength;
        Polygon = vectorPolygon; // working polygon reference (may be replaced per-frame)
        if (vectorPolygon != null)
        {
            // Store a base clone if caller reuses the same polygon across particles.
            BasePolygon = vectorPolygon.Clone();
        }
    }

    // Update particle's position and reduce its lifespan
    public void Update(GameTime gameTime)
    {
        var elapsedTime = (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Store previous position before updating
        PreviousPosition = Position;

        // Update the particle position
        Position += Velocity * elapsedTime;

        Velocity += Gravity * elapsedTime;

        // Slow the particle over time
        Velocity -= Velocity * (elapsedTime * (Drag ?? dragPerSecond));

        // Reduce it's life time over time
        LifeTime -= elapsedTime;

        // Fade Colour over lifetime (normalized to original lifetime)
        if (!FadeWithScale && FadesOut)
        {
            if (OriginalLifeTime > 0f)
            {
                float alphaFrac = Math.Clamp(LifeTime / OriginalLifeTime, 0f, 1f);
                Color.A = (byte)(255f * alphaFrac);
            }
            else
            {
                Color.A = 0;
            }
        }

        // Polygon transform is now handled during rendering to avoid cumulative transforms.

        if (!IsAlive)
        {
            OnDeath?.Invoke(Position);
        }
    }
}

/// <summary>
/// Easing modes for particle scaling over lifetime.
/// </summary>
public enum ScaleEasingMode
{
    Linear,
    EaseOutQuad,
    EaseInQuad
}