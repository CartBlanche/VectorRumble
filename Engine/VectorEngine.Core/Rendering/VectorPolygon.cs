using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace VectorEngine.Rendering;

/// <summary>
/// A series of points that may be drawn together to form a line.
/// </summary>
public partial class VectorPolygon
{
    /// <summary>
    /// The raw set of points, in "model space".
    /// </summary>
    private Vector2[] points;
    [ContentSerializer]
    public Vector2[] Points
    {
        get { return points; }
        set
        {
            if (points != value)
            {
                points = value;
                transformedPoints = (Vector2[])points.Clone();
            }
        }
    }

    /// <summary>
    /// The transformed points, typically in "world" space
    /// </summary>
    private Vector2[] transformedPoints;
    public Vector2[] TransformedPoints
    {
        get { return transformedPoints; }
    }


    /// <summary>
    /// Constructs a new VectorPolygon object.
    /// </summary>
    public VectorPolygon()
    {
    }

    /// <summary>
    /// Constructs a new VectorPolygon object from the given points.
    /// </summary>
    /// <param name="points">The raw set of points.</param>
    public VectorPolygon(Vector2[] points)
    {
        this.points = points;
        this.transformedPoints = (Vector2[])points.Clone();
    }

    /// <summary>
    /// The parent polygon of this polygon, if any.
    /// </summary>
    public VectorPolygon Parent { get; private set; }

    /// <summary>
    /// The child polygons of this polygon.
    /// </summary>
    // (made on the first child: most polygons have none, and every Clone() made an empty list)
    private List<VectorPolygon> children;
    public IReadOnlyList<VectorPolygon> Children => children ?? (IReadOnlyList<VectorPolygon>)Array.Empty<VectorPolygon>();

    /// <summary>
    /// Adds a child polygon to this polygon.
    /// </summary>
    /// <param name="child">The child polygon to add.</param>
    public void AddChild(VectorPolygon child)
    {
        if (child == null)
            throw new ArgumentNullException(nameof(child));

        if (child.Parent != null)
            throw new InvalidOperationException("Child already has a parent.");

        (children ??= new List<VectorPolygon>()).Add(child);
        child.Parent = this;
    }

    /// <summary>
    /// Removes a child polygon from this polygon.
    /// </summary>
    /// <param name="child">The child polygon to remove.</param>
    public void RemoveChild(VectorPolygon child)
    {
        if (children != null && children.Remove(child))
        {
            child.Parent = null;
        }
    }

    /// <summary>
    /// Recursively transforms this polygon and all its children.
    /// </summary>
    /// <param name="matrix">The transformation matrix to apply.</param>
    public void Transform(Matrix matrix)
    {
        // Transform this polygon
        Vector2.Transform(points, 0, ref matrix, transformedPoints, 0, points.Length);

        // Recursively transform children
        if (children != null)
        {
            for (int i = 0; i < children.Count; i++)
                children[i].Transform(matrix);
        }
    }

    /// <summary>
    /// Calculates the global (world-space) transformation of this polygon, considering its parents.
    /// </summary>
    /// <returns>The combined transformation matrix.</returns>
    public Matrix GetGlobalTransform()
    {
        if (Parent == null)
            return Matrix.Identity;

        return Parent.GetGlobalTransform() * Parent.GetLocalTransform();
    }

    /// <summary>
    /// Returns the local transformation of this polygon relative to its parent.
    /// </summary>
    /// <returns>The local transformation matrix.</returns>
    private Matrix GetLocalTransform()
    {
        // Example: you can store and use position/rotation/scale properties for local transforms
        return Matrix.Identity; // Replace with actual logic if needed
    }

