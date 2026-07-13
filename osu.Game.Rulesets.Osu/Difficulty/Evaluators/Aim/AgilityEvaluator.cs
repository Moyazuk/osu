// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
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

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = current.Index > 0 ? (OsuDifficultyHitObject)current.Previous(0) : null;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            double strain = 10 / Math.Pow(osuCurrObj.AdjustedDeltaTime / 1000.0, 4);

            double wideAngleBonus = 0;

            if (osuCurrObj.Angle != null)
            {
                double currAngle = osuCurrObj.Angle.Value;

                double prevDistanceMultiplier = DiffUtils.Smootherstep(osuPrevObj.LazyJumpDistance / radius, 1, 2);

                wideAngleBonus = SnapAimEvaluator.CalcAngleWideness(currAngle) * prevDistanceMultiplier;
            }

            strain *= 1 + wideAngleBonus * 2;

            strain *= Math.Pow(osuCurrObj.SmallCircleBonus, 1.5);

            return strain;
        }
    }
}
