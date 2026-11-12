using Microsoft.Xna.Framework;
using VectorEngine.Simulation;
using Xunit;

namespace VectorEngine.Tests;

public class CollisionTests
{
    [Fact]
    public void Crossing_lines_intersect_at_the_crossing_point()
    {
        Assert.True(Collision.LineLineIntersect(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0), out var p));
        Assert.Equal(5f, p.X, 3);
        Assert.Equal(5f, p.Y, 3);
    }

    [Fact]
    public void Parallel_lines_do_not_intersect() =>
        Assert.False(Collision.LineLineIntersect(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 1), new Vector2(10, 1), out _));

    [Fact]
    public void Overlapping_circles_intersect_and_distant_ones_do_not()
    {
        Assert.True(Collision.CircleCircleIntersect(new Vector2(0, 0), 5, new Vector2(8, 0), 5));
        Assert.False(Collision.CircleCircleIntersect(new Vector2(0, 0), 5, new Vector2(11, 0), 5));
    }

    [Fact]
    public void Circle_touching_a_wall_collides_with_the_wall_normal()
    {
        var r = new Collision.CircleLineCollisionResult();
        Assert.True(Collision.CircleLineCollide(new Vector2(5, 3), 5, new Vector2(0, 0), new Vector2(10, 0), ref r));
        Assert.True(r.Collision);
        Assert.Equal(0f, r.Normal.X, 3);
        Assert.Equal(1f, r.Normal.Y, 3);
    }
}
