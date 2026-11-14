using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Xunit;

namespace VectorRumble.Tests
{
    public class NavGridTests
    {
        static readonly Rectangle Arena = new Rectangle(0, 0, 800, 400);

        // a wall down the middle with a gap at the top: x = 400, from y = 120 to the bottom
        static NavGrid WithWall() =>
            new NavGrid(new[] { new Vector2(400, 120), new Vector2(400, 400) }, Arena, 30f);

        [Fact]
        public void Cells_near_a_wall_or_the_edge_are_closed()
        {
            NavGrid grid = WithWall();

            Assert.False(grid.IsOpen(new Vector2(400, 300)));       // on the wall
            Assert.False(grid.IsOpen(new Vector2(410, 300)));       // beside it
            Assert.False(grid.IsOpen(new Vector2(5, 5)));           // in the corner
            Assert.True(grid.IsOpen(new Vector2(200, 200)));
            Assert.True(grid.IsOpen(new Vector2(400, 40)));         // through the gap
        }

        [Fact]
        public void A_line_is_blocked_by_a_wall_it_crosses_or_grazes_and_clear_otherwise()
        {
            NavGrid grid = WithWall();

            Assert.False(grid.LineClear(new Vector2(200, 300), new Vector2(600, 300), 4f));
            Assert.False(grid.LineClear(new Vector2(390, 200), new Vector2(390, 350), 20f));   // alongside, too close for the ship
            Assert.True(grid.LineClear(new Vector2(200, 300), new Vector2(300, 300), 4f));
            Assert.True(grid.LineClear(new Vector2(200, 60), new Vector2(600, 60), 20f));      // over the top of the wall
        }

        [Fact]
        public void A_route_goes_round_the_wall_through_the_gap()
        {
            NavGrid grid = WithWall();
            var path = new List<Vector2>();

            Assert.True(grid.FindPath(new Vector2(200, 300), new Vector2(600, 300), path));
            Assert.True(path.Count >= 2);

            // it bends up through the gap, never crossing the wall, and ends where it was going
            Vector2 at = new Vector2(200, 300);
            bool throughGap = false;
            foreach (Vector2 point in path)
            {
                Assert.True(grid.LineClear(at, point, 4f));
                if (point.Y < 120)
                {
                    throughGap = true;
                }
                at = point;
            }
            Assert.True(throughGap);
            Assert.True(Vector2.Distance(at, new Vector2(600, 300)) < NavGrid.DefaultCellSize);
        }

        [Fact]
        public void A_clear_way_is_a_straight_line()
        {
            NavGrid grid = WithWall();
            var path = new List<Vector2>();

            Assert.True(grid.FindPath(new Vector2(100, 200), new Vector2(300, 250), path));
            Assert.Single(path);
        }

        [Fact]
        public void A_sealed_pocket_has_no_route_to_it()
        {
            // a wall right across: nothing gets to the other side
            var grid = new NavGrid(new[] { new Vector2(400, 0), new Vector2(400, 400) }, Arena, 30f);
            Assert.False(grid.FindPath(new Vector2(200, 200), new Vector2(600, 200), new List<Vector2>()));
        }

        [Fact]
        public void Random_places_are_open_and_a_goal_in_a_wall_moves_to_the_nearest_open_place()
        {
            NavGrid grid = WithWall();
            var random = new Random(3);
            for (int i = 0; i < 50; i++)
            {
                Assert.True(grid.TryRandomOpen(random, out Vector2 p));
                Assert.True(grid.IsOpen(p));
            }

            Assert.True(grid.TryNearestOpen(new Vector2(400, 300), out Vector2 near));
            Assert.True(grid.IsOpen(near));
            Assert.True(Vector2.Distance(near, new Vector2(400, 300)) < 80f);
        }

        [Fact]
        public void A_level_with_no_walls_is_open_inside_its_edge()
        {
            var grid = new NavGrid(null, Arena, 30f);
            var path = new List<Vector2>();
            Assert.True(grid.FindPath(new Vector2(100, 100), new Vector2(700, 300), path));
            Assert.Single(path);
        }

        [Fact]
        public void Segment_distance_is_zero_when_they_cross_and_the_gap_when_they_do_not()
        {
            Assert.Equal(0f, NavGrid.SegmentDistance(new Vector2(0, 0), new Vector2(10, 10), new Vector2(0, 10), new Vector2(10, 0)));
            Assert.Equal(5f, NavGrid.SegmentDistance(new Vector2(0, 0), new Vector2(10, 0), new Vector2(0, 5), new Vector2(10, 5)), 3);
            Assert.Equal(5f, NavGrid.SegmentDistance(new Vector2(0, 0), new Vector2(10, 0), new Vector2(15, 0), new Vector2(20, 0)), 3);
        }
    }
}
