// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class CombinedAim : Aim
    {
        public CombinedAim(Mod[] mods, bool includeSliders)
            : base(mods, includeSliders)
        {
        }

        protected override double StrainValueOf(DifficultyHitObject current)
        {
            double snap = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double flow = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);

            double p_snap = ProbabilityOf(flow / snap);
            double p_flow = 1 - p_snap; // same as ProbabilityOf(snap / flow)
            
            return snap * p_snap + flow * p_flow;
        }
    }
}
