namespace VectorEngine.Simulation
{
    /// <summary>
    /// An actor that stands in for the world, in wall collisions, etc.
    /// </summary>
    public class WorldActor : Actor
    {
        /// <summary>
        /// Construct a new world actor.
        /// </summary>
        /// <param name="world">The world that this world-actor belongs to.</param>
        public WorldActor(WorldBase world)
            : base(world)
        {
            radius = 0f;
            mass = 100f;
        }
    }
}
