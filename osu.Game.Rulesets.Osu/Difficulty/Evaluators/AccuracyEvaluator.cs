// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Utils;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AccuracyEvaluator
    {


        /// <summary>
        /// Evaluates the difficulty of tapping the current object, based on:
        /// <list type="bullet">
        /// <item><description>time between pressing the previous and current object,</description></item>
        /// <item><description>and how easily they can be cheesed.</description></item>
        /// </list>
        /// </summary>
        public static double EvaluateDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            var osuCurrObj = (OsuDifficultyHitObject)current;
            var osuPrevObj = (OsuDifficultyHitObject)current.Previous(0);

            if (osuPrevObj == null)
                return 1;

            // Use custom cap value to ensure that that at this point delta time is actually zero
            double currDelta = Math.Max(osuCurrObj.DeltaTime, 1e-7);
            double prevDelta = Math.Max(osuPrevObj.DeltaTime, 1e-7);

            double deltaDifference = Math.Max(prevDelta, currDelta) / Math.Min(prevDelta, currDelta);

            double effectiveRatio = getEffectiveRatio(deltaDifference);

            double difficulty = 1;

            difficulty *= effectiveRatio;

            Console.WriteLine($"effective ratio: {effectiveRatio}");

            return difficulty;
        }

        private static double getEffectiveRatio(double deltaDifference)
        {
            var ratioMultipliers = new[]
            {
                (1.0, 0.1), // same rhythm
                (4.0 / 3.0, 1.0), // 1/4 <-> 1/3
                (1.5, 1), // 1/3 <-> 1/2
                (5.0 / 3.0, 1.0), // 1/5 <-> 1/3
                (2.0, 0.75), // 1/4 <-> 1/2
                (2.5, 1), // 1/5 <-> 1/2
                (3.0, 1), // 1/3 <-> 1/1
                (4.0, 1) // 1/4 <-> 1/1
            };

            return LerpFromArrays(ratioMultipliers, deltaDifference);
        }

        public static double LerpFromArrays((double ratio, double multiplier)[] ratioMultipliers, double t)
        {
            if (t <= ratioMultipliers[0].ratio)
                return ratioMultipliers[0].multiplier;

            if (t >= ratioMultipliers[^1].ratio)
                return ratioMultipliers[^1].multiplier;

            for (int i = 0; i < ratioMultipliers.Length - 1; i++)
            {
                if (t >= ratioMultipliers[i].ratio && t <= ratioMultipliers[i + 1].ratio)
                {
                    double distance = (t - ratioMultipliers[i].ratio) / (ratioMultipliers[i + 1].ratio - ratioMultipliers[i].ratio);
                    return Interpolation.Lerp(ratioMultipliers[i].multiplier, ratioMultipliers[i + 1].multiplier, distance);
                }
            }

            return 0;
        }
    }
}
