using Microsoft.Xna.Framework;

namespace VectorEngine.Simulation
{
    /// <summary>
    /// The result of a collision query
    /// </summary>
    public struct CollisionResult
    {
        /// <summary>
        /// How far away did the collision occur down the ray
        /// </summary>
        public float Distance;

        /// <summary>
        /// The collision "direction"
        /// </summary>
        public Vector2 Normal;

        /// <summary>
        /// What caused the collison (what the source ran into)
        /// </summary>
        public Actor Actor;

        public static int Compare(CollisionResult a, CollisionResult b)
        {
            return a.Distance.CompareTo(b.Distance);
        }
    }
}
