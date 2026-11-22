using System;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Xunit;

namespace VectorRumble.Tests
{
    public class AIPilotTests
    {
        [Fact]
        public void Each_level_is_better_than_the_one_below()
        {
            AIProfile easy = AIProfile.For(AIDifficulty.Easy), medium = AIProfile.For(AIDifficulty.Medium), hard = AIProfile.For(AIDifficulty.Hard);

            Assert.True(easy.ReactionTime > medium.ReactionTime && medium.ReactionTime > hard.ReactionTime);
            Assert.True(easy.AimError > medium.AimError && medium.AimError > hard.AimError);
            Assert.True(easy.FireRange < medium.FireRange && medium.FireRange < hard.FireRange);
            Assert.True(easy.Lead < medium.Lead && medium.Lead < hard.Lead);
            Assert.True(easy.Dodge < medium.Dodge && medium.Dodge < hard.Dodge);
            Assert.True(easy.DodgeHorizon < medium.DodgeHorizon && medium.DodgeHorizon < hard.DodgeHorizon);
            Assert.True(easy.SightRange < medium.SightRange && medium.SightRange < hard.SightRange);
            Assert.True(easy.SpotDelay > medium.SpotDelay && medium.SpotDelay > hard.SpotDelay);
            Assert.True(easy.WarningPause > medium.WarningPause && medium.WarningPause > hard.WarningPause);
            Assert.True(easy.FleeShield < medium.FleeShield && medium.FleeShield < hard.FleeShield);
        }

        [Fact]
        public void The_levels_run_in_their_own_way_and_give_up_a_search_in_a_couple_of_seconds()
        {
            Assert.Equal(FleeStyle.Straight, AIProfile.For(AIDifficulty.Easy).Flee);
            Assert.Equal(FleeStyle.Zigzag, AIProfile.For(AIDifficulty.Medium).Flee);
            Assert.Equal(FleeStyle.Cover, AIProfile.For(AIDifficulty.Hard).Flee);
            Assert.Equal(2f, AIProfile.For(AIDifficulty.Medium).LoseSightTime);

            // the first shot misses on purpose at every level
            foreach (AIDifficulty level in Enum.GetValues(typeof(AIDifficulty)))
            {
                Assert.True(AIProfile.For(level).WarningMiss > AIProfile.For(level).FireCone * 0.5f);
            }
        }

        [Fact]
        public void A_pilot_pulled_into_an_obstacle_slides_along_it_instead_of_standing_still()
        {
            // pushed down the screen by a wall above, wanting to go straight up
            Vector2 away = new Vector2(0, 1), want = new Vector2(0, -1);

            Vector2 slid = AIPilot.Slide(away, want, 1f);
            Assert.True(Math.Abs(slid.X) > 0.9f);                 // sideways
            Assert.True(Vector2.Dot(slid, away) >= -0.001f);      // no part of it into the wall

            // the other side when the pilot has swapped
            Assert.Equal(-slid.X, AIPilot.Slide(away, want, -1f).X, 3);

            // a goal along or away from the obstacle is left alone
            Assert.Equal(new Vector2(1, 0.5f), AIPilot.Slide(away, new Vector2(1, 0.5f), 1f));
            Assert.Equal(want, AIPilot.Slide(Vector2.Zero, want, 1f));
        }

        [Fact]
        public void A_hit_is_located_better_the_harder_the_level()
        {
            Assert.True(AIProfile.For(AIDifficulty.Easy).HitLocateError > AIProfile.For(AIDifficulty.Medium).HitLocateError);
            Assert.Equal(0f, AIProfile.For(AIDifficulty.Hard).HitLocateError);
        }

        [Fact]
        public void The_levels_keep_their_saved_numbers()
        {
            Assert.Equal(0, (int)AIDifficulty.Easy);
            Assert.Equal(1, (int)AIDifficulty.Medium);
            Assert.Equal(2, (int)AIDifficulty.Hard);
        }

