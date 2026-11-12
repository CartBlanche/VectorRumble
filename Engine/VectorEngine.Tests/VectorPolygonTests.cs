using Microsoft.Xna.Framework;
using VectorEngine.Rendering;
using Xunit;

namespace VectorEngine.Tests;

public class VectorPolygonTests
{
    [Fact]
    public void Transform_moves_transformed_points_but_not_model_points()
    {
        var p = new VectorPolygon(new[] { new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10) });
        p.Transform(Matrix.CreateTranslation(5, 7, 0));
        Assert.Equal(new Vector2(5, 7), p.TransformedPoints[0]);
        Assert.Equal(new Vector2(15, 17), p.TransformedPoints[2]);
        Assert.Equal(new Vector2(0, 0), p.Points[0]);
    }

    [Fact]
    public void CreateCircle_makes_the_requested_segments_on_the_radius()
    {
        var c = VectorPolygon.CreateCircle(Vector2.Zero, 10, 12);
        Assert.Equal(12, c.Points.Length);
        foreach (var pt in c.Points) Assert.Equal(10f, pt.Length(), 2);
    }

    [Fact]
    public void Children_follow_the_parent_transform()
    {
        var parent = new VectorPolygon(new[] { new Vector2(0, 0), new Vector2(1, 0) });
        var child = new VectorPolygon(new[] { new Vector2(2, 0), new Vector2(3, 0) });
        parent.AddChild(child);
        parent.Transform(Matrix.CreateTranslation(0, 4, 0));
        Assert.Equal(new Vector2(2, 4), child.TransformedPoints[0]);
    }
}
