// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Difficulty.Evaluators
{
    public static class AccuracyEvaluator
    {
        public static double EvaluateEffectiveHitWindow(DifficultyHitObject current)
        {
            if (current.BaseObject is Spinner)
            {
                return double.PositiveInfinity;
            }

            var osuCurrObj = (OsuDifficultyHitObject)current;

            double hitWindowGreat = ((OsuDifficultyHitObject)current).HitWindow(HitResult.Great);
            double r = Math.Max(RhythmEvaluator.EvaluateDifficultyOf(current), 1.0);
            double effectiveHitWindow = hitWindowGreat / Math.Pow(r, 0.4);

            return effectiveHitWindow;
        }
    }
}