    /// <summary>
    /// Scale the raw points by the scaleFactor given.
    /// </summary>
    /// <param name="scaleFactor">The scaleFactor to scale the points by.</param>
    /// <param name="customCentre">The centre around which we're scaling</param>
    public void Scale(float scaleFactor, Vector2? customCentre = null)
    {
        if (customCentre.HasValue)
        {
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = customCentre.Value + (points[i] - customCentre.Value) * scaleFactor;
            }
        }
        else
        {
            var matrix = Matrix.CreateScale(scaleFactor);
            Transform(matrix);
        }
    }

    /// <summary>
    /// Translates the raw points by the position given.
    /// </summary>
    /// <param name="position">The translation vector to move the polygon by.</param>
    public void Translate(Vector2 position)
    {
        var matrix = Matrix.CreateTranslation(position.X, position.Y, 0);

        Transform(matrix);
    }

    /// <summary>
    /// Rotates the polygon around the origin by the given angle in radians.
    /// </summary>
    /// <param name="angle">The angle in radians to rotate the polygon by.</param>
    public void Rotate(float angle)
    {
        for (int i = 0; i < points.Length; i++)
        {
            float x = points[i].X * (float)Math.Cos(angle) - points[i].Y * (float)Math.Sin(angle);
            float y = points[i].X * (float)Math.Sin(angle) + points[i].Y * (float)Math.Cos(angle);
            points[i] = new Vector2(x, y);
        }

        // Update transformed points
        transformedPoints = (Vector2[])points.Clone();
    }

    /* OVERKILL public void Rotate(float angle)
    {
        // Create a quaternion representing the rotation (might be overkill, but want the interpolation)
        Quaternion rotationQuaternion = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, angle);

        // Convert 2D points to 3D points
        Vector3[] points3D = points.Select(p => new Vector3(p.X, p.Y, 0)).ToArray();

        // Apply the rotation to each point
        for (int i = 0; i < points3D.Length; i++)
        {
            points3D[i] = Vector3.Transform(points3D[i], rotationQuaternion);
        }

        // Convert 3D points back to 2D points
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = new Vector2(points3D[i].X, points3D[i].Y);
        }

        // Update the transformed points
        transformedPoints = (Vector2[])points.Clone();
    }*/

    /// <summary>
    /// Creates a deep copy of the current VectorPolygon.
    /// </summary>
    /// <returns>A new VectorPolygon with the same points.</returns>
    public VectorPolygon Clone()
    {
        // Create a new instance with copied points
        return new VectorPolygon((Vector2[])points.Clone());
    }

    private static Random random = new Random();


    /// <summary>
    /// Creates a polygon in the shape of a circle.
    /// </summary>
    /// <param name="center">The offset of the center of the circle.</param>
    /// <param name="radius">The radius of the circle.</param>
    /// <param name="segments">The number of segments used in the circle.</param>
    /// <returns>A new VectorPolygon object in the shape of a circle.</returns>
    public static VectorPolygon CreateCircle(Vector2 center, float radius, int segments)
    {
        Vector2[] points = new Vector2[segments];
        float angle = MathHelper.TwoPi / points.Length;

        for (int i = 0; i <= points.Length - 1; i++)
        {
            points[i] = new Vector2(
                center.X + radius * (float)Math.Round(Math.Sin(angle * i), 4),
                center.Y + radius * (float)Math.Round(Math.Cos(angle * i), 4));
        }

        return new VectorPolygon(points);
    }


    /// <summary>
    /// Create a polygon shaped like a pointer.
    /// </summary>
    /// <param name="center">The offset of the center of the circle.</param>
    /// <param name="size">The size of the pointer.</param>
    /// <returns>A new VectorPolygon object in the shape of a point.</returns>
    public static VectorPolygon CreatePointer(Vector2 center, float size)
    {
        // Define a triangle-like shape resembling a pointer
        Vector2[] points = new Vector2[3];

        // Top (pointer tip)
        points[0] = new Vector2(center.X, center.Y - size);

        // Bottom-right (base of the pointer)
        points[1] = new Vector2(center.X + size * 0.5f, center.Y + size);

        // Bottom-left (base of the pointer)
        points[2] = new Vector2(center.X - size * 0.5f, center.Y + size);

        return new VectorPolygon(points);
    }

    /// <summary>
    /// Creates a polygon in the shape of a rectangle.
    /// </summary>
    /// <param name="topLeft">The Top Left of the rectangle.</param>
    /// <param name="bottomRight">The Bottom Right of the rectangle.</param>
    /// <returns>A new VectorPolygon object in the shape of a rectangle.</returns>
    public static VectorPolygon CreateRectangle (Vector2 topLeft, Vector2 bottomRight)
    {
        Vector2[] points = new Vector2[4];

        points[0] = topLeft;
        points[1] = new Vector2(bottomRight.X, 0);
        points[2] = bottomRight;
        points[3] = new Vector2 (0, bottomRight.Y);

        Matrix scale = Matrix.CreateScale (1.0f);

        Vector2.Transform (points, ref scale, points);

        return new VectorPolygon (points);
    }
}