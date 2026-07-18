// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators.Speed;
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

            var osuCurrObj = (OsuDifficultyHitObject)current;

            double hitWindowGreat = ((OsuDifficultyHitObject)current).HitWindowGreat / 2;
            double rhythmMultiplier = RhythmEvaluator.EvaluateDifficultyOf(current);
            double midBpmBuff = CalculateMidBpmBuff(DiffUtils.BPMToMilliseconds(osuCurrObj.AdjustedDeltaTime)) * 1.2;
            double accuracyScale = rhythmMultiplier;
            double effectiveHitWindow = hitWindowGreat / accuracyScale;

            return effectiveHitWindow;
        }

        public static double CalculateMidBpmBuff(double bpm)
        {
            const double start = 40;
            const double midpoint = 200;
            const double end = 230;

            if (bpm <= midpoint)
                return 0.5 * DiffUtils.Smootherstep(bpm, start, midpoint);
            else
                return 0.5 + 0.5 * DiffUtils.Smootherstep(bpm, midpoint, end);
        }
    }
}