        [Fact]
        public void Difficulty_is_saved_and_defaults_to_medium()
        {
            Assert.Equal(AIDifficulty.Medium, new VectorRumbleSettings().AIDifficulty);
            var back = JsonSerializer.Deserialize<VectorRumbleSettings>(JsonSerializer.Serialize(new VectorRumbleSettings { AIDifficulty = AIDifficulty.Hard }));
            Assert.Equal(AIDifficulty.Hard, back.AIDifficulty);
            // a file from before the setting existed
            Assert.Equal(AIDifficulty.Medium, JsonSerializer.Deserialize<VectorRumbleSettings>("{\"MusicPercent\":50}").AIDifficulty);
        }

        [Fact]
        public void Aiming_ahead_leads_a_moving_target_and_not_a_still_one()
        {
            Vector2 from = Vector2.Zero, target = new Vector2(640, 0);

            Vector2 still = AIPilot.Aim(target, Vector2.Zero, from, 1f);
            Assert.Equal(1f, still.X, 3);

            // it moves down the screen at 100 a second, and the shot takes a second to arrive
            Vector2 ahead = AIPilot.Aim(target, new Vector2(0, 100), from, 1f);
            Assert.True(ahead.Y > 0.1f);
            Assert.Equal(1f, ahead.Length(), 3);

            // no lead aims straight at it
            Assert.Equal(1f, AIPilot.Aim(target, new Vector2(0, 100), from, 0f).X, 3);
        }

        [Fact]
        public void Aiming_at_ones_own_position_does_not_produce_nan()
        {
            Vector2 aim = AIPilot.Aim(Vector2.Zero, Vector2.Zero, Vector2.Zero, 1f);
            Assert.False(float.IsNaN(aim.X) || float.IsNaN(aim.Y));
        }

        [Fact]
        public void The_firing_cone_is_either_side_of_the_nose()
        {
            Vector2 up = AIPilot.Forward(0f);
            Assert.Equal(new Vector2(0, -1), up);
            Assert.True(AIPilot.WithinCone(up, AIPilot.Rotate(up, 0.2f), 0.3f));
            Assert.True(AIPilot.WithinCone(up, AIPilot.Rotate(up, -0.2f), 0.3f));
            Assert.False(AIPilot.WithinCone(up, AIPilot.Rotate(up, 0.5f), 0.3f));
            Assert.True(AIPilot.WithinCone(up, up, 0f));
        }

        [Fact]
        public void A_wall_pushes_away_from_itself_and_only_when_near()
        {
            Vector2 a = new Vector2(0, 100), b = new Vector2(400, 100);

            Vector2 near = AIPilot.Repulsion(new Vector2(200, 130), a, b, 100f);
            Assert.True(near.Y > 0f);
            Assert.Equal(0f, near.X, 3);

            Assert.Equal(Vector2.Zero, AIPilot.Repulsion(new Vector2(200, 300), a, b, 100f));

            // beside the end of the wall, the push comes from the end
            Vector2 end = AIPilot.Repulsion(new Vector2(430, 100), a, b, 100f);
            Assert.True(end.X > 0f);
        }

        [Fact]
        public void A_shot_on_course_is_dodged_to_the_side_it_misses_on()
        {
            Vector2 ship = new Vector2(500, 0);

            // coming along the x axis, slightly below the ship's centre line: step up the screen
            Assert.True(AIPilot.Incoming(ship, new Vector2(0, 5), new Vector2(640, 0), 30f, 600f, out float time, out Vector2 side));
            Assert.Equal(500f / 640f, time, 3);
            Assert.True(Math.Abs(side.Y) > 0.9f);
            Assert.True(side.Y < 0f);

            // the same shot passing far off, going away, or too far to matter
            Assert.False(AIPilot.Incoming(ship, new Vector2(0, 200), new Vector2(640, 0), 30f, 600f, out _, out _));
            Assert.False(AIPilot.Incoming(ship, new Vector2(900, 0), new Vector2(640, 0), 30f, 600f, out _, out _));
            Assert.False(AIPilot.Incoming(ship, new Vector2(-900, 0), new Vector2(640, 0), 30f, 600f, out _, out _));
            Assert.False(AIPilot.Incoming(ship, new Vector2(0, 0), Vector2.Zero, 30f, 600f, out _, out _));
        }

