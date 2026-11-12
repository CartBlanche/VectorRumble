using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace VectorEngine.Rendering;

/// <summary>
/// Batches line "draw" calls from the game, and renders them at one time.
/// </summary>
public class LineBatch
{
    /// <summary>
    /// The default maximum number of vertices in each LineBatch vertex array. A full buffer is flushed in
    /// one draw call, so this is sized to hold a busy frame in a single call; a small one splits a frame
    /// into many, which is costly on mobile GPUs.
    /// </summary>
    public const int DefaultMaxVertexCount = 8192;

    readonly int maxVertexCount;

    /// <summary>
    /// The screen this batch draws to (virtual size and window transform), if the game supplied one.
    /// </summary>
    public IRenderContext Context { get; }

    /// <summary>
    /// The graphics device that renders the lines.
    /// </summary>
    public GraphicsDevice GraphicsDevice { get; }

    /// <summary>
    /// The effect applied to the lines.
    /// </summary>
    BasicEffect effect;

    /// <summary>
    /// The vertex declaration which is defines the line vertices.
    /// </summary>
    VertexDeclaration vertexDeclaration;

    /// <summary>
    /// Separate vertex buffers for lines and triangles so they do not interfere.
    /// </summary>
    VertexPositionColor[] lineVertices;
    VertexPositionColor[] triangleVertices;

    int lineVertexIndex;      // raw vertex cursor for line list
    int linePrimitiveCount;   // number of line primitives (each uses 2 vertices)

    int triVertexIndex;       // raw vertex cursor for triangle list
    int trianglePrimitiveCount; // number of triangle primitives (each uses 3 vertices)

    ///
    public static BlendState LineBlendState = new BlendState()
    {
        AlphaBlendFunction = BlendFunction.Add,
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.InverseSourceAlpha,
    };

