// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
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
            var osuPrev1Obj = (OsuDifficultyHitObject)current.Previous(1);

            double agilityBonus = 0;

            if (osuCurrObj.Angle != null && osuPrevObj.Angle != null && osuPrev1Obj.Angle != null)
            {

                double currStrainTime = osuCurrObj.AdjustedDeltaTime;
                double lastStrainTime = osuPrevObj.AdjustedDeltaTime;

                double currDistanceMultiplier = Smootherstep(osuCurrObj.LazyJumpDistance / radius, 1, 2);
                double prevDistanceMultiplier = Smootherstep(osuPrevObj.LazyJumpDistance / radius, 1, 2);

                double currTime = currStrainTime + lastStrainTime * (1 - prevDistanceMultiplier);
                double prevTime = lastStrainTime;

                double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;

                double angleBonus = 0.1 * Smootherstep(currentAngle, 40, 120);

                double baseBpm = 240 / (1 + (angleBonus) * currDistanceMultiplier * prevDistanceMultiplier);

                agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, 2) - 1);
            }

            return agilityBonus * 24.1575;
        }
    }
}
