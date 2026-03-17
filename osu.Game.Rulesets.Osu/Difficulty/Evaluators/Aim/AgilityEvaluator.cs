// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class AgilityEvaluator
    {
        private const double distance_cap = OsuDifficultyHitObject.NORMALISED_DIAMETER * 1.25; // 1.25 circles distance between centers

        /// <summary>
        /// Evaluates the difficulty of fast aiming
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, Movement currentMovement)
        {
            if (current.BaseObject is Spinner || current.Index < 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            double agilityBonus = 0;

            var previousMovement = currentMovement.PreviousMovement!;
            var prevPrevMovement = previousMovement.PreviousMovement;

            if (prevPrevMovement != null)
            {
                const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

                var osuCurrObj = (OsuDifficultyHitObject)current;
                var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

                double currStrainTime = currentMovement.Time;
                double lastStrainTime = previousMovement.Time;

                double currVelocity = currentMovement.Distance / currStrainTime;
                double prevVelocity = previousMovement.Distance / lastStrainTime;

                double currDistanceMultiplier = DifficultyCalculationUtils.Smootherstep(currentMovement.Distance / radius, 1, 2);
                double prevDistanceMultiplier = DifficultyCalculationUtils.Smootherstep(previousMovement.Distance / radius, 1, 2);

                double currTime = currStrainTime;
                double prevTime = lastStrainTime;

                double currAngle = currentMovement.Angle(previousMovement);

                double angleBonus = 0.85 * DifficultyCalculationUtils.Smootherstep(currAngle, 40, 140) * currDistanceMultiplier * prevDistanceMultiplier;

                double velocityBonus = Math.Pow(osuCurrObj.LazyJumpDistance / currStrainTime, 2) * 0.0015;

                double baseBpm = 260 / (1 + (angleBonus + velocityBonus));

                agilityBonus = Math.Max(0, Math.Pow(DifficultyCalculationUtils.MillisecondsToBPM(currTime, 2) / baseBpm, 3) - 1);
            }

            return agilityBonus * 1;
        }

        private static double highBpmBonus(double ms) => 1 / (1 - Math.Pow(0.3, Math.Pow(ms / 1000, 0.9)));
    }
}
