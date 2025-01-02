// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    public class Rhythm : OsuProbabilitySkill
    {
        private double strainDecayBase => 0.3;

        private double currentStrain;

        private double skillMultiplier => 8;

        public Rhythm(Mod[] mods)
            : base(mods)
        {
        }

        protected override double HitProbability(double skill, double difficulty)
        {
            if (skill <= 0) return 0;
            if (difficulty <= 0) return 1;

            // An arbitrary formula to gauge rhythm scaling
            return Math.Pow(0.9, Math.Pow(difficulty, 2) / skill);
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += Math.Max(0, RhythmEvaluator.EvaluateDifficultyOf(current) - 1) * skillMultiplier;

            return currentStrain;
        }
    }
}
