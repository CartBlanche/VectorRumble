using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using VectorEngine.Diagnostics;
using VectorEngine.Rendering;

namespace VectorEngine.Effects;

/// <summary>
/// A system for maintaining and rendering particles in this game.
/// </summary>
public class ParticleManager
{
    private List<Particle> emptyPolygonParticles;
    private List<Particle> filledPolygonParticles;
    private List<Particle> lineParticles;
    private List<Particle> particles;
    private List<Particle> textureParticles;


    private Random random;
    private SpriteBatch spriteBatch;
    private LineBatch lineBatch;
    private Texture2D texture;
    private Vector2 position;
    private readonly IRenderContext context;
    private Vector2 textureOrigin;

    public Vector2 Position
    {
        get => position;
        set => position = value;
    }

    /// <summary>
    /// The matrix particles are drawn with. Null (the default) is the screen's global transformation; a screen that moves a camera sets its own
    /// so effects stay where the action is.
    /// </summary>
    public Matrix? Transform { get; set; }

    /// <summary>
    /// Emit small sparks: <paramref name="count"/> particles at <paramref name="at"/> flying off in random directions at up to <paramref name="speed"/>
    /// pixels per second, each living between the two lifetimes (seconds). A general-purpose spark burst (a taser, a short circuit, a hit).
    /// </summary>
    public void EmitSparks(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float scale)
    {
        for (int i = 0; i < count; i++)
        {
            var velocity = new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1)) * speed;
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            particles.Add(new Particle(at, velocity, lifetime, color * ((float)random.NextDouble() * 0.5f + 0.5f), scale));
        }
    }

    /// <summary>
    /// A ring that grows from nothing to <paramref name="radius"/> world pixels and fades: where a drop lands. The same shape as the
    /// main menu's ripple, but any size, so it can follow the camera zoom.
    /// </summary>
    public void EmitRipple(Vector2 at, float radius, Color color, float life = 1.2f)
    {
        var ripple = new Particle(at, Vector2.Zero, life, color, 1f, ParticleRenderType.EmptyPolygon, 0f, dropRipple)
        {
            StartScale = 0.05f,
            EndScale = radius / 10f,   // dropRipple has a radius of 10
            ScaleEasing = ScaleEasingMode.EaseOutQuad,
            FadeWithScale = true,
        };
        particles.Add(ripple);
    }

    /// <summary>
    /// A bubble breaking the surface of lava: a ring that swells from a third of <paramref name="radius"/> to all of it in <paramref name="life"/>
    /// seconds (slowly at first and faster as it stretches, like a bubble in thick lava), then pops in a few sparks (<paramref name="pop"/>) and
    /// calls <paramref name="onPop"/>. All sizes are in world pixels.
    /// </summary>
    public void EmitBubble(Vector2 at, float radius, Color color, float life, Color pop, Action onPop = null)
    {
        var bubble = new Particle(at, Vector2.Zero, life, color, 1f, ParticleRenderType.EmptyPolygon, 0f, dropRipple)
        {
            StartScale = radius / 10f * 0.33f,
            EndScale = radius / 10f,
            ScaleEasing = ScaleEasingMode.EaseInQuad,
            FadeWithScale = false,
        };
        bubble.OnDeath += _ =>
        {
            EmitSparks(at, 7, pop, radius * 11f, 0.18f, 0.4f, 0.22f);
            onPop?.Invoke();
        };
        particles.Add(bubble);
    }

    /// <summary>
    /// A drop falling from <paramref name="from"/> to <paramref name="to"/> in <paramref name="fallSeconds"/>: a short streak that speeds up
    /// under gravity, then a ripple of <paramref name="rippleRadius"/> where it lands. All sizes are in world pixels.
    /// </summary>
    public void EmitDrop(Vector2 from, Vector2 to, Color color, float fallSeconds, float streakLength, int width, float rippleRadius)
    {
        var fall = to - from;
        var start = fall / fallSeconds * 0.3f;   // a drop is not at rest when it lets go
        var gravity = 2f * (fall - start * fallSeconds) / (fallSeconds * fallSeconds);
        var drop = new Particle(from, start, fallSeconds, color, 1f, ParticleRenderType.LineList)
        {
            Gravity = gravity,
            Drag = 0f,
            StreakLength = streakLength,
            LineWidth = width,
            FadesOut = false,
        };
        drop.OnDeath += _ => EmitRipple(to, rippleRadius, color);
        particles.Add(drop);
    }

    /// <summary>
    /// Fixed-length streaks flying out from <paramref name="at"/> in every direction (chips off a rock face), slowing at <paramref name="drag"/> per second.
    /// All sizes are in world pixels.
    /// </summary>
    public void EmitStreaks(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float length, int width, float drag)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            var velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed * (0.5f + (float)random.NextDouble() * 0.5f);
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            particles.Add(new Particle(at, velocity, lifetime, color, 1f, ParticleRenderType.LineList)
            {
                StreakLength = length * (0.6f + (float)random.NextDouble() * 0.4f),
                LineWidth = width,
                Drag = drag,
            });
        }
    }

    /// <summary>
    /// Soft puffs of steam: filled discs that drift slowly, grow from about <paramref name="size"/> to four times that and fade as they grow
    /// (seen from above, steam rises toward the camera, so it swells rather than climbs). All sizes are in world pixels.
    /// </summary>
    public void EmitPuffs(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float size)
    {
        var points = new Vector2[10];
        for (int k = 0; k < points.Length; k++)
        {
            float angle = k * MathHelper.TwoPi / points.Length;
            points[k] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 10f;
        }
        var disc = new VectorPolygon(points);

        for (int i = 0; i < count; i++)
        {
            float heading = (float)(random.NextDouble() * MathHelper.TwoPi);
            var velocity = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading)) * speed * (float)random.NextDouble();
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            float grow = 0.8f + (float)random.NextDouble() * 0.4f;
            particles.Add(new Particle(at, velocity, lifetime, color, 1f, ParticleRenderType.FilledPolygon, 0f, disc)
            {
                StartScale = size / 10f,
                EndScale = size / 10f * 4f * grow,
                ScaleEasing = ScaleEasingMode.EaseOutQuad,
                FadeWithScale = true,
                PeakAlpha = 70,
                Drag = 1.5f,
            });
        }
    }

    /// <summary>
    /// Small rock-shaped pieces breaking away from <paramref name="at"/>: filled polygons of about <paramref name="size"/> world pixels, each a different
    /// irregular shape, that slow and shrink as they go.
    /// </summary>
    public void EmitChunks(Vector2 at, int count, Color color, float speed, float minLifetime, float maxLifetime, float size, float drag)
    {
        for (int i = 0; i < count; i++)
        {
            int sides = 4 + random.Next(2);
            float turn = (float)(random.NextDouble() * MathHelper.TwoPi);
            var points = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                float angle = turn + k * MathHelper.TwoPi / sides + (float)(random.NextDouble() - 0.5) * 0.5f;
                points[k] = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 10f * (0.6f + (float)random.NextDouble() * 0.4f);
            }

            float heading = (float)(random.NextDouble() * MathHelper.TwoPi);
            var velocity = new Vector2((float)Math.Cos(heading), (float)Math.Sin(heading)) * speed * (0.5f + (float)random.NextDouble() * 0.5f);
            float lifetime = minLifetime + (float)random.NextDouble() * (maxLifetime - minLifetime);
            particles.Add(new Particle(at, velocity, lifetime, color, 1f, ParticleRenderType.FilledPolygon, 0f, new VectorPolygon(points))
            {
                StartScale = size / 10f,
                EndScale = size / 10f * 0.4f,
                Drag = drag,
            });
        }
    }

    /// <summary>
    /// A firework in two stages: a rocket (a short bright streak) climbs from <paramref name="from"/> to <paramref name="burstAt"/> in
    /// <paramref name="riseSeconds"/>, then bursts into <paramref name="count"/> streaks flying out in a ring (every third one in <paramref name="accent"/>)
    /// that fall under gravity, slow and fade. <paramref name="power"/> is the burst's speed in pixels per second; <paramref name="onBurst"/> is called
    /// with the burst's position (a pop sound). All sizes are in world pixels.
    /// </summary>
    public void EmitFirework(Vector2 from, Vector2 burstAt, Color color, Color accent, float riseSeconds, int count, float power, Action<Vector2> onBurst = null)
    {
        var rocket = new Particle(from, (burstAt - from) / riseSeconds, riseSeconds, color * 0.9f, 1f, ParticleRenderType.LineList)
        {
            Drag = 0f,   // a steady climb, so it reaches the burst point as its life ends
            StreakLength = 16f,
            LineWidth = 2,
            FadesOut = false,
        };
        rocket.OnDeath += at =>
        {
            EmitFireworkBurst(at, count, color, accent, power);
            onBurst?.Invoke(at);
        };
        particles.Add(rocket);
    }

    private void EmitFireworkBurst(Vector2 at, int count, Color color, Color accent, float power)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = MathHelper.TwoPi * i / count + (float)random.NextDouble() * 0.2f;
            float speed = power * (0.55f + 0.45f * (float)random.NextDouble());
            particles.Add(new Particle(at, new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * speed, 1.1f + 0.8f * (float)random.NextDouble(),
                i % 3 == 0 ? accent : color, 1f, ParticleRenderType.LineList)
            {
                Gravity = new Vector2(0f, 130f),
                StreakLength = 7f,
                LineWidth = 2,
            });
        }
    }

    public Texture2D Texture
    {
        get => texture;
        set => texture = value;
    }

    public int ParticleCount
    {
        get
        {
            if (particles != null)
            {
                return particles.Count;
            }
            else
            {
                return 0;
            }
        }
    }

    /// <param name="context">The screen being drawn to: its virtual size and window transform.</param>
    /// <param name="spriteBatch">Draws the texture particles.</param>
    /// <param name="lineBatch">Draws the line and polygon particles.</param>
    public ParticleManager(IRenderContext context, SpriteBatch spriteBatch, LineBatch lineBatch)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(spriteBatch);
        ArgumentNullException.ThrowIfNull(lineBatch);
        this.context = context;
        this.random = new Random();
        this.spriteBatch = spriteBatch;
        this.lineBatch = lineBatch;

        this.emptyPolygonParticles = new List<Particle>();
        this.filledPolygonParticles = new List<Particle>();
        this.lineParticles = new List<Particle>();
        this.particles = new List<Particle>();
        this.textureParticles = new List<Particle>();
    }

    /// <param name="texture">The picture texture particles use (drawn centred on each particle).</param>
    /// <param name="position">Where <see cref="Emit"/> places the effects.</param>
    public ParticleManager(IRenderContext context, SpriteBatch spriteBatch, LineBatch lineBatch, Texture2D texture, Vector2 position)
        : this(context, spriteBatch, lineBatch)
    {
        this.position = position;
        this.texture = texture;
        this.textureOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
    }

    // Emit particles based on the effect type
    public void Emit(int numberOfParticles, ParticleEffectType effectType, Color? color = null)
    {
        switch (effectType)
        {
            case ParticleEffectType.Confetti:
                EmitConfetti(numberOfParticles, position, color);
                break;

            case ParticleEffectType.Explosions:
                EmitLineExplosions(numberOfParticles, position, color);
                //EmitPolygonExplosions(numberOfParticles, position, color);
                break;

            case ParticleEffectType.Fireworks:
                EmitFireworks(numberOfParticles, position, color);
                break;

            case ParticleEffectType.Rain:
                //EmitRainDropRipples(numberOfParticles, position, color);
                EmitRainDrops(numberOfParticles, position, color);
                break;

            case ParticleEffectType.Sparkles:
                EmitSparkles(numberOfParticles, position, color);
                break;
        }
    }

    void EmitRainDrops(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            // Step 1: Destination Point
            Vector2 destinationPoint = new Vector2(
                (float)(random.NextDouble() * context.BaseScreenSize.X),
                (float)(random.NextDouble() * context.BaseScreenSize.Y)
            );

            // Step 2: Calculate Drift Towards Center
            Vector2 driftDirection = Vector2.Normalize(emitPosition - destinationPoint); // Direction toward the center
            float driftMagnitude = 20f;//(float)(random.NextDouble() * 50); // Random magnitude of drift
            Vector2 drift = driftDirection * driftMagnitude; // Apply drift magnitude towards center

            // Step 3: Start Point Calculation
            float startDistance = 250f; // (float)(random.NextDouble() * 300 + 200); // Distance behind player
            float angleAway = (float)Math.Atan2(-driftDirection.Y, -driftDirection.X); // Reverse drift direction
            Vector2 startPosition = new Vector2(
                destinationPoint.X + (float)Math.Cos(angleAway) * startDistance,
                destinationPoint.Y + (float)Math.Sin(angleAway) * startDistance
            );

            // Step 4: Slight Drift Towards Center     
            startPosition += drift;

            // Step 5: Particle Properties
            Vector2 direction = Vector2.Normalize(destinationPoint - startPosition);
            float speed = (float)(random.NextDouble() * 300 + 100);
            float lifetime = 1.3f;//(float)(random.NextDouble() * 1.5f + 0.5f);
            Color particleColor = color ?? new Color(random.Next(200, 256), random.Next(100, 200), random.Next(0, 100));
            float scale = 0.7f;//(float)(random.NextDouble() * 0.5f + 0.2f);

            Color actualParticleColor = color ?? Color.Lerp(Color.Gray, Color.White, 1 - (startDistance / 1024));

            var particle = new Particle(
                startPosition,
                direction * speed,
                lifetime,
                actualParticleColor,
                scale,
                ParticleRenderType.LineList,
                tailLength: 10
            );

            particles.Add(particle);

            // Create a rain drop ripple effect 
            particle.OnDeath += EmitRainDropRipple;
        }

        /*const float maxDistance = 1280f;
        //Vector2 vanishingPoint,
        float targetAreaRadius = 100;
        for (int i = 0; i < numberOfParticles; i++)
        {
            // Randomly generate a start position around the screen
            float angle = (float)(random.NextDouble() * MathHelper.TwoPi);
            float distance = random.Next((int)maxDistance);
            Vector2 startPosition = new Vector2(
                emitPosition.X + (float)Math.Cos(angle) * distance,
                emitPosition.Y + (float)Math.Sin(angle) * distance
            );

            // Calculate a target point within a radius around the vanishing point
            float targetAngle = (float)(random.NextDouble() * MathHelper.TwoPi);
            float targetDistance = (float)random.NextDouble() * targetAreaRadius;
            Vector2 targetPoint = new Vector2(
                emitPosition.X + (float)Math.Cos(targetAngle) * targetDistance,
                emitPosition.Y + (float)Math.Sin(targetAngle) * targetDistance
            );

            // Calculate direction towards the target point
            Vector2 direction = targetPoint - startPosition;
            direction.Normalize();

            float speed = MathHelper.Lerp(200, 800, (float)random.NextDouble());

            // Calculate the velocity to move towards the target point
            Vector2 velocity = direction * speed;

            // Lifetime depends on the distance to the target point
            float lifetime = Vector2.Distance(startPosition, targetPoint) / speed;

            // Scale particles based on distance from start (maintaining original perspective)
            float scale = MathHelper.Lerp(0.2f, 1.0f, 1 - (distance / maxDistance));

            Color actualParticleColor = color ?? Color.Lerp(Color.Gray, Color.White, 1 - (distance / maxDistance));

            var particle = new Particle(
                startPosition,
                velocity,
                lifetime,
                actualParticleColor,
                scale,
                ParticleRenderType.LineList,
                tailLength: scale * 0.5f
            );

            particles.Add(particle);

            // Create a rain drop ripple effect 
            particle.OnDeath += EmitRainDropRipple;
        }*/
    }

    // Emit particles for Confetti effect
    private void EmitConfetti(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            Vector2 velocity = new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble())) * 100;
            float lifetime = (float)random.NextDouble() * 3f + 1f;

            Color actualParticalColor;
            if (color.HasValue)
            {
                actualParticalColor = color.Value;
            }
            else
            {
                actualParticalColor = new Color(random.Next(256), random.Next(256), random.Next(256)); // Bright colors for confetti
            }
            float scale = (float)random.NextDouble() * 0.3f + 0.3f;

            particles.Add(new Particle(emitPosition, velocity, lifetime, actualParticalColor, scale));
        }
    }

    private void EmitLineExplosions(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            // Calculate velocity with more explosive characteristics
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = (float)(random.NextDouble() * 200 + 100);
            Vector2 velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed
            );

            float lifetime = (float)random.NextDouble() * 1.5f + 0.5f;

            Color actualParticleColor = color ?? new Color(
                random.Next(200, 256),  // High red
                random.Next(100, 200),  // Medium green
                random.Next(0, 100)     // Low blue
            );

            float scale = (float)random.NextDouble() * 0.4f + 0.2f;

            var particle = new Particle
                (
                    emitPosition,
                    velocity,
                    lifetime,
                    actualParticleColor,
                    scale,
                    ParticleRenderType.LineList,
                    (float)random.Next(10) * 0.5f + 0.5f // Trail length for the explosion
                );
            particles.Add(particle);
        }
    }

    // Emit particles for Fireworks effect
    private void EmitFireworks(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            Vector2 velocity = new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1)) * 150;
            float lifetime = (float)random.NextDouble() * 2f + 1f;

            Color actualParticalColor;
            if (color.HasValue)
            {
                actualParticalColor = color.Value;
            }
            else
            {
                actualParticalColor = new Color(random.Next(256), random.Next(256), random.Next(256)); // Random colors for fireworks
            }
            float scale = (float)random.NextDouble() * 0.5f + 0.5f;

            var particle = new Particle(emitPosition, velocity, lifetime, actualParticalColor, scale);
            particle.OnDeath += FireworksParticle_OnDeath;

            particles.Add(particle);
        }
    }

    private void FireworksParticle_OnDeath(Vector2 particlePosition)
    {
        EmitLineExplosions(10, particlePosition);
    }

    VectorPolygon dropRipple = VectorPolygon.CreateCircle(Vector2.Zero, 10f, 16);

    /* TODO  Not needed for now private void EmitRainDropRipples(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            EmitRainDropRipple(new Vector2(random.Next(1280), random.Next(720)));
        }
    }*/

    private void EmitRainDropRipple(Vector2 rainDropPosition)
    {
        var particle = new Particle
                (
                    rainDropPosition,
                    new Vector2(0.0f, 0.0f),
                    2.0f,
                    Color.WhiteSmoke,
                    1.5f,
                    ParticleRenderType.EmptyPolygon,
                    10,
                    dropRipple
                );

        // Configure per-particle scaling & easing for ripple:
        // Start tiny, grow to double previous radius (0.5 vs legacy 0.25) with ease-out and fade tied to scale.
        particle.StartScale = 0.05f;
        particle.EndScale = 0.5f; // doubled final radius
        particle.ScaleEasing = ScaleEasingMode.EaseOutQuad;
        particle.FadeWithScale = true;

        particles.Add(particle);
    }

    // Emit particles for Sparkles effect
    private void EmitSparkles(int numberOfParticles, Vector2 emitPosition, Color? color = null)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            Vector2 velocity = new Vector2((float)(random.NextDouble() * 2 - 1), (float)(random.NextDouble() * 2 - 1)) * 50;
            float lifetime = (float)random.NextDouble() * 1f + 0.5f;

            Color actualParticleColor = color ?? Color.White * ((float)random.NextDouble() * 0.5f + 0.5f); // Light sparkly effect

            float scale = (float)random.NextDouble() * 0.2f + 0.2f;

            particles.Add(new Particle(emitPosition, velocity, lifetime, actualParticleColor, scale));
        }
    }

    // Example of a new effect using polygons
    private void EmitPolygonExplosions(int numberOfParticles, Vector2 emitPosition, Color? color)
    {
        for (int i = 0; i < numberOfParticles; i++)
        {
            float angle = (float)(random.NextDouble() * Math.PI * 2);
            float speed = (float)(random.NextDouble() * 150 + 50);
            Vector2 velocity = new Vector2(
                (float)Math.Cos(angle) * speed,
                (float)Math.Sin(angle) * speed
            );

            float lifetime = (float)random.NextDouble() * 2f + 1f;
            Color actualParticleColor = color ?? new Color(
                random.Next(200, 256),
                random.Next(100, 200),
                random.Next(0, 100)
            );

            // Create a simple triangle polygon
            Vector2[] points = new Vector2[]
            {
                new Vector2(-5, -5),
                new Vector2(5, -5),
                new Vector2(0, 5)
            };

            var polygon = new VectorPolygon(points);

            particles.Add(new Particle
                (
                    emitPosition,
                    velocity,
                    lifetime,
                    actualParticleColor,
                    (float)random.NextDouble() * 0.4f + 0.2f,
                    ParticleRenderType.FilledPolygon,
                    (float)random.NextDouble() * 0.5f + 0.5f, // Trail length for the explosion
                    polygon
                )
           );
        }
    }

    /// <summary>
    /// Update the particle manager.
    /// </summary>
    /// <param name="gameTime">The amount of elapsed time, in seconds.</param>
    public void Update(GameTime gameTime)
    {
        var probe = AllocProbe.Begin();
        UpdateParticles(gameTime);
        AllocProbe.End(typeof(ParticleManager), "Update", probe);
    }

    private void UpdateParticles(GameTime gameTime)
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            particles[i].Update(gameTime);

            if (!particles[i].IsAlive)
            {
                particles.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// Controls the "density" of the tail
    /// Dense Tail (t += 1f): A continuous, almost solid-looking trail.Ideal for effects like glowing streaks.
	/// Sparse Tail (t += 10f): A dotted, fragmented appearance.Useful for effects like spark trails or light debris.
    /// </summary>
    const float tailDensity = 5f;

    public void Draw(GameTime gameTime)
    {
        var probe = AllocProbe.Begin();
        DrawParticles(gameTime);
        AllocProbe.End(typeof(ParticleManager), "Draw", probe);
    }

    private void DrawParticles(GameTime gameTime)
    {
        // Clear down our lists
        this.emptyPolygonParticles.Clear();
        this.filledPolygonParticles.Clear();
        this.lineParticles.Clear();
        this.textureParticles.Clear();

        // Group particles by their type to avoid context twitching
        foreach (var particle in particles)
        {
            if (!particle.IsAlive)
                continue;

            switch (particle.Type)
            {
                case ParticleRenderType.EmptyPolygon:
                    emptyPolygonParticles.Add(particle);
                    break;

                case ParticleRenderType.FilledPolygon:
                    filledPolygonParticles.Add(particle);
                    break;

                case ParticleRenderType.LineList:
                    lineParticles.Add(particle);
                    break;

                case ParticleRenderType.Texture:
                    textureParticles.Add(particle);
                    break;
            }
        }

        // Unified geometry pass: filled polygons first (triangles), then lines & empty polygons (lines) via End ordering.
        bool hasGeometry = filledPolygonParticles.Count > 0 || lineParticles.Count > 0 || emptyPolygonParticles.Count > 0;
        if (hasGeometry)
        {
            lineBatch.Begin(Transform ?? context.GlobalTransformation);

            // 1. Filled polygons (enqueued into triangle buffer)
            // BasePolygon keeps its raw points; Transform rewrites only the transformed copy each frame, so nothing accumulates.
            // Per-particle scaling with easing.
            if (filledPolygonParticles.Count > 0)
            {
                foreach (var particle in filledPolygonParticles)
                {
                    if (particle.BasePolygon != null && particle.LifeTime > 0f && particle.OriginalLifeTime > 0f)
                    {
                        float rawProgress = 1f - MathHelper.Clamp(particle.LifeTime / particle.OriginalLifeTime, 0f, 1f); // 0 -> 1

                        // Resolve start/end scale (fallback to legacy constants if unset)
                        float startScale = float.IsNaN(particle.StartScale) ? 0.05f : particle.StartScale;
                        float endScale = float.IsNaN(particle.EndScale) ? 0.25f : particle.EndScale;

                        // Easing
                        float easedProgress = rawProgress;
                        switch (particle.ScaleEasing)
                        {
                            case ScaleEasingMode.EaseInQuad:
                                easedProgress = rawProgress * rawProgress;
                                break;
                            case ScaleEasingMode.EaseOutQuad:
                                easedProgress = 1f - (1f - rawProgress) * (1f - rawProgress); // ease-out quad
                                break;
                            case ScaleEasingMode.Linear:
                            default:
                                easedProgress = rawProgress;
                                break;
                        }

                        float scale = MathHelper.Lerp(startScale, endScale, easedProgress);

                        // Optional alpha driven by scale growth (Ripple style)
                        if (particle.FadeWithScale)
                        {
                            float alphaFrac = 1f - easedProgress; // fade out as scale grows
                            particle.Color.A = (byte)(particle.PeakAlpha * MathHelper.Clamp(alphaFrac, 0f, 1f));
                        }

                        // Transform only rewrites the transformed points from the raw ones, so there is no cumulative mutation and no need to clone
                        var working = particle.BasePolygon;
                        Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(particle.Position.X, particle.Position.Y, 0f);
                        working.Transform(world);
                        lineBatch.DrawFilledPolygon(working, particle.Color);
                    }
                }
            }

            // 2. Line trails (line list)
            if (lineParticles.Count > 0)
            {
                foreach (var particle in lineParticles)
                {
                    int thickness = particle.LineWidth > 0 ? particle.LineWidth : (int)(particle.MinScale * 5);
                    if (thickness < 1) thickness = 1;
                    var tail = particle.PreviousPosition;
                    if (!float.IsNaN(particle.StreakLength) && particle.CurrentVelocity.LengthSquared() > 1e-4f)
                        tail = particle.Position - Vector2.Normalize(particle.CurrentVelocity) * particle.StreakLength;
                    lineBatch.DrawLineList(
                        particle.Position,
                        tail,
                        particle.Color,
                        thickness
                    );
                }
            }

            // 3. Empty polygon outlines (line list)
            // Same scaling approach as filled polygons so outlines match fill growth visually.
            if (emptyPolygonParticles.Count > 0)
            {
                foreach (var particle in emptyPolygonParticles)
                {
                    if (particle.BasePolygon != null && particle.LifeTime > 0f && particle.OriginalLifeTime > 0f)
                    {
                        float rawProgress = 1f - MathHelper.Clamp(particle.LifeTime / particle.OriginalLifeTime, 0f, 1f);
                        float startScale = float.IsNaN(particle.StartScale) ? 0.05f : particle.StartScale;
                        float endScale = float.IsNaN(particle.EndScale) ? 0.25f : particle.EndScale;

                        float easedProgress = rawProgress;
                        switch (particle.ScaleEasing)
                        {
                            case ScaleEasingMode.EaseInQuad:
                                easedProgress = rawProgress * rawProgress;
                                break;
                            case ScaleEasingMode.EaseOutQuad:
                                easedProgress = 1f - (1f - rawProgress) * (1f - rawProgress);
                                break;
                            case ScaleEasingMode.Linear:
                            default:
                                easedProgress = rawProgress;
                                break;
                        }

                        float scale = MathHelper.Lerp(startScale, endScale, easedProgress);

                        if (particle.FadeWithScale)
                        {
                            float alphaFrac = 1f - easedProgress;
                            particle.Color.A = (byte)(255f * MathHelper.Clamp(alphaFrac, 0f, 1f));
                        }

                        var working = particle.BasePolygon;
                        Matrix world = Matrix.CreateScale(scale) * Matrix.CreateTranslation(particle.Position.X, particle.Position.Y, 0f);
                        working.Transform(world);
                        lineBatch.DrawEmptyPolygon(working, particle.Color);
                    }
                }
            }

            lineBatch.End();
        }

        // Draw texture particles if you have a SpriteBatch
        if (textureParticles.Count > 0 && texture != null)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, Transform ?? context.GlobalTransformation);

            foreach (var particle in textureParticles)
            {
                // Calculate the direction and length of the tail
                Vector2 tailDirection = particle.Position - particle.PreviousPosition;
                float tailLength = particle.TailLength * tailDirection.Length();

                // Normalize the tail direction
                if (tailDirection != Vector2.Zero)
                    tailDirection.Normalize();

                // Draw the this particle's texture
                spriteBatch.Draw(
                    texture,
                    particle.Position,
                    null,
                    particle.Color,
                    0.0f,
                    textureOrigin,
                    particle.MinScale,
                    SpriteEffects.None,
                    0f);

                // Draw the particle's tail
                for (float t = 0; t < tailLength; t += tailDensity)
                {
                    Vector2 tailPosition = particle.Position - tailDirection * t;

                    // Fade out the tail
                    float alpha = MathHelper.Clamp(1f - (t / tailLength), 0f, 1f);
                    Color tailColor = particle.Color * alpha;

                    spriteBatch.Draw(
                        texture,
                        tailPosition,
                        null,
                        tailColor,
                        0f,
                        textureOrigin,
                        particle.MinScale * 0.8f, // Shrink the tail particle slightly
                        SpriteEffects.None,
                        0f);
                }
            }

            spriteBatch.End();
        }
    }
}