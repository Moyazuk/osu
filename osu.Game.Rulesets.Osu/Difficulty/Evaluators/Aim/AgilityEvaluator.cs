// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class AgilityEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of fast aiming
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool aimCheese)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;

            double effectiveDeltaTime = osuCurrObj.AdjustedDeltaTime;

            if (aimCheese && current.Index >= 2)
            {
                double prevDistance = osuPrevObj.LazyJumpDistance;
                effectiveDeltaTime += osuPrevObj.AdjustedDeltaTime *  (1 - DiffUtils.Smootherstep(prevDistance, 0, OsuDifficultyHitObject.NORMALISED_DIAMETER));
            }

            double numerator = 1;

            if (osuCurrObj.Angle != null && osuPrevObj?.Angle != null)
            {
                // angle switching bonus
                numerator += 0.5 * (1 - Math.Min(AngleUtils.CalculateAcuteness(osuCurrObj.Angle.Value), DiffUtils.Pow(AngleUtils.CalculateAcuteness(osuPrevObj.Angle.Value), 3)));
                // wide angle bonus
                numerator += 0.5 * AngleUtils.CalculateWideness(osuCurrObj.Angle.Value) * 1.5;
            }

            double agilityDifficulty = numerator / DiffUtils.Pow(effectiveDeltaTime, 3);

            agilityDifficulty *= DiffUtils.Pow(osuCurrObj.SmallCircleBonus, 1.5);

            return agilityDifficulty;
        }
    }
}
