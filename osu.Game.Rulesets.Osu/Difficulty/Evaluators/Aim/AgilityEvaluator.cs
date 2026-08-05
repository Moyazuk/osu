// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class AgilityEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of fast aiming
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            const double distance_cap = OsuDifficultyHitObject.NORMALISED_DIAMETER * 1.2; // 1.2 circles distance between centers

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;

            var osuPrevPrevObj = current.Index > 1 ? (OsuDifficultyHitObject)current.Previous(1) : null;

            double travelDistance = osuPrevObj?.LazyTravelDistance ?? 0;
            double distance = travelDistance + osuCurrObj.LazyJumpDistance;

            double distanceScaled = Math.Min(distance, distance_cap) / distance_cap;

            double agilityDifficulty = distanceScaled * 1000 / osuCurrObj.AdjustedDeltaTime;

            agilityDifficulty *= DiffUtils.Pow(osuCurrObj.SmallCircleBonus, 1.5);

            agilityDifficulty *= highBpmBonus(osuCurrObj.AdjustedDeltaTime);

            return agilityDifficulty;
        }

        private static double highBpmBonus(double ms) => 1 / (1 - DiffUtils.Pow(0.2, ms / 1000));

        public static double EvaluateHybridBonus(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;
            var osuPrevPrevObj = current.Index > 1 ? (OsuDifficultyHitObject)current.Previous(1) : null;

            double hybridBonus = 0;

            if (osuPrevObj != null && osuPrevPrevObj != null)
            {
                double currentSpeedDifficulty = SpeedEvaluator.EvaluateDifficultyOf(osuPrevObj);
                double previousSpeedDifficulty = SpeedEvaluator.EvaluateDifficultyOf(osuPrevPrevObj);

                hybridBonus = Math.Min(currentSpeedDifficulty, previousSpeedDifficulty);
                hybridBonus /= osuCurrObj.AdjustedDeltaTime;
            }

            return hybridBonus * 0.17;
        }
    }
}
