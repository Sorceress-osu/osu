// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Catch.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Catch.Difficulty.Skills
{
    public class Movement : StrainDecaySkill
    {
        private const float absolute_player_positioning_error = 16f;
        private const float normalized_hitobject_radius = 41.0f;

        private const double direction_change_bonus = 21.5;
        private const double base_distance_bonus = 12.5;

        private const double edge_dash_threshold = 20.0;
        private const double edge_dash_bonus = 5.7;

        private const double strain_time_weight = 16;

        protected override double SkillMultiplier => 900;
        protected override double StrainDecayBase => 0.2;

        protected override double DecayWeight => 0.94;

        protected override int SectionLength => 750;

        protected readonly float HalfCatcherWidth;

        private bool isDirectionChange;
        private bool lastIsDirectionChange;
        private bool isBuzzslider;
        private bool lastHyperDash;

        private float? lastPlayerPosition;
        private float lastDistanceMoved;
        private double lastStrainTime;

        /// <summary>
        /// The speed multiplier applied to the player's catcher.
        /// </summary>
        private readonly double catcherSpeedMultiplier;

        public Movement(Mod[] mods, float halfCatcherWidth, double clockRate)
            : base(mods)
        {
            HalfCatcherWidth = halfCatcherWidth;

            // In catch, clockrate adjustments do not only affect the timings of hitobjects,
            // but also the speed of the player's catcher, which has an impact on difficulty
            // TODO: Support variable clockrates caused by mods such as ModTimeRamp
            //  (perhaps by using IApplicableToRate within the CatchDifficultyHitObject constructor to set a catcher speed for each object before processing)
            catcherSpeedMultiplier = clockRate;
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            var catchCurrent = (CatchDifficultyHitObject)current;

            lastPlayerPosition ??= catchCurrent.LastNormalizedPosition;

            float playerPosition = Math.Clamp(
                lastPlayerPosition.Value,
                catchCurrent.NormalizedPosition - (normalized_hitobject_radius - absolute_player_positioning_error),
                catchCurrent.NormalizedPosition + (normalized_hitobject_radius - absolute_player_positioning_error)
            );

            float distanceMoved = playerPosition - lastPlayerPosition.Value;

            double weightedStrainTime = catchCurrent.StrainTime + (strain_time_weight / catcherSpeedMultiplier);
            double lastWeightedStrainTime = lastStrainTime + (strain_time_weight / catcherSpeedMultiplier);

            double weightedSqrtStrain = Math.Sqrt(weightedStrainTime);
            double lastWeightedSqrtStrain = Math.Sqrt(lastWeightedStrainTime);

            // Initial addition based on distance moved
            double movementAddition = (Math.Pow(Math.Abs(distanceMoved), 1.3) / 510);

            // Are we changing direction
            isDirectionChange = (Math.Abs(distanceMoved) > 0.1 && Math.Abs(lastDistanceMoved) > 0.1 && Math.Sign(distanceMoved) != Math.Sign(lastDistanceMoved));

            // Direction change bonus considering both current and previous movement
            if (isDirectionChange)
            {
                double bonusFactor = Math.Min(50, Math.Abs(distanceMoved)) / 50;
                double antiflowFactor = Math.Max(Math.Min(70, Math.Abs(lastDistanceMoved)) / 70, 0.1);

                movementAddition += direction_change_bonus / lastWeightedSqrtStrain * bonusFactor * antiflowFactor * Math.Max(1 - Math.Pow(weightedStrainTime / 1000, 3), 0);
            }

            // Base bonus for every movement, giving some weight to streams
            if (Math.Abs(distanceMoved) > 0.1)
                movementAddition += base_distance_bonus * Math.Min(Math.Abs(distanceMoved), normalized_hitobject_radius * 2) / (normalized_hitobject_radius * 6) / weightedSqrtStrain;

            // Bonus for edge dashes, bonus is scaled with strain time (pre clockrate adjustments) as edge dashes are easier at lower ms values
            if (catchCurrent.LastObject.DistanceToHyperDash <= edge_dash_threshold && !catchCurrent.LastObject.HyperDash)
                movementAddition *= 1.0 + edge_dash_bonus * ((edge_dash_threshold - catchCurrent.LastObject.DistanceToHyperDash) / edge_dash_threshold) * Math.Pow((Math.Min(catchCurrent.StrainTime * catcherSpeedMultiplier, 265) / 265), 1.5);

            // After a hyperdash the player is at the exact position of the next fruit
            if (catchCurrent.LastObject.HyperDash)
                playerPosition = catchCurrent.NormalizedPosition;

            // Consecutive hyperdashes in the same direction require no change in input so are heavily nerfed
            if (catchCurrent.LastObject.HyperDash && lastHyperDash && !isDirectionChange)
                movementAddition *= 0.3;

            // Buzz slider fix
            if (isDirectionChange && lastIsDirectionChange && Math.Abs(distanceMoved) == Math.Abs(lastDistanceMoved) && Math.Abs(distanceMoved) <= HalfCatcherWidth)
            {
                // The fix isn't applied to the first instance of a buzzslider being triggered so players are still rewarded for the initial difficulty of catching such a pattern
                if (isBuzzslider)
                    movementAddition *= Math.Pow((Math.Min(catchCurrent.StrainTime, 120) / 120), 2);
                else
                    isBuzzslider = true;
            }
            else
            {
                isBuzzslider = false;
            }

            lastPlayerPosition = playerPosition;
            lastDistanceMoved = distanceMoved;
            lastStrainTime = catchCurrent.StrainTime;
            lastIsDirectionChange = isDirectionChange;
            lastHyperDash = catchCurrent.LastObject.HyperDash;

            return movementAddition / weightedStrainTime;
        }
    }
}
