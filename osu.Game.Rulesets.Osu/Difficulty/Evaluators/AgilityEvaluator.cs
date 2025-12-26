// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using static osu.Game.Rulesets.Difficulty.Utils.DifficultyCalculationUtils;
using static osu.Game.Rulesets.Osu.Difficulty.Preprocessing.OsuDifficultyHitObject;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AgilityEvaluator
    {
        public static double EvaluateDifficultyOf(DifficultyHitObject current, bool withCheesability, OsuDifficultyTuning tuning)
        {
            if (!IsValid(current, 3))
                return 0;

            const int radius = OsuDifficultyHitObject.NORMALISED_RADIUS;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            double currStrainTime = osuCurrObj.AdjustedDeltaTime;
            double lastStrainTime = osuPrevObj.AdjustedDeltaTime;

            if (withCheesability)
            {
                currStrainTime += osuCurrObj.ExtraDeltaTime * tuning.AgilityCheesabilityTimeScale;
                lastStrainTime += osuPrevObj.ExtraDeltaTime * tuning.AgilityCheesabilityTimeScale;
            }

            double currDistanceMultiplier = Smootherstep(osuCurrObj.LazyJumpDistance / radius, 1, 2);
            double prevDistanceMultiplier = Smootherstep(osuPrevObj.LazyJumpDistance / radius, 1, 2);

            double currTime = currStrainTime + lastStrainTime * (1 - prevDistanceMultiplier);
            double prevTime = lastStrainTime;

            double currentAngle = osuCurrObj.Angle!.Value * 180 / Math.PI;
            double prevAngle = osuPrevObj.Angle!.Value * 180 / Math.PI;

            double angleBonus = tuning.AgilityAngleBonusScale * Smootherstep(currentAngle, 0, 180);
            double baseFactor = 1 - tuning.AgilityAngleRepeatBaseScale * SnapAimEvaluator.AngleDifference(currentAngle, prevAngle);
            double angleRepetitionNerf = Math.Pow(baseFactor + (1 - baseFactor) * tuning.AgilityAngleRepeatVectorScale * SnapAimEvaluator.AngleVectorRepetition(osuCurrObj), tuning.AgilityAngleRepeatExponent);

            double baseBpm = tuning.AgilityBaseBpm / (1 + angleBonus * currDistanceMultiplier * prevDistanceMultiplier);

            double agilityBonus = Math.Max(0, Math.Pow(MillisecondsToBPM(Math.Max(currTime, prevTime), 2) / baseBpm, tuning.AgilityExponent) - 1);

            return agilityBonus * angleRepetitionNerf * tuning.AgilityOverallScale;
        }
    }
}
