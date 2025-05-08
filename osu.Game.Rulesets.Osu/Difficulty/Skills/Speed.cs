// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using System.Linq;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : ContinuousStrainSkill
    {
        private double skillMultiplier => 2.45;
        protected override double StrainDecayBase => 0.30;

        //protected override int ReducedSectionCount => 5;

        private double currentStrain;
        private double currentRhythm;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= StrainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += SpeedEvaluator.EvaluateDifficultyOf(current, Mods) * skillMultiplier;

            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            double totalStrain = currentStrain * currentRhythm;

            return totalStrain;
        }

        public double RelevantNoteCount()
        {
            if (Strains.Count == 0)
                return 0;

            double maxStrain = Strains.MaxBy(s => (s.Strain, s.StrainCountChange)).Strain;
            if (maxStrain == 0)
                return 0;

            return Strains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain.Strain / maxStrain * 12.0 - 6.0))));
        }
    }
}
