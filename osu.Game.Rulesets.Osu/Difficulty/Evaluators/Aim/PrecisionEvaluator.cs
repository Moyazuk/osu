// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators.Aim
{
    public static class PrecisionEvaluator
    {
        /// <summary>
        /// Evaluates the difficulty of small circles
        /// </summary>
        public static double EvaluateSnapDifficultyOf(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
                return 0;

            double objectRadius = ((OsuHitObject)current.BaseObject).Radius;

            double difficulty = Math.Max(0, (30 - objectRadius) / 70);

            return difficulty;
        }
    }
}
