// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;
            const int diameter = OsuDifficultyHitObject.NORMALISED_DIAMETER;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double prevDistanceMultiplier = DifficultyCalculationUtils.Smootherstep(osuPrevObj.LazyJumpDistance, radius / 2.0, radius);

            // If the previous notes are stacked, we add the previous note's strainTime since there was no movement since at least 2 notes earlier.
            // https://youtu.be/-yJPIk-YSLI?t=186
            double currTime = osuCurrObj.AdjustedDeltaTime + osuPrevObj.AdjustedDeltaTime * (1 - prevDistanceMultiplier);
            double prevTime = osuPrevObj.AdjustedDeltaTime;

            double wideBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;

                wideBonus += calcWideAngleBonus(currAngle) * prevDistanceMultiplier * 0.4;
            }

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(DifficultyCalculationUtils.MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / 230.0, 6) - 1);

            double difficulty = agilityBonus;

            difficulty *= 1 + wideBonus;

            difficulty *= osuCurrObj.SmallCircleBonus;

            return difficulty * 0.15;
        }

        private static double calcWideAngleBonus(double angle) => DifficultyCalculationUtils.Smoothstep(angle, double.DegreesToRadians(60), double.DegreesToRadians(110));
    }
}
