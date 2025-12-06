// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;

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
            double snap = AimEvaluator.EvaluateDifficultyOf(current, IncludeSliders) + AgilityEvaluator.EvaluateDifficultyOf(current);
            double flow = FlowAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);

            double pSnap = ProbabilityOf(flow / snap);
            double pFlow = 1 - pSnap; // same as ProbabilityOf(snap / flow)

            return snap * pSnap + flow * pFlow;
        }
    }
}
