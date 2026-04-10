// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class PrecisionEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of small circles
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);
            var osuNextObj = (OsuDifficultyHitObject)current.Next(0);



            double currDistanceMultiplier = osuCurrObj != null ? Smootherstep(osuCurrObj.LazyJumpDistance / radius, 1, 2) : 1;
            double nextDistanceMultiplier = osuNextObj != null ? Smootherstep(osuNextObj.LazyJumpDistance / radius, 1, 2) : 1;

            double difficulty = Math.Max(0, osuCurrObj.SmallCircleBonus - 1);

            difficulty *= (currDistanceMultiplier + nextDistanceMultiplier) / 2;

            return difficulty;
        }
    }
}
