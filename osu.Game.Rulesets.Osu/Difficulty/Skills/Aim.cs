// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Utils;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Difficulty.Skills
{
    /// <summary>
    /// Represents the skill required to correctly aim at every object in the map with a uniform CircleSize and normalized distances.
    /// </summary>
    public abstract class Aim : OsuStrainSkill
    {
        public readonly bool IncludeSliders;

        protected Aim(Mod[] mods, bool includeSliders)
            : base(mods)
        {
            IncludeSliders = includeSliders;
        }

        private double currentStrain;

        private double skillMultiplier => 26.0;
        private double strainDecayBase => 0.15;

        private const double k = 7.27;

        // A function that turns the ratio of snap : flow into the probability of snapping/flowing
        // It has the constraints:
        // P(snap) + P(flow) = 1 (the object is always either snapped or flowed)
        // P(snap) = f(snap/flow), P(flow) = f(flow/snap) (ie snap and flow are symmetric and reversible)
        // Therefore: f(x) + f(1/x) = 1
        // 0 <= f(x) <= 1 (cannot have negative or greater than 100% probability of snapping or flowing)
        // This logistic function is a solution, which fits nicely with the general idea of interpolation and provides a tuneable constant
        protected static double ProbabilityOf(double ratio)
            => ratio == 0 ? 0 :
                double.IsNaN(ratio) ? 1 :
                (1 / (1 + Math.Exp(-k * Math.Log(ratio))));

        private readonly List<double> sliderStrains = new List<double>();

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected abstract double StrainValueOf(DifficultyHitObject current);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            currentStrain *= strainDecay(current.DeltaTime);

            currentStrain += StrainValueOf(current) * skillMultiplier;

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentStrain);

            return currentStrain;
        }

        private const double k = 7.27;

        // A function that turns the ratio of snap : flow into the probability of snapping/flowing
        // It has the constraints:
        // P(snap) + P(flow) = 1 (the object is always either snapped or flowed)
        // P(snap) = f(snap/flow), P(flow) = f(flow/snap) (ie snap and flow are symmetric and reversible)
        // Therefore: f(x) + f(1/x) = 1
        // 0 <= f(x) <= 1 (cannot have negative or greater than 100% probability of snapping or flowing)
        // This logistic function is a solution, which fits nicely with the general idea of interpolation and provides a tuneable constant
        protected static double ProbabilityOf(double ratio)
            => ratio == 0 ? 0 :
                double.IsNaN(ratio) ? 1 :
                (1 / (1 + Math.Exp(-k * Math.Log(ratio))));

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
