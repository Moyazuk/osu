// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Difficulty.Preprocessing;
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
            previousStrains = new List<(double, double)>();
        }

        private double currentStrain;

        private const double backwards_strain_influence = 1000;

        private double skillMultiplier => 28.6;
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

        private readonly List<(double, double)> previousStrains;

        private double strainDecay(double ms) => Math.Pow(strainDecayBase, ms / 1000);
        protected override double CalculateInitialStrain(double time, DifficultyHitObject current) => currentStrain * strainDecay(time - current.Previous(0).StartTime);

        protected abstract double StrainValueOf(DifficultyHitObject current);

        protected override double StrainValueAt(DifficultyHitObject current)
        {
            var osuCurrent = (OsuDifficultyHitObject)current;

            double currentDifficulty = StrainValueOf(current) * skillMultiplier;

            currentStrain = getCurrentStrainValue(osuCurrent.StartTime, previousStrains) * 4.25;

            previousStrains.Add((osuCurrent.StartTime, currentDifficulty));

            if (current.BaseObject is Slider)
                sliderStrains.Add(currentDifficulty + currentStrain);

            return currentDifficulty + currentStrain;
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

        private double getCurrentStrainValue(double endTime, List<(double Time, double Diff)> previousDifficulties)
        {
            if (previousDifficulties.Count < 2)
                return 0;

            double sum = 0;

            double highestNoteVal = 0;
            double prevDeltaTime = 0;

            int index = 1;

            while (index < previousDifficulties.Count)
            {
                double prevTime = previousDifficulties[index - 1].Time;
                double currTime = previousDifficulties[index].Time;

                double deltaTime = currTime - prevTime;
                double prevDifficulty = previousDifficulties[index - 1].Diff;

                // How much of the current deltaTime does not fall under the backwards strain influence value.
                double startTimeOffset = Math.Max(0, endTime - prevTime - backwards_strain_influence);

                // If the deltaTime doesn't fall into the backwards strain influence value at all, we can remove its corresponding difficulty.
                // We don't iterate index because the list moves backwards.
                if (startTimeOffset > deltaTime)
                {
                    previousDifficulties.RemoveAt(0);

                    continue;
                }

                highestNoteVal = Math.Max(prevDifficulty, strainDecay(prevDeltaTime));
                prevDeltaTime = deltaTime;

                sum += highestNoteVal * (strainDecayAntiderivative(startTimeOffset) - strainDecayAntiderivative(deltaTime));

                index++;
            }

            // CalculateInitialStrain stuff
            highestNoteVal = Math.Max(previousDifficulties.Last().Diff, highestNoteVal);
            double lastTime = previousDifficulties.Last().Time;
            sum += (strainDecayAntiderivative(0) - strainDecayAntiderivative(endTime - lastTime)) * highestNoteVal;

            return sum;

            double strainDecayAntiderivative(double t) => Math.Pow(strainDecayBase, t / 1000) / Math.Log(1.0 / strainDecayBase);
        }

        public double CountTopWeightedSliders() => OsuStrainUtils.CountTopWeightedSliders(sliderStrains, DifficultyValue());
    }
}
