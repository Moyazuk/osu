// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
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
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuPrev1Obj = (OsuDifficultyHitObject)current.Previous(1);

            double agilityBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null && osuPrev1Obj.Angle != null)
            {

                double currStrainTime = osuCurrObj.AdjustedDeltaTime;
                double lastStrainTime = osuPrevObj.AdjustedDeltaTime;

                double currDistanceMultiplier = DifficultyCalculationUtils.Smootherstep(osuCurrObj.LazyJumpDistance / radius, 1, 2);
                double prevDistanceMultiplier = DifficultyCalculationUtils.Smootherstep(osuPrevObj.LazyJumpDistance / radius, 1, 2);

                double currTime = currStrainTime + lastStrainTime * (1 - prevDistanceMultiplier);
                double prevTime = lastStrainTime;

                double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

                double angleBonus = 1 + 12 * DifficultyCalculationUtils.Smootherstep(currentAngle, 40, 140) * currDistanceMultiplier * prevDistanceMultiplier;

                double velocityBonus = Math.Pow(osuCurrObj.LazyJumpDistance / currStrainTime, 3) * 0.00075;

                double baseBpm = 240 / (1 + velocityBonus);



                agilityBonus = Math.Max(0, Math.Pow(DifficultyCalculationUtils.MillisecondsToBPM(currTime, 2) / baseBpm, 4) - 1);

                agilityBonus *= angleBonus;

                // Penalize angle repetition.
                agilityBonus *= vectorAngleRepetition(osuCurrObj, osuPrevObj);
            }

            return agilityBonus;
        }


        private static double vectorAngleRepetition(OsuDifficultyHitObject current, OsuDifficultyHitObject previous)
        {
            if (current.Angle == null || previous.Angle == null)
                return 1;

            const double note_limit = 6;

            double constantAngleCount = 0;

            for (int index = 0; index < note_limit; index++)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                // Only consider vectors in the same jump section, stopping to change rhythm ruins momentum
                if (Math.Max(current.AdjustedDeltaTime, loopObj.AdjustedDeltaTime) > 1.1 * Math.Min(current.AdjustedDeltaTime, loopObj.AdjustedDeltaTime))
                    break;

                if (loopObj.NormalisedVectorAngle.IsNotNull() && current.NormalisedVectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - loopObj.NormalisedVectorAngle.Value);
                    // Refer to this desmos for tuning, constants need to be precise so that values stay within the range of 0 and 1.
                    // https://www.desmos.com/calculator/a8jesv5sv2
                    constantAngleCount += Math.Cos(8 * Math.Min(double.DegreesToRadians(11.25), angleDifference));
                }
            }

            double vectorRepetition = Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);

            double stackFactor = DifficultyCalculationUtils.Smootherstep(current.LazyJumpDistance, 0, OsuDifficultyHitObject.NORMALISED_DIAMETER);

            double currAngle = current.Angle.Value;
            double lastAngle = previous.Angle.Value;

            double angleDifferenceAdjusted = Math.Cos(2 * Math.Min(double.DegreesToRadians(45), Math.Abs(currAngle - lastAngle) * stackFactor));

            double baseNerf = 1 - 0.35 * SnapAimEvaluator.CalcAcuteAngleBonus(lastAngle) * angleDifferenceAdjusted;

            return Math.Pow(baseNerf + (1 - baseNerf) * vectorRepetition * 0.25 * stackFactor, 2);
        }
    }
}
