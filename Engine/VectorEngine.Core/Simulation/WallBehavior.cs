namespace VectorEngine.Simulation
{
    /// <summary>How an actor meets the walls of the world.</summary>
    public enum WallBehavior
    {
        /// <summary>The actor bounces off the wall.</summary>
        Bounce,

        /// <summary>The actor is not stopped; it touches the world's <see cref="WorldBase.WallActor"/> where it crosses a wall.</summary>
        Touch,
    }
}
