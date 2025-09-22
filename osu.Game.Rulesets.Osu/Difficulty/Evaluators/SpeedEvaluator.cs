// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class SpeedEvaluator
    {
        private const double single_spacing_threshold = OsuDifficultyHitObject.NORMALISED_DIAMETER * 1.25; // 1.25 circles distance between centers
        private const double min_speed_bonus = 200; // 200 BPM 1/4th
        private const double speed_balancing_factor = 40;
        private const double distance_multiplier = 0.8;

        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>distance between those objects,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current, IReadOnlyList<Mod> mods)
        {
            if (current.BaseObject is Spinner)
                return 0;

            // derive strainTime for calculation
            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = osuCurrObj.TapIndex is > 0 ? (OsuDifficultyHitObject)osuCurrObj.PreviousTap(0) : null;

            if (!osuCurrObj.IsTapObject)
                return 0;

            // --- DEBUG: inspect tap timing context (no behavior change) ---
            var prevTap0 = osuPrevObj; // already computed as osuCurrObj.TapIndex > 0 ? osuCurrObj.PreviousTap(0) : null
            double rawTapStrain = osuCurrObj.TapStrainTime;
            double clampFactor = Math.Clamp((rawTapStrain / osuCurrObj.HitWindowGreat) / 0.93, 0.92, 1);

            Console.WriteLine(
                $"[SpeedEval] idx={osuCurrObj.Index} tapIdx={(osuCurrObj.TapIndex?.ToString() ?? "NULL")} isTap={osuCurrObj.IsTapObject} " +
                $"rawTapStrain={rawTapStrain} hitWindow={osuCurrObj.HitWindowGreat} clampFactor={clampFactor} " +
                $"currType={osuCurrObj.BaseObject.GetType().Name} prevTapIdx={(prevTap0==null ? "NULL" : prevTap0.Index.ToString())} " +
                $"currStart={osuCurrObj.StartTime} prevTapStart={(prevTap0==null ? "NULL" : prevTap0.StartTime.ToString())} " +
                $"delta={(prevTap0==null ? "NULL" : (osuCurrObj.StartTime - prevTap0.StartTime).ToString())}"
            );

            double strainTime = osuCurrObj.TapStrainTime;
            double doubletapness = osuCurrObj.IsTapObject ? 1.0 - osuCurrObj.GetDoubletapness((OsuDifficultyHitObject?)osuCurrObj.NextTap(0)) : 1;

            // Cap deltatime to the OD 300 hitwindow.
            // 0.93 is derived from making sure 260bpm OD8 streams aren't nerfed harshly, whilst 0.92 limits the effect of the cap.
            strainTime /= Math.Clamp((strainTime / osuCurrObj.HitWindowGreat) / 0.93, 0.92, 1);

            // speedBonus will be 0.0 for BPM < 200
            double speedBonus = 0.0;

            // Add additional scaling bonus for streams/bursts higher than 200bpm
            if (DifficultyCalculationUtils.MillisecondsToBPM(strainTime) > min_speed_bonus)
                speedBonus += 0.75 * Math.Pow((DifficultyCalculationUtils.BPMToMilliseconds(min_speed_bonus) - strainTime) / speed_balancing_factor, 2);



            // Base difficulty with all bonuses
            double difficulty = (1.0 + speedBonus) * 1000 / strainTime;

            // Apply penalty if there's doubletappable doubles
            return difficulty * doubletapness;
        }
    }
}