    /// <param name="graphicsDevice">The device to draw with.</param>
    /// <param name="context">Optional screen description, exposed as <see cref="Context"/> for code that draws with this batch.</param>
    /// <param name="maxVertexCount">Size of each vertex array (lines and triangles are buffered separately).</param>
    public LineBatch(GraphicsDevice graphicsDevice, IRenderContext context = null, int maxVertexCount = DefaultMaxVertexCount)
    {
        this.Context = context;
        this.maxVertexCount = maxVertexCount;
        this.GraphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));

        // create and configure the effect
        this.effect = new BasicEffect(GraphicsDevice);
        this.effect.VertexColorEnabled = true;
        this.effect.TextureEnabled = false;
        this.effect.LightingEnabled = false;

        // configure the effect
        this.effect.World = Matrix.Identity;
        this.effect.View = Matrix.CreateLookAt(Vector3.Zero, Vector3.Forward, Vector3.Up);

        // update the projection in the line-batch
        SetProjection(Matrix.CreateOrthographicOffCenter(0.0f,
            GraphicsDevice.Viewport.Width,
            GraphicsDevice.Viewport.Height, 0.0f, 0.0f, 1.0f));

        var vd = VertexPositionColor.VertexDeclaration.GetVertexElements();
        // create the vertex declaration
        this.vertexDeclaration = new VertexDeclaration(vd);

        // create the vertex arrays
        this.lineVertices = new VertexPositionColor[maxVertexCount];
        this.triangleVertices = new VertexPositionColor[maxVertexCount];
    }

    /// <summary>
    /// Set the new projection for the line batch effect.
    /// </summary>
    /// <param name="projection"></param>
    public void SetProjection(Matrix projection)
    {
        if (effect != null)
        {
            effect.Projection = projection;
        }
    }

    /// <summary>
    /// Configures the object and the device to begin drawing lines.
    /// </summary>
    public void Begin()
    {
        Begin(Matrix.Identity);
    }

    /// <summary>
    /// Configures the object and the device to begin drawing lines.
    /// </summary>
    public void Begin(Matrix worldTransformation)
    {
        // reset counters
        lineVertexIndex = 0;
        linePrimitiveCount = 0;
        triVertexIndex = 0;
        trianglePrimitiveCount = 0;

        effect.World = worldTransformation;
    }

    /// <summary>
    /// Draw a line from one point to another with the same color.
    /// </summary>
    /// <param name="start">The starting point.</param>
    /// <param name="end">The ending point.</param>
    /// <param name="color">The color throughout the line.</param>
    /// <param name="thinkness">The thickness of the line. defaults to 1</param>
    public void DrawLineList(Vector2 start, Vector2 end, Color color, int thinkness = 1)
    {
        if (thinkness < 1)
            throw new ArgumentOutOfRangeException(nameof(thinkness), "Cannot be less than 1");

        if (thinkness == 1)
        {
            DrawLineList(
                new VertexPositionColor(new Vector3(start, 0f), color),
                new VertexPositionColor(new Vector3(end, 0f), color));
        }
        else
        {
            for (int i = 0; i < thinkness; i++)
            {
                DrawLineList(
                new VertexPositionColor(new Vector3(start, 0f), color),
                new VertexPositionColor(new Vector3(end, 0f), color));
                start.Y -= 1;
                end.Y -= 1;
            }

        }
    }


    /// <summary>
    /// Draw a line from one point to another with different colors at each end.
    /// </summary>
    /// <param name="start">The starting point.</param>
    /// <param name="end">The ending point.</param>
    /// <param name="startColor">The color at the starting point.</param>
    /// <param name="endColor">The color at the ending point.</param>
    public void DrawLineList(Vector2 start, Vector2 end,
        Color startColor, Color endColor)
    {
        DrawLineList(
            new VertexPositionColor(new Vector3(start, 0f), startColor),
            new VertexPositionColor(new Vector3(end, 0f), endColor));
    }


    /// <summary>
    /// Draws a line from one vertex to another.
    /// </summary>
    /// <param name="start">The starting vertex.</param>
    /// <param name="end">The ending vertex.</param>
    public void DrawLineList(VertexPositionColor start, VertexPositionColor end)
    {
        EnsureLineBufferSpace();

        lineVertices[lineVertexIndex++] = start;
        lineVertices[lineVertexIndex++] = end;
        linePrimitiveCount++;
    }


    /// <summary>
    /// Draws the given polygon.
    /// </summary>
    /// <param name="polygon">The polygon to render.</param>
    /// <param name="color">The color to use when drawing the polygon.</param>
    public void DrawEmptyPolygon(VectorPolygon polygon, Color color)
    {
        DrawEmptyPolygon(polygon, color, false);
    }


    /// <summary>
    /// Draws the given polygon.
    /// </summary>
    /// <param name="aPolygon">The polygon to render.</param>
    /// <param name="aColor">The color to use when drawing the polygon.</param>
    /// <param name="aDashed">If true, the polygon will be "dashed".</param>
    /// <param name="aDashLength">If aDashed, the polygon uses this length of dash.</param>
    public void DrawEmptyPolygon(VectorPolygon aPolygon, Color aColor, bool aDashed, int aDashLength = 2)
    {
        ArgumentNullException.ThrowIfNull(aPolygon);

        // (a polygon with fewer than two points has no edges to draw)
        if (aPolygon.TransformedPoints.Length < 2)
        {
            return;
        }

        int dashStep = aDashed ? aDashLength : 1;
        for (int i = 0; i < aPolygon.TransformedPoints.Length; i += dashStep)
        {
            EnsureLineBufferSpace();

            Vector2 start = aPolygon.TransformedPoints[i % aPolygon.TransformedPoints.Length];
            Vector2 end = aPolygon.TransformedPoints[(i + 1) % aPolygon.TransformedPoints.Length];

            SetVertexPosition(start, aColor);
            SetVertexPosition(end, aColor);

            linePrimitiveCount++;
        }
    }

    private void EnsureLineBufferSpace()
    {
        if (lineVertexIndex >= lineVertices.Length - 2)
        {
            // flush existing lines and reset only line counters (keep triangles untouched)
            InternalFlushLines();
        }
    }

    private void EnsureTriangleBufferSpace()
    {
        if (triVertexIndex >= triangleVertices.Length - 3)
        {
            InternalFlushTriangles();
        }
    }

    private void SetVertexPosition(Vector2 position, Color color)
    {
        lineVertices[lineVertexIndex].Position.X = position.X;
        lineVertices[lineVertexIndex].Position.Y = position.Y;
        lineVertices[lineVertexIndex++].Color = color;
    }

    // (legacy) const int numberOfVerticesPerPrimitive = 3; // no longer used with dual buffers

    /// <summary>
    /// Draws the given polygon.
    /// </summary>
    /// <param name="polygon">The polygon to render.</param>
    /// <param name="color">The color to use when drawing the polygon.</param>
    public void DrawFilledPolygon(VectorPolygon polygon, Color color)
    {
        ArgumentNullException.ThrowIfNull(polygon);

        if (polygon.TransformedPoints.Length < 3)
        {
            throw new ArgumentException("Polygon must have at least three points.", nameof(polygon));
        }
        // Triangle fan (assumes convex). For concave shapes, a triangulator is needed.
        for (int i = 1; i < polygon.TransformedPoints.Length - 1; i++)
        {
            EnsureTriangleBufferSpace();
            triangleVertices[triVertexIndex++] = new VertexPositionColor(new Vector3(polygon.TransformedPoints[0], 0f), color);
            triangleVertices[triVertexIndex++] = new VertexPositionColor(new Vector3(polygon.TransformedPoints[i], 0f), color);
            triangleVertices[triVertexIndex++] = new VertexPositionColor(new Vector3(polygon.TransformedPoints[i + 1], 0f), color);
            trianglePrimitiveCount++;
        }
    }

    /// <summary>
    /// Fills a slice of a ring (the area between two radii and two angles) with a strip of quads. The slice is not
    /// convex, so it cannot go through the triangle fan of <see cref="DrawFilledPolygon"/>.
    /// </summary>
    public void DrawFilledRingSector(Vector2 centre, float innerRadius, float outerRadius, float startAngle, float endAngle, Color color, int steps = 10)
    {
        for (int i = 0; i < steps; i++)
        {
            float a0 = MathHelper.Lerp(startAngle, endAngle, i / (float)steps);
            float a1 = MathHelper.Lerp(startAngle, endAngle, (i + 1) / (float)steps);
            var d0 = new Vector2((float)Math.Cos(a0), (float)Math.Sin(a0));
            var d1 = new Vector2((float)Math.Cos(a1), (float)Math.Sin(a1));
            Vector2 outer0 = centre + d0 * outerRadius, outer1 = centre + d1 * outerRadius;
            Vector2 inner0 = centre + d0 * innerRadius, inner1 = centre + d1 * innerRadius;

            AddTriangle(outer0, outer1, inner1, color);
            AddTriangle(outer0, inner1, inner0, color);
        }
    }

    private void AddTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        EnsureTriangleBufferSpace();
        triangleVertices[triVertexIndex++] = new VertexPositionColor(new Vector3(a, 0f), color);
        triangleVertices[triVertexIndex++] = new VertexPositionColor(new Vector3(b, 0f), color);
        triangleVertices[triVertexIndex++] = new VertexPositionColor(new Vector3(c, 0f), color);
        trianglePrimitiveCount++;
    }

    /// <summary>
    /// Draws the given polygon, in defined segments.
    /// </summary>
    /// <param name="aPolygon">The polygon to render.</param>
    /// <param name="aColor">The color to use when drawing the polygon.</param>
    /// <param name="aDashed">If true, the polygon will be "dashed".</param>
    /// <param name="aStartSeg">Start of segment drawing.</param>
    /// <param name="aEndSeg">End of segment drawing.</param>
    /// <param name="aDashLength">If aDashed, the polygon uses this length of dash.</param>
    public void DrawPolygonSegments(VectorPolygon aPolygon, Color aColor, bool aDashed, int aStartSeg, int aEndSeg, int aDashLength = 2)
    {
        if (aPolygon == null)
            throw new ArgumentNullException(nameof(aPolygon));

        if (aStartSeg < 0 || aEndSeg > aPolygon.TransformedPoints.Length || aStartSeg > aEndSeg)
            throw new ArgumentOutOfRangeException("Segment indices are out of range.");

        int dashStep = aDashed ? aDashLength : 1;
        for (int i = aStartSeg; i < aEndSeg; i += dashStep)
        {
            EnsureLineBufferSpace();

            Vector2 start = aPolygon.TransformedPoints[i % aPolygon.TransformedPoints.Length];
            Vector2 end = aPolygon.TransformedPoints[(i + 1) % aPolygon.TransformedPoints.Length];

            SetVertexPosition(start, aColor);
            SetVertexPosition(end, aColor);

            linePrimitiveCount++;
        }
    }

    /// <summary>Add ready-made triangles (three vertices each, already in the batch's pixel space).</summary>
    public void DrawTriangleVertices(VertexPositionColor[] vertices, int vertexCount)
    {
        for (int i = 0; i + 2 < vertexCount; i += 3)
        {
            EnsureTriangleBufferSpace();
            triangleVertices[triVertexIndex++] = vertices[i];
            triangleVertices[triVertexIndex++] = vertices[i + 1];
            triangleVertices[triVertexIndex++] = vertices[i + 2];
            trianglePrimitiveCount++;
        }
    }

    /// <summary>Add ready-made lines (two vertices each, already in the batch's pixel space).</summary>
    public void DrawLineVertices(VertexPositionColor[] vertices, int vertexCount)
    {
        for (int i = 0; i + 1 < vertexCount; i += 2)
        {
            EnsureLineBufferSpace();
            lineVertices[lineVertexIndex++] = vertices[i];
            lineVertices[lineVertexIndex++] = vertices[i + 1];
            linePrimitiveCount++;
        }
    }

    /// <summary>Draw the triangles added so far, so lines added after them land on top.</summary>
    public void FlushTriangles() => InternalFlushTriangles();

    /// <summary>
    /// Ends the batch of lines, submitting them to the graphics device.
    /// </summary>
    [Obsolete("Use End() instead. EndLineList will be removed after legacy cleanup.")]
    public void EndLineList() => InternalFlushLines();

    /// <summary>
    /// Ends the batch of lines as filled, submitting them to the graphics device.
    /// </summary>
    [Obsolete("Use End() instead. EndTriangleList will be removed after legacy cleanup.")]
    public void EndTriangleList() => InternalFlushTriangles();

    /// <summary>
    /// Flush triangles then lines (fills under outlines)
    /// </summary>
    public void End()
    {
        InternalFlushTriangles();
        InternalFlushLines();
    }

    void InternalFlushLines()
    {
        if (linePrimitiveCount <= 0)
            return;
        GraphicsDevice.BlendState = LineBlendState;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.LineList, lineVertices, 0, linePrimitiveCount);
        }
        lineVertexIndex = 0;
        linePrimitiveCount = 0;
    }

    void InternalFlushTriangles()
    {
        if (trianglePrimitiveCount <= 0)
            return;
        GraphicsDevice.BlendState = LineBlendState;
        foreach (EffectPass pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            GraphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, triangleVertices, 0, trianglePrimitiveCount);
        }
        triVertexIndex = 0;
        trianglePrimitiveCount = 0;
    }

    // Legacy DrawLineStrip / EndLineStrip removed: not used in current codebase after refactor.

    /// <summary>
    /// Disposes of the object, cleanly releasing graphics resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            effect?.Dispose();
            vertexDeclaration?.Dispose();
        }
    }
}