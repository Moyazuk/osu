// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        // === Tunable constants for MassBalancer ===
        public static double baseBPMConstant = 240.0;
        public static double agilityExponent = 4.0;
        public static double agilityOverallMultiplier = 0.02;
        public static double agilityVelocityChangeMultiplier = 0.1;
        public static double angleBonusMultiplier = 0.35;
        public static double distanceBonusMultiplier = 0.00000000175;

        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (!IsValid(current, 3))
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double nukeMultiplier = 8;

            double currStrainTime = osuCurrObj.AdjustedDeltaTime;
            double lastStrainTime = osuPrevObj.AdjustedDeltaTime;

            double currVelocity = osuCurrObj.LazyJumpDistance / currStrainTime;
            double prevVelocity = osuPrevObj.LazyJumpDistance / lastStrainTime;

            double currDistanceMultiplier = Smootherstep(osuCurrObj.LazyJumpDistance / radius, 1, 2);
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.LazyJumpDistance / radius, 1, 2);

            double currTime = currStrainTime + lastStrainTime * (1 - prevDistanceMultiplier);
            double prevTime = lastStrainTime;

            double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;
            double prevAngle = osuPrevObj.Angle!.Value * 180 / Math.PI;

            double angleBonus = 0.50 * Smootherstep(currentAngle, 0, 120);

            double velocityChangeBonus = Math.Abs(prevVelocity - currVelocity) * agilityVelocityChangeMultiplier;

            double distanceBonus = 0.00000075 * Math.Pow(osuCurrObj.LazyJumpDistance, 2)
                                                 * Smootherstep(MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime, 2), 280, 320);

            double baseBpm = 240 / (1 + (angleBonus + distanceBonus) * currDistanceMultiplier * prevDistanceMultiplier);

            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 4) - 1);

            return agilityBonus * 0.295;
        }
    }
}
