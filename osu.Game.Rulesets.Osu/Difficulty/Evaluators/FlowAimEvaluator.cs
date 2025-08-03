// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowAimEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withSliderTravelDistance)
        {
            if (current.BaseObject is Spinner || current.Index <= 1 || current.Previous(0).BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            var currLazyJumpDistance = AdjustFlowDistance(osuCurrObj);

            // Base snap difficulty is velocity.
            double difficulty = currLazyJumpDistance / osuCurrObj.StrainTime;

            // But if the last object is a slider, then we extend the travel velocity through the slider into the current object.
            if (osuPrevObj.BaseObject is Slider && withSliderTravelDistance)
            {
                double travelVelocity = osuPrevObj.TravelDistance / osuPrevObj.TravelTime; // calculate the slider velocity from slider head to slider end.
                double movementVelocity = osuCurrObj.MinimumJumpDistance / osuCurrObj.MinimumJumpTime; // calculate the movement velocity from slider end to current object

                difficulty = Math.Max(difficulty, movementVelocity + travelVelocity); // take the larger total combined velocity.
            }

            return difficulty * 10000000;
        }

        public static double AdjustFlowDistance(DifficultyHitObject current)
        {
            var osuCurr = (OsuDifficultyHitObject)current;
            var osuPrev = (OsuDifficultyHitObject)current.Previous(0);

            // If angle is missing, no bonus is applied
            if (!osuCurr.Angle.HasValue)
                return osuCurr.LazyJumpDistance;

            double angle = osuCurr.Angle.Value;
            double lazyDistance = osuCurr.LazyJumpDistance;

            double maxBonusAngle = double.DegreesToRadians(120);

            if (angle >= maxBonusAngle)
                return lazyDistance;

            double previousVelocity = osuPrev.LazyJumpDistance / osuPrev.StrainTime;

            double angleScale = 1.0 - DifficultyCalculationUtils.Smootherstep(angle, 0, maxBonusAngle);

            double velocityBonus = 1 + previousVelocity * angleScale * 0.0;

            return lazyDistance * velocityBonus;
        }
    }
}
