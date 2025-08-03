// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class FlowStrainEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current;

            double travelDistance = osuPrevObj?.TravelDistance ?? 0;
            double distance = travelDistance + osuCurrObj.MinimumJumpDistance;

            double difficulty = osuCurrObj.LazyJumpDistance / osuCurrObj.StrainTime;

            double adjustedDistanceScale = 1.0;

            if (osuCurrObj.Angle.HasValue &&
                osuPrevObj?.Angle != null &&
                Math.Abs(osuCurrObj.DeltaTime - osuPrevObj.DeltaTime) < 25)
            {
                double angleDifference = Math.Abs(osuCurrObj.Angle.Value - osuPrevObj.Angle.Value);
                double angleDifferenceAdjusted = Math.Sin(angleDifference / 2) * 180.0;
                double angularVelocity = angleDifferenceAdjusted / (0.1 * osuCurrObj.StrainTime);
                double angularVelocityBonus = Math.Max(0.0, Math.Pow(angularVelocity, 0.5) - 1.0);
                adjustedDistanceScale = 0.8 + angularVelocityBonus * 0.3;
            }

            return difficulty * adjustedDistanceScale * 0;
        }
    }
}
