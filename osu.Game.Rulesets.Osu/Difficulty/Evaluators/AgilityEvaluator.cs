// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double baseFactor = 1;
            double angleBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;
                double lastAngle = osuPrevObj.Angle.Value;

                angleBonus = 0.35 * Smootherstep(currAngle, 0, 120);

                baseFactor = 1 - 0.25 * DifficultyCalculationUtils.Smoothstep(lastAngle, double.DegreesToRadians(90), double.DegreesToRadians(40)) * angleDifference(currAngle, lastAngle);
            }

            double currStrainTime = osuCurrObj.AdjustedDeltaTime;
            double lastStrainTime = osuPrevObj.AdjustedDeltaTime;

            double currVelocity = osuCurrObj.LazyJumpDistance / currStrainTime;

            double prevVelocity = osuPrevObj.LazyJumpDistance / lastStrainTime;

            double currTime = currStrainTime;
            double prevTime = lastStrainTime;

            // Penalize angle repetition.
            double angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * 0.95 * angleVectorRepetition(osuCurrObj), 2);

            double velocityChangeBonus = Math.Abs(prevVelocity - currVelocity) * 0.1;

            double distanceBonus = 0.00000000175 * Math.Pow(osuCurrObj.LazyJumpDistance, 3) *
                                   Smootherstep(MillisecondsToBPM(osuCurrObj.AdjustedDeltaTime, 2), 280, 320);

            // We reward high bpm more for wider angles, but only when both current and previous distance are over 0.5 radii.
            double baseBpm = 240.0 / (1 + (angleBonus + distanceBonus + velocityChangeBonus));

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 3) - 1);

            return agilityBonus * angleRepetitionNerf * 1.25;
        }

        private static double angleDifference(double curAngle, double lastAngle)
        {
            return Math.Cos(2 * Math.Min(Math.PI / 4, Math.Abs(curAngle - lastAngle)));
        }

        private static double angleVectorRepetition(OsuDifficultyHitObject current)
        {
            const double note_limit = 6;

            double constantAngleCount = 0;
            int index = 0;
            double notesProcessed = 0;

            while (notesProcessed < note_limit)
            {
                var loopObj = (OsuDifficultyHitObject)current.Previous(index);

                if (loopObj.IsNull())
                    break;

                if (Math.Abs(current.DeltaTime - loopObj.DeltaTime) > 25)
                    break;

                if (loopObj.NormalisedVectorAngle.IsNotNull() && current.NormalisedVectorAngle.IsNotNull())
                {
                    double angleDifference = Math.Abs(current.NormalisedVectorAngle.Value - loopObj.NormalisedVectorAngle.Value);
                    constantAngleCount += Math.Cos(8 * Math.Min(Math.PI / 16, angleDifference));
                }

                notesProcessed++;
                index++;
            }

            return Math.Pow(Math.Min(0.5 / constantAngleCount, 1), 2);
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(90), double.DegreesToRadians(140));
    }
}
