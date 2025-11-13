// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;

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

            double hitWindowGreat = ((OsuDifficultyHitObject)current).HitWindowGreat / 2;
            double effectiveHitWindow = hitWindowGreat;

            return effectiveHitWindow;
        }
    }
}
