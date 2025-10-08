// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Aggregation;
using osu.Game.Rulesets.Osu.Difficulty.Evaluators;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public class Aim : OsuTimeSkill
    {
        public readonly bool IncludeSliders;

        public Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private double currentAgilityStrain;
        private double aimMultiplier => 7.7;
        private double strainDecayBase => 0.15;
        private double agilityStrainDecayBase => 0.1;

        private readonly List<double> sliderStrains = new List<double>();

        protected override double HitProbability(double skill, double difficulty)
        {
            if (difficulty <= 0) return 1;
            if (skill <= 0) return 0;

            return DifficultyCalculationUtils.Erf(skill / (Math.Sqrt(2) * difficulty));
        }

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);

        private double agilityStrainDecay(double ms) => Math.Pow(agilityStrainDecayBase, ms / 1000);

        protected double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);
            currentAgilityStrain *= agilityStrainDecay(current.DeltaTime);

            double currentDifficulty;
            double snapDifficulty = SnapAimEvaluator.EvaluateDifficultyOf(current, IncludeSliders);
            double flowDifficulty = FlowAimEvaluator.EvaluateDifficultyOf(current);
            double agilityDifficulty = AgilityEvaluator.EvaluateDifficultyOf(current);

            bool isFlow = (flowDifficulty) < (snapDifficulty + agilityDifficulty);

            if (isFlow)
            {
                currentDifficulty = flowDifficulty;
                currentStrain += currentDifficulty;
            }
            else
            {
                currentDifficulty = snapDifficulty;
                currentAgilityStrain += agilityDifficulty;
                currentStrain += currentDifficulty + currentAgilityStrain;
            }

            if (current.BaseObject is Slider)
            {
                sliderStrains.Add(currentStrain);
            }

            return currentStrain * aimMultiplier;
        }

        public double GetDifficultSliders()
        {
            if (sliderStrains.Count == 0)
                return 0;

            double maxSliderStrain = sliderStrains.Max();

            if (maxSliderStrain == 0)
                return 0;

            return sliderStrains.Sum(strain => 1.0 / (1.0 + Math.Exp(-(strain / maxSliderStrain * 12.0 - 6.0))));
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
