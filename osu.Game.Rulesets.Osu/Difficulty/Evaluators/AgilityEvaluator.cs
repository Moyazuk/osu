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
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (!IsValid(current, 2))
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currVelocity = osuCurrObj.LazyJumpDistance / osuCurrObj.StrainTime;
            double prevVelocity = osuPrevObj.LazyJumpDistance / osuPrevObj.StrainTime;

            double currDistanceMultiplier = Smootherstep(osuCurrObj.LazyJumpDistance / radius, 0.5, 1);
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.LazyJumpDistance / radius, 0.5, 1);

            // If the previous notes are stacked, we add the previous note's strainTime since there was no movement since at least 2 notes earlier.
            // https://youtu.be/-yJPIk-YSLI?t=186
            double currTime = osuCurrObj.StrainTime + osuPrevObj.StrainTime * (1 - prevDistanceMultiplier);
            double prevTime = osuPrevObj.StrainTime;

            double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

            // We reward high bpm more for wider angles, but only when both current and previous distance are over 0.5 radii.
            double baseBpm = 240.0 / (1 + 0 * Smootherstep(currentAngle, 0, 120) * currDistanceMultiplier * prevDistanceMultiplier);

            // Agility bonus of 1 at base BPM.
            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 4) - 1);

            return agilityBonus * 14;
        }
    }
}
