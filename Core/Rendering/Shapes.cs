using System;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// Vector Rumble's own polygon shapes (the engine's VectorPolygon only ships the generic ones).
    /// </summary>
    static class Shapes
    {
        private static readonly Random random = new Random();

        /// <summary>
        /// Create a polygon shaped like a rocket.
        /// </summary>
        /// <returns>A new VectorPolygon object in the shape of a rocket.</returns>
        public static VectorPolygon CreateRocket()
        {
            Vector2[] points = new Vector2[4];
            points[0] = new Vector2(0, -6);
            points[1] = new Vector2(6, 0);
            points[2] = new Vector2(0, 10);
            points[3] = new Vector2(-6, 0);

            return new VectorPolygon(points);
        }


        /// <summary>
        /// Create a polygon shaped like a mine.
        /// </summary>
        /// <returns>A new VectorPolygon object in the shape of a mine.</returns>
        public static VectorPolygon CreateMine()
        {
            Vector2[] points = new Vector2[12];

            points[0] = new Vector2(0, -32);
            points[1] = new Vector2(16, -16);
            points[2] = new Vector2(32, 0);
            points[3] = new Vector2(16, 16);
            points[4] = new Vector2(0, 32);
            points[5] = new Vector2(-16, 16);
            points[6] = new Vector2(-32, 0);
            points[7] = new Vector2(-16, -16);
            points[8] = new Vector2(16, -16);
            points[9] = new Vector2(16, 16);
            points[10] = new Vector2(-16, 16);
            points[11] = new Vector2(-16, -16);

            Matrix scale = Matrix.CreateScale(0.33f);

            Vector2.Transform(points, ref scale, points);

            return new VectorPolygon(points);
        }


        /// <summary>
        /// Create a polygon shaped like an asteroid.
        /// </summary>
        /// <param name="radius">The radius of the asteroid.</param>
        /// <returns>A new VectorPolygon object in the shape of an asteroid.</returns>
        public static VectorPolygon CreateAsteroid(float radius)
        {
            return CreateAsteroid(radius, random);
        }

        /// <summary>
        /// Creates an asteroid shape using the given random numbers, so that every machine in
        /// a network match can make the same one.
        /// </summary>
        public static VectorPolygon CreateAsteroid(float radius, Random shapeRandom)
        {
            VectorPolygon polygon = VectorPolygon.CreateCircle(Vector2.Zero, radius, 12);
            for (int i = 0; i < polygon.Points.Length; ++i)
            {
                Vector2 normal = Vector2.Normalize(polygon.Points[i]);
                polygon.Points[i] += normal * ((radius * 0.2f) * 
                    (float)shapeRandom.NextDouble() - (radius * 0.1f));
            }

            return polygon;
        }
    }
}
