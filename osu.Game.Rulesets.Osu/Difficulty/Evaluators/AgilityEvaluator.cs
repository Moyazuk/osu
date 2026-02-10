// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

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

            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrev1Obj = (OsuDifficultyHitObject)current.Previous(1);

            double agilityBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null && osuPrev1Obj.Angle != null)
            {

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

                double angleDif = SnapAimEvaluator.AngleDifference(currentAngle, prevAngle);

                double angleBonus = 0.15 * Smootherstep(currentAngle, 40, 120);
                double vectorRepetition = SnapAimEvaluator.AngleVectorRepetition(osuCurrObj);

                double stackFactor = DifficultyCalculationUtils.Smootherstep(osuPrevObj.LazyJumpDistance, 0, diameter);

                double baseFactor = 1 - angleDif;

                double distanceBonus = 0.00000000125 * Math.Pow(osuCurrObj.LazyJumpDistance, 3) *
                                       Smootherstep(MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime, 2), 240, 300);

                // Penalize angle repetition.
                double aimComplexityBonus = angleBonus * Math.Pow(baseFactor + (1 - baseFactor) * vectorRepetition * stackFactor, 2);

                double velocityChangeBonus = Math.Abs(prevVelocity - currVelocity) * agilityVelocityChangeMultiplier;

                double baseBpm = baseBPMConstant / (1 + (angleBonus + distanceBonus + aimComplexityBonus * 0.05 + 0) * currDistanceMultiplier * prevDistanceMultiplier);

                agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 3) - 1);
            }

            return agilityBonus * 1.0575;
        }
    }
}