        [Fact]
        public void Ai_pilots_are_for_this_machine_only_and_never_go_on_the_network()
        {
            Assert.InRange(new MatchRules().AIPilots, 1, MatchRules.MaxAIPilots);
            Assert.Equal(3, MatchRules.MaxAIPilots);

            var rules = new MatchRules { AIPilots = 3 };
            using (var stream = new System.IO.MemoryStream())
            {
                using (var writer = new System.IO.BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    rules.Write(writer);
                }
                stream.Position = 0;
                Assert.Equal(0, MatchRules.Read(new System.IO.BinaryReader(stream)).AIPilots);
            }
        }
    
        [Fact]
        public void A_chase_survives_a_short_loss_of_sight_but_not_a_long_one()
        {
            // two looks without sight used to end it, which on an arena with walls made the pilot flip between chasing and searching
            Assert.False(AIPilot.LostChase(2, AIProfile.For(AIDifficulty.Medium).ReactionTime));
            Assert.True(AIPilot.LostChase(4, AIProfile.For(AIDifficulty.Medium).ReactionTime));
            Assert.True(AIPilot.LostChase(2, AIProfile.For(AIDifficulty.Easy).ReactionTime));    // a slow pilot's looks are far apart
            Assert.False(AIPilot.LostChase(4, AIProfile.For(AIDifficulty.Hard).ReactionTime));
            Assert.True(AIPilot.LostChase(7, AIProfile.For(AIDifficulty.Hard).ReactionTime));
        }

        [Fact]
        public void A_pilot_keeps_its_rival_for_a_while_and_only_turns_to_one_that_is_clearly_nearer()
        {
            Assert.False(AIPilot.ShouldSwitchRival(0.5f, 400f, 100f));     // just took this one: stay, even if another is much nearer
            Assert.False(AIPilot.ShouldSwitchRival(5f, 300f, 280f));       // held long enough, but the other is barely nearer
            Assert.True(AIPilot.ShouldSwitchRival(5f, 300f, 150f));        // held long enough and the other is much nearer
            Assert.False(AIPilot.ShouldSwitchRival(5f, 300f, 400f));       // never to one that is further
        }

        [Fact]
        public void A_power_up_is_wanted_only_if_it_is_better_than_the_weapon_in_hand()
        {
            Assert.Equal(0, AIPilot.Worth((Weapon)null));
            Assert.True(AIPilot.Worth(PowerUpKind.TripleLaser) > AIPilot.Worth(PowerUpKind.DoubleLaser));
            Assert.True(AIPilot.WantsPowerUp(0, AIPilot.Worth(PowerUpKind.DoubleLaser)));      // a plain laser takes any of them
            Assert.True(AIPilot.WantsPowerUp(AIPilot.Worth(PowerUpKind.DoubleLaser), AIPilot.Worth(PowerUpKind.Rocket)));
            Assert.False(AIPilot.WantsPowerUp(AIPilot.Worth(PowerUpKind.TripleLaser), AIPilot.Worth(PowerUpKind.DoubleLaser)));   // never a step down
            Assert.False(AIPilot.WantsPowerUp(AIPilot.Worth(PowerUpKind.Rocket), AIPilot.Worth(PowerUpKind.TripleLaser)));        // nor a swap for one as good
        }

        [Fact]
        public void A_pilot_goes_for_a_power_up_unless_a_rival_is_close_and_in_sight()
        {
            Assert.True(AIPilot.PreferCollect(500f, false, 100f));     // no rival in sight
            Assert.True(AIPilot.PreferCollect(500f, true, 900f));      // rival far off
            Assert.False(AIPilot.PreferCollect(300f, true, 250f));     // a rival in sight and close, the power-up not much nearer
            Assert.True(AIPilot.PreferCollect(80f, true, 250f));       // the power-up is within a step
        }
}
}
