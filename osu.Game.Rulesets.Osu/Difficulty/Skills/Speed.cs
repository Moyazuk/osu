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
    /// Represents the skill required to press keys with regards to keeping up with the speed at which objects need to be hit.
    /// </summary>
    public class Speed : OsuStrainSkill
    {
        private double skillMultiplier => 1.45;
        private double strainDecayBase => 0.3;

        private double currentStrain;
        private double currentRhythm;

        private readonly List<double> sliderStrains = new List<double>();

        private double aimMultiplier => 1.46 * 1.53;
        private double aimDecayBase => 0.15;
        private double currentAim;

        protected override int ReducedSectionCount => 5;

        public Speed(Mod[] mods)
            : base(mods)
        {
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        private double aimDecay(double ms) => Math.Pow(aimDecayBase, ms / 1000);

        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => (currentStrain * strainDecay(time - current.Previous(0).StartTime) + currentAim * aimDecay(time - current.Previous(0).StartTime)) * currentRhythm;

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(((OsuDifficultyHitObject)current).StrainTime);
            currentStrain += SpeedEvaluator.EvaluateDifficultyOf(current, Mods) * skillMultiplier;

            currentRhythm = RhythmEvaluator.EvaluateDifficultyOf(current);

            //currentAim *= aimDecay(((OsuDifficultyHitObject)current).StrainTime);
            //currentAim += SpeedAimEvaluator.EvaluateDifficultyOf(current, Mods) * aimMultiplier;

            double totalStrain = (currentStrain) * currentRhythm;

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
