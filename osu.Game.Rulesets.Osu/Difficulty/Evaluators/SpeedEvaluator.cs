// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Difficulty;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>distance between those objects,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, OsuDifficultyTuning tuning)
        {
            if (current.BaseObject is Spinner)
                return 0;

            // derive strainTime for calculation
            var osuCurrObj = (OsuDifficultyHitObject)current;

            double strainTime = osuCurrObj.AdjustedDeltaTime;
            double doubletapness = 1.0 - osuCurrObj.GetDoubletapness((OsuDifficultyHitObject?)osuCurrObj.Next(0));

            // speedBonus will be 0.0 for BPM < 200
            double speedBonus = 0.0;

            // Add additional scaling bonus for streams/bursts higher than 200bpm
            if (DifficultyCalculationUtils.MillisecondsToBPM(strainTime) > tuning.SpeedMinBonusBpm)
                speedBonus = tuning.SpeedBonusScale * Math.Pow((DifficultyCalculationUtils.BPMToMilliseconds(tuning.SpeedMinBonusBpm) - strainTime) / tuning.SpeedBalancingFactor, tuning.SpeedBonusExponent);

            // Base difficulty with all bonuses
            double difficulty = (1 + speedBonus) * tuning.SpeedBaseScale / strainTime;

            // Apply penalty if there's doubletappable doubles
            return difficulty * doubletapness;
        }
    }
}
