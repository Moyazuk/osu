// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class CombinedAim : Aim
    {
        public CombinedAim(Mod[] mods, bool includeSliders, bool includeControlFactors)
            : base(mods, includeSliders, includeControlFactors)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double snap = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, IncludeControlFactors) + AgilityEvaluator.EvaluateDifficultyOf(current, IncludeSliders, IncludeControlFactors);
            double flow = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders, IncludeControlFactors);

            return Math.Min(snap, flow);
        }
    }
}
