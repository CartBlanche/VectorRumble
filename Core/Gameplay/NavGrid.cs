using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace VectorRumble
{
    /// <summary>
    /// What the computer's pilots know of an arena: a coarse grid over it, with the cells a ship cannot sit in (too near a
    /// wall or the edge) marked, and A* over the open ones. It is built from the arena's walls when a match starts, so a
    /// level made by someone else needs nothing in it for the pilots to find their way. Asteroids move, so they are not in
    /// the grid; the pilots steer round them and count them when they look for a clear shot.
    /// </summary>
    sealed class NavGrid
    {
        public const float DefaultCellSize = 40f;

        readonly float cell;
        readonly int columns, rows;
        readonly Vector2 origin;
        readonly bool[] open;
        readonly Vector2[] walls;           // pairs of points, in pixels

        // A* scratch space, reused: a search allocates nothing but the path it returns
        readonly float[] cost;
        readonly int[] cameFrom;
        readonly bool[] closed;
        readonly PriorityQueue<int, float> queue = new PriorityQueue<int, float>();

        public int Columns => columns;
        public int Rows => rows;

        /// <param name="normalisedWalls">The arena's walls as the level stores them: pairs of points, 0 to 1 across the arena.</param>
        /// <param name="size">The arena's size in pixels.</param>
        /// <param name="safe">The area ships may be in; the edge of the arena bounces them back.</param>
        /// <param name="clearance">How far a ship's centre must be from a wall (about its radius and a little more).</param>
        public static NavGrid Build(Vector2[] normalisedWalls, Vector2 size, Rectangle safe, float clearance, float cellSize = DefaultCellSize)
        {
            int count = (normalisedWalls?.Length ?? 0) / 2 * 2;
            var walls = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                walls[i] = normalisedWalls[i] * size;
            }

            return new NavGrid(walls, safe, clearance, cellSize);
        }

        public NavGrid(Vector2[] wallPoints, Rectangle safe, float clearance, float cellSize = DefaultCellSize)
        {
            cell = cellSize;
            walls = wallPoints ?? new Vector2[0];
            origin = new Vector2(safe.Left, safe.Top);
            columns = Math.Max(1, (int)Math.Ceiling(safe.Width / cell));
            rows = Math.Max(1, (int)Math.Ceiling(safe.Height / cell));
            open = new bool[columns * rows];
            cost = new float[open.Length];
            cameFrom = new int[open.Length];
            closed = new bool[open.Length];

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Vector2 centre = Centre(x, y);
                    bool inside = centre.X - clearance >= safe.Left && centre.X + clearance <= safe.Right
                               && centre.Y - clearance >= safe.Top && centre.Y + clearance <= safe.Bottom;
                    open[y * columns + x] = inside && SegmentsClear(centre, centre, clearance);
                }
            }
        }

        Vector2 Centre(int x, int y) => origin + new Vector2((x + 0.5f) * cell, (y + 0.5f) * cell);

        Vector2 Centre(int index) => Centre(index % columns, index / columns);

        int CellOf(Vector2 p)
        {
            int x = Math.Max(0, Math.Min(columns - 1, (int)Math.Floor((p.X - origin.X) / cell)));
            int y = Math.Max(0, Math.Min(rows - 1, (int)Math.Floor((p.Y - origin.Y) / cell)));
            return y * columns + x;
        }

        /// <summary>True if the point is in a cell a ship can be in.</summary>
        public bool IsOpen(Vector2 p) => open[CellOf(p)];

        /// <summary>The centre of the open cell nearest to the point (the point's own cell if it is open).</summary>
        public bool TryNearestOpen(Vector2 p, out Vector2 result)
        {
            int index = NearestOpenCell(p);
            result = index >= 0 ? Centre(index) : p;
            return index >= 0;
        }

        int NearestOpenCell(Vector2 p)
        {
            int start = CellOf(p);
            if (open[start])
            {
                return start;
            }

            int sx = start % columns, sy = start / columns;
            int best = -1;
            float bestDistance = float.MaxValue;
            int reach = Math.Max(columns, rows);
            for (int ring = 1; ring <= reach; ring++)
            {
                for (int y = sy - ring; y <= sy + ring; y++)
                {
                    for (int x = sx - ring; x <= sx + ring; x++)
                    {
                        bool onRing = Math.Abs(x - sx) == ring || Math.Abs(y - sy) == ring;
                        if (!onRing || x < 0 || y < 0 || x >= columns || y >= rows || !open[y * columns + x])
                        {
                            continue;
                        }

                        float d = Vector2.DistanceSquared(p, Centre(x, y));
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = y * columns + x;
                        }
                    }
                }

                if (best >= 0)
                {
                    break;
                }
            }

            return best;
        }

        /// <summary>A random place a ship can be, or false when the arena has none.</summary>
        public bool TryRandomOpen(Random random, out Vector2 result)
        {
            for (int tries = 0; tries < 40; tries++)
            {
                int index = random.Next(open.Length);
                if (open[index])
                {
                    result = Centre(index);
                    return true;
                }
            }

            for (int i = 0; i < open.Length; i++)
            {
                if (open[i])
                {
                    result = Centre(i);
                    return true;
                }
            }

            result = Vector2.Zero;
            return false;
        }

        #region Straight lines

        /// <summary>True if a disc of the given radius can slide from a to b without touching a wall.</summary>
        public bool LineClear(Vector2 a, Vector2 b, float radius) => SegmentsClear(a, b, radius);

        bool SegmentsClear(Vector2 a, Vector2 b, float radius)
        {
            for (int i = 0; i + 1 < walls.Length; i += 2)
            {
                if (SegmentDistance(a, b, walls[i], walls[i + 1]) < radius)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The shortest distance between the segment a-b and the segment c-d (0 if they cross).</summary>
        internal static float SegmentDistance(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            if (SegmentsCross(a, b, c, d))
            {
                return 0f;
            }

            return Math.Min(
                Math.Min(PointSegmentDistance(a, c, d), PointSegmentDistance(b, c, d)),
                Math.Min(PointSegmentDistance(c, a, b), PointSegmentDistance(d, a, b)));
        }

        internal static float PointSegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.LengthSquared();
            float t = lengthSquared > 0f ? MathHelper.Clamp(Vector2.Dot(p - a, ab) / lengthSquared, 0f, 1f) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c), d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a), d4 = Cross(b - a, d - a);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f)) && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        static float Cross(Vector2 u, Vector2 v) => u.X * v.Y - u.Y * v.X;

        #endregion

        #region A*

        static readonly int[] stepX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] stepY = { 0, 0, 1, -1, 1, -1, 1, -1 };

        /// <summary>
        /// Finds a way from one place to another through the open cells and puts the points to steer through in
        /// <paramref name="path"/> (cleared first), the last being the destination's cell. Straight stretches are cut
        /// short, so the path has a point for each bend rather than for each cell. False if there is no way.
        /// </summary>
        public bool FindPath(Vector2 start, Vector2 goal, List<Vector2> path, float shipRadius = 24f)
        {
            path.Clear();
            int s = NearestOpenCell(start), g = NearestOpenCell(goal);
            if (s < 0 || g < 0)
            {
                return false;
            }

            if (s == g)
            {
                path.Add(Centre(g));
                return true;
            }

            Array.Fill(cost, float.MaxValue);
            Array.Clear(closed, 0, closed.Length);
            queue.Clear();

            cost[s] = 0f;
            cameFrom[s] = -1;
            queue.Enqueue(s, Heuristic(s, g));

            bool found = false;
            while (queue.TryDequeue(out int current, out _))
            {
                if (closed[current])
                {
                    continue;
                }

                closed[current] = true;
                if (current == g)
                {
                    found = true;
                    break;
                }

                int cx = current % columns, cy = current / columns;
                for (int k = 0; k < 8; k++)
                {
                    int nx = cx + stepX[k], ny = cy + stepY[k];
                    if (nx < 0 || ny < 0 || nx >= columns || ny >= rows)
                    {
                        continue;
                    }

                    int next = ny * columns + nx;
                    if (!open[next] || closed[next])
                    {
                        continue;
                    }

                    // no cutting the corner of a blocked cell
                    if (k >= 4 && (!open[cy * columns + nx] || !open[ny * columns + cx]))
                    {
                        continue;
                    }

                    float step = k >= 4 ? 1.4142f : 1f;
                    float candidate = cost[current] + step;
                    if (candidate < cost[next])
                    {
                        cost[next] = candidate;
                        cameFrom[next] = current;
                        queue.Enqueue(next, candidate + Heuristic(next, g));
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            // back from the goal to the start
            for (int at = g; at != s; at = cameFrom[at])
            {
                path.Add(Centre(at));
            }

            path.Reverse();
            Smooth(start, path, shipRadius);
            return true;
        }

        float Heuristic(int a, int b)
        {
            int dx = Math.Abs(a % columns - b % columns), dy = Math.Abs(a / columns - b / columns);
            return Math.Max(dx, dy) + 0.4142f * Math.Min(dx, dy);
        }

        // drops every point that the one before it can already see past
        void Smooth(Vector2 start, List<Vector2> path, float shipRadius)
        {
            if (path.Count < 2)
            {
                return;
            }

            var kept = new List<Vector2>(path.Count);
            Vector2 from = start;
            int i = 0;
            while (i < path.Count)
            {
                int furthest = i;
                for (int j = path.Count - 1; j > i; j--)
                {
                    if (LineClear(from, path[j], shipRadius))
                    {
                        furthest = j;
                        break;
                    }
                }

                kept.Add(path[furthest]);
                from = path[furthest];
                i = furthest + 1;
            }

            path.Clear();
            path.AddRange(kept);
        }

        #endregion
    }
}
