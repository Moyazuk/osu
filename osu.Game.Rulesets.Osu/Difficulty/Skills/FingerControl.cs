// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
using osu.Game.Rulesets.Osu.Objects;
using System.Linq;
using osu.Game.Rulesets.Osu.Difficulty.Utils;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to press keys in time with complex rhythmic patterning.
    /// </summary>
    public class FingerControl : OsuStrainSkill
    {
        private double skillMultiplier => 18.8;
        private double strainDecayBase => 0.20;

        private double currentStrain;

        private readonly List<double> sliderStrains = new List<double>();

        protected override int ReducedSectionCount => 5;

        public FingerControl(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => (currentStrain) * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += RhythmEvaluator.EvaluateDifficultyOf(current) * skillMultiplier;

            double totalStrain = currentStrain;

            if (current.BaseObject is Slider)
                sliderStrains.Add(totalStrain);

            return totalStrain;
        }

        public double RelevantNoteCount()
        {
            if (ObjectStrains.Count == 0)
                return 0;

            double maxStrain = ObjectStrains.Max();
            if (maxStrain == 0)
                return 0;

            return ObjectStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
